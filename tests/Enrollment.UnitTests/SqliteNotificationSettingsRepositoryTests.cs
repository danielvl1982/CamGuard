using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>Integration tests for <see cref="SqliteNotificationSettingsRepository"/> against a temp SQLite file.</summary>
public sealed class SqliteNotificationSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-settings-{Guid.NewGuid():N}.db");
    private readonly SqliteNotificationSettingsRepository _repository;

    public SqliteNotificationSettingsRepositoryTests()
    {
        var options = Options.Create(new VectorStoreOptions { ConnectionString = $"Data Source={_dbPath}" });
        _repository = new SqliteNotificationSettingsRepository(options, new ActivitySource("Enrollment.UnitTests.Settings"));
    }

    [Fact]
    public async Task Never_stored_returns_every_channel()
    {
        var channels = await _repository.GetDefaultChannelsAsync(CancellationToken.None);

        Assert.Equal(
            [NotificationChannel.Push, NotificationChannel.Email],
            channels.OrderBy(c => c));
    }

    [Fact]
    public async Task Set_subset_is_returned()
    {
        await _repository.SetDefaultChannelsAsync([NotificationChannel.Email], CancellationToken.None);

        Assert.Equal([NotificationChannel.Email], await _repository.GetDefaultChannelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Set_empty_is_stored_and_distinct_from_never_stored()
    {
        await _repository.SetDefaultChannelsAsync([], CancellationToken.None);

        Assert.Empty(await _repository.GetDefaultChannelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Set_replaces_the_previous_set()
    {
        await _repository.SetDefaultChannelsAsync([NotificationChannel.Push, NotificationChannel.Email], CancellationToken.None);
        await _repository.SetDefaultChannelsAsync([NotificationChannel.Push], CancellationToken.None);

        Assert.Equal([NotificationChannel.Push], await _repository.GetDefaultChannelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Undefined_channel_is_rejected_and_nothing_is_stored()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.SetDefaultChannelsAsync([(NotificationChannel)99], CancellationToken.None));

        Assert.Equal(2, (await _repository.GetDefaultChannelsAsync(CancellationToken.None)).Count);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
