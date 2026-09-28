using Common;
using CSharpFunctionalExtensions;
using Enrollment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="IIncidentRepository"/> adapter: incidents need no vector search (only
/// newest-first listing), so they live in a PLAIN SQLite table via
/// <c>Microsoft.Data.Sqlite</c> (already referenced) instead of a SqliteVec
/// collection — no embedding column, no vector index, no preview-package surface.
/// Schema migration follows the T1/T2 precedent: <c>CREATE TABLE IF NOT EXISTS</c>
/// never adds columns to an existing table, so each data column is migrated with
/// raw ALTER TABLE guarded by single-quoted <c>pragma_table_info</c> + a
/// duplicate-column race catch.
/// </summary>
public sealed class SqliteIncidentRepository : IIncidentRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly ActivitySource _activitySource;

    public SqliteIncidentRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _connectionString = value.ConnectionString;
        _tableName = value.IncidentsTableName;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText =
                $"CREATE TABLE IF NOT EXISTS {QuotedTable} (" +
                "\"Id\" TEXT PRIMARY KEY, " +
                "\"OccurredAtUtc\" TEXT NOT NULL DEFAULT '', " +
                "\"Kind\" TEXT NOT NULL DEFAULT '', " +
                "\"MemberId\" TEXT NULL, " +
                "\"Note\" TEXT NULL, " +
                "\"SnapshotImagePath\" TEXT NULL)";
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await EnsureColumnAsync(connection, "OccurredAtUtc", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "Kind", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "MemberId", "TEXT NULL", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "Note", "TEXT NULL", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(connection, "SnapshotImagePath", "TEXT NULL", cancellationToken).ConfigureAwait(false);
    }

    private string QuotedTable =>
        "\"" + _tableName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private async Task EnsureColumnAsync(
        SqliteConnection connection,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        // Identifiers cannot be parameterized. The table-valued pragma requires the
        // name as a SINGLE-quoted string literal (see SqliteVecMemberRepository).
        var pragmaTableArg = "'" + _tableName.Replace("'", "''", StringComparison.Ordinal) + "'";
        var columnArg = "'" + columnName.Replace("'", "''", StringComparison.Ordinal) + "'";

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info({pragmaTableArg}) WHERE name = {columnArg}";
            var exists = Convert.ToInt32(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            if (exists)
            {
                return;
            }
        }

        await using (var alter = connection.CreateCommand())
        {
            alter.CommandText = $"ALTER TABLE {QuotedTable} ADD COLUMN \"{columnName}\" {columnDefinition}";
            try
            {
                await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
                // Lost a race with another process migrating the same table — the column
                // now exists, so there is nothing left to do.
            }
        }
    }

    public async Task SaveAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);

        using var activity = _activitySource.StartActivity("Enrollment.Incidents.Save");
        var startTimestamp = Stopwatch.GetTimestamp();

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT OR REPLACE INTO {QuotedTable} " +
            "(\"Id\", \"OccurredAtUtc\", \"Kind\", \"MemberId\", \"Note\", \"SnapshotImagePath\") " +
            "VALUES (@id, @occurredAtUtc, @kind, @memberId, @note, @snapshotImagePath)";
        command.Parameters.AddWithValue("@id", incident.Id.Value.ToString());
        command.Parameters.AddWithValue("@occurredAtUtc", incident.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@kind", incident.Kind.ToString());
        command.Parameters.AddWithValue("@memberId", (object?)incident.MemberId?.Value.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("@note", (object?)incident.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@snapshotImagePath", (object?)incident.SnapshotImagePath ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }

    public async Task<IReadOnlyList<Incident>> ListRecentAsync(int limit, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.Incidents.ListRecent");

        if (limit <= 0)
        {
            return [];
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        // OccurredAtUtc is stored in ISO-8601 round-trip form (all UTC), so TEXT
        // ordering is chronological. rowid breaks same-timestamp ties by insertion
        // order, keeping the listing deterministic.
        command.CommandText =
            $"SELECT \"Id\", \"OccurredAtUtc\", \"Kind\", \"MemberId\", \"Note\", \"SnapshotImagePath\" " +
            $"FROM {QuotedTable} ORDER BY \"OccurredAtUtc\" DESC, rowid DESC LIMIT @limit";
        command.Parameters.AddWithValue("@limit", limit);

        var incidents = new List<Incident>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            incidents.Add(ReadIncident(reader));
        }

        activity?.SetTag("count", incidents.Count);
        return incidents;
    }

    public async Task<Incident?> FindByIdAsync(IncidentId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.Incidents.FindById");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"Id\", \"OccurredAtUtc\", \"Kind\", \"MemberId\", \"Note\", \"SnapshotImagePath\" " +
            $"FROM {QuotedTable} WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("@id", id.Value.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadIncident(reader);
    }

    private static Incident ReadIncident(SqliteDataReader reader)
    {
        var idText = reader.GetString(0);
        if (!Guid.TryParse(idText, out var idGuid))
        {
            throw new InvalidOperationException($"Incident row has a corrupt id '{idText}'.");
        }

        var id = IncidentId.From(idGuid);
        if (id.IsFailure)
        {
            throw new InvalidOperationException($"Incident row has a corrupt id '{idText}'.");
        }

        var occurredAt = DateTime.Parse(
            reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        var kindText = reader.GetString(2);
        if (!Enum.TryParse<IncidentKind>(kindText, out var kind) || !Enum.IsDefined(kind))
        {
            throw new InvalidOperationException($"Incident row '{idText}' has an unknown kind '{kindText}'.");
        }

        MemberId? memberId = null;
        if (!reader.IsDBNull(3))
        {
            var memberText = reader.GetString(3);
            if (!Guid.TryParse(memberText, out var memberGuid))
            {
                throw new InvalidOperationException($"Incident row '{idText}' has a corrupt member id '{memberText}'.");
            }

            var memberIdResult = MemberId.From(memberGuid);
            if (memberIdResult.IsFailure)
            {
                throw new InvalidOperationException($"Incident row '{idText}' has a corrupt member id '{memberText}'.");
            }

            memberId = memberIdResult.Value;
        }

        return Incident.Rehydrate(
            id.Value,
            occurredAt,
            kind,
            memberId,
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }
}
