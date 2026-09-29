using Common;
using Enrollment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="IRoleRepository"/> adapter over two PLAIN SQLite tables (<c>roles</c> and
/// <c>role_channels</c>), modelled on <see cref="SqliteIncidentRepository"/>: no vector search is
/// needed, so no SqliteVec surface. Foreign keys are enabled per connection so deleting a role
/// cascades to its channels. <see cref="IsInUseAsync"/> reads the members table directly, tolerating
/// a store where that table or its <c>RoleId</c> column does not exist yet.
/// </summary>
public sealed class SqliteRoleRepository : IRoleRepository
{
    private readonly string _connectionString;
    private readonly string _rolesTable;
    private readonly string _channelsTable;
    private readonly string _membersTable;
    private readonly ActivitySource _activitySource;

    /// <summary>Schema is ensured at most once per instance; safe because the type is registered Scoped.</summary>
    private bool _schemaEnsured;

    public SqliteRoleRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _connectionString = value.ConnectionString;
        _rolesTable = Quote(value.RolesTableName);
        _channelsTable = Quote(value.RoleChannelsTableName);
        _membersTable = value.MembersCollectionName;
    }

    private static string Quote(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // SQLite ignores foreign keys unless enabled on every connection.
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var create = connection.CreateCommand();
        create.CommandText =
            $"CREATE TABLE IF NOT EXISTS {_rolesTable} (" +
            "\"Id\" TEXT PRIMARY KEY, " +
            "\"Name\" TEXT NOT NULL UNIQUE COLLATE NOCASE, " +
            "\"CreatedAtUtc\" TEXT NOT NULL DEFAULT ''); " +
            $"CREATE TABLE IF NOT EXISTS {_channelsTable} (" +
            $"\"RoleId\" TEXT NOT NULL REFERENCES {_rolesTable}(\"Id\") ON DELETE CASCADE, " +
            "\"Channel\" TEXT NOT NULL, " +
            "PRIMARY KEY (\"RoleId\", \"Channel\"))";
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _schemaEnsured = true;
    }

    public async Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        using var activity = _activitySource.StartActivity("Enrollment.Roles.Save");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Upsert (not INSERT OR REPLACE): a REPLACE would delete the row and cascade away its channels.
        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                $"INSERT INTO {_rolesTable} (\"Id\", \"Name\", \"CreatedAtUtc\") VALUES (@id, @name, @createdAtUtc) " +
                "ON CONFLICT(\"Id\") DO UPDATE SET \"Name\" = excluded.\"Name\"";
            upsert.Parameters.AddWithValue("@id", role.Id.Value.ToString());
            upsert.Parameters.AddWithValue("@name", role.Name);
            upsert.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = $"DELETE FROM {_channelsTable} WHERE \"RoleId\" = @id";
            clear.Parameters.AddWithValue("@id", role.Id.Value.ToString());
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var channel in role.Channels)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {_channelsTable} (\"RoleId\", \"Channel\") VALUES (@id, @channel)";
            insert.Parameters.AddWithValue("@id", role.Id.Value.ToString());
            insert.Parameters.AddWithValue("@channel", channel.ToString());
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Role?> FindByIdAsync(RoleId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.Roles.FindById");
        var roles = await QueryAsync("WHERE r.\"Id\" = @value", id.Value.ToString(), cancellationToken).ConfigureAwait(false);
        return roles.Count == 0 ? null : roles[0];
    }

    public async Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        using var activity = _activitySource.StartActivity("Enrollment.Roles.FindByName");
        // The Name column is COLLATE NOCASE, so plain equality is case-insensitive.
        var roles = await QueryAsync("WHERE r.\"Name\" = @value", name.Trim(), cancellationToken).ConfigureAwait(false);
        return roles.Count == 0 ? null : roles[0];
    }

    public async Task<IReadOnlyList<Role>> ListAllAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.Roles.ListAll");
        var roles = await QueryAsync(null, null, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("count", roles.Count);
        return roles;
    }

    public async Task DeleteAsync(RoleId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.Roles.Delete");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {_rolesTable} WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("@id", id.Value.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsInUseAsync(RoleId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.Roles.IsInUse");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // The members table (and its RoleId column) may not exist yet on a fresh or legacy store:
        // then nobody can hold a role. Single-quoted literal is required by the table-valued pragma.
        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText =
                $"SELECT COUNT(*) FROM pragma_table_info('{_membersTable.Replace("'", "''", StringComparison.Ordinal)}') WHERE name = 'RoleId'";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 0)
            {
                return false;
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {Quote(_membersTable)} WHERE \"RoleId\" = @id)";
        command.Parameters.AddWithValue("@id", id.Value.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    private async Task<List<Role>> QueryAsync(string? whereClause, string? value, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT r.\"Id\", r.\"Name\", c.\"Channel\" " +
            $"FROM {_rolesTable} r LEFT JOIN {_channelsTable} c ON c.\"RoleId\" = r.\"Id\" " +
            $"{whereClause} ORDER BY r.\"Name\", r.\"Id\"";
        if (value is not null)
        {
            command.Parameters.AddWithValue("@value", value);
        }

        var roles = new List<Role>();
        string? currentId = null;
        string currentName = string.Empty;
        var channels = new List<NotificationChannel>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var idText = reader.GetString(0);
            if (idText != currentId)
            {
                Flush();
                currentId = idText;
                currentName = reader.GetString(1);
                channels = [];
            }

            if (!reader.IsDBNull(2))
            {
                channels.Add(ParseChannel(reader.GetString(2), idText));
            }
        }

        Flush();
        return roles;

        void Flush()
        {
            if (currentId is null)
            {
                return;
            }

            if (!Guid.TryParse(currentId, out var guid) || RoleId.From(guid) is not { IsSuccess: true } roleId)
            {
                throw new InvalidOperationException($"Role row has a corrupt id '{currentId}'.");
            }

            roles.Add(Role.Rehydrate(roleId.Value, currentName, channels));
        }
    }

    private static NotificationChannel ParseChannel(string text, string roleId)
    {
        if (!Enum.TryParse<NotificationChannel>(text, out var channel) || !Enum.IsDefined(channel))
        {
            throw new InvalidOperationException($"Role row '{roleId}' has an unknown channel '{text}'.");
        }

        return channel;
    }
}
