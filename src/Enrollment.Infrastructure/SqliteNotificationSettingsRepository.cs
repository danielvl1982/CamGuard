using Common;
using Enrollment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="INotificationSettingsRepository"/> adapter over two PLAIN SQLite tables: one row per
/// default channel, plus a marker row in a key/value table so an explicitly stored empty set is
/// distinguishable from a store that was never configured.
/// </summary>
public sealed class SqliteNotificationSettingsRepository : INotificationSettingsRepository
{
    private const string ConfiguredKey = "default_channels_configured";

    private readonly string _connectionString;
    private readonly string _channelsTable;
    private readonly string _settingsTable;
    private readonly ActivitySource _activitySource;

    /// <summary>Schema is ensured at most once per instance; safe because the type is registered Scoped.</summary>
    private bool _schemaEnsured;

    public SqliteNotificationSettingsRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _connectionString = value.ConnectionString;
        _channelsTable = Quote(value.DefaultChannelsTableName);
        _settingsTable = Quote(value.NotificationSettingsTableName);
    }

    private static string Quote(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
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
            $"CREATE TABLE IF NOT EXISTS {_channelsTable} (\"Channel\" TEXT PRIMARY KEY); " +
            $"CREATE TABLE IF NOT EXISTS {_settingsTable} (\"Key\" TEXT PRIMARY KEY, \"Value\" TEXT NOT NULL)";
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _schemaEnsured = true;
    }

    public async Task<IReadOnlySet<NotificationChannel>> GetDefaultChannelsAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.NotificationSettings.GetDefaultChannels");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var marker = connection.CreateCommand())
        {
            marker.CommandText = $"SELECT EXISTS(SELECT 1 FROM {_settingsTable} WHERE \"Key\" = @key)";
            marker.Parameters.AddWithValue("@key", ConfiguredKey);
            if (Convert.ToInt32(await marker.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 0)
            {
                return new HashSet<NotificationChannel>(Enum.GetValues<NotificationChannel>());
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Channel\" FROM {_channelsTable}";

        var channels = new HashSet<NotificationChannel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var text = reader.GetString(0);
            if (!Enum.TryParse<NotificationChannel>(text, out var channel) || !Enum.IsDefined(channel))
            {
                throw new InvalidOperationException($"Default channels contain an unknown channel '{text}'.");
            }

            channels.Add(channel);
        }

        return channels;
    }

    public async Task SetDefaultChannelsAsync(IReadOnlyCollection<NotificationChannel> channels, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Any(c => !Enum.IsDefined(c)))
        {
            throw new ArgumentException("Undefined notification channel.", nameof(channels));
        }

        using var activity = _activitySource.StartActivity("Enrollment.NotificationSettings.SetDefaultChannels");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = $"DELETE FROM {_channelsTable}";
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var channel in channels.Distinct())
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {_channelsTable} (\"Channel\") VALUES (@channel)";
            insert.Parameters.AddWithValue("@channel", channel.ToString());
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var marker = connection.CreateCommand())
        {
            marker.Transaction = transaction;
            marker.CommandText = $"INSERT OR REPLACE INTO {_settingsTable} (\"Key\", \"Value\") VALUES (@key, '1')";
            marker.Parameters.AddWithValue("@key", ConfiguredKey);
            await marker.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
