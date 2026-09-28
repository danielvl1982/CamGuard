using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers incident-log T2 (incident persistence): repository save/find/list
/// round-trip against a temp SQLite file, newest-first ordering, limit handling,
/// null-safe optionals, idempotent re-recording, and domain normalization.
/// </summary>
public sealed class IncidentRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteIncidentRepository _repository;

    public IncidentRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-incidents-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            IncidentsTableName = "incidents_test"
        });
        _repository = new SqliteIncidentRepository(options, new ActivitySource("Enrollment.UnitTests.Incidents"));
    }

    private static Incident NoteAt(DateTime occurredAtUtc, string? note = "Door event") =>
        Incident.Create(IncidentId.New(), occurredAtUtc, IncidentKind.ManualNote, note: note);

    [Fact]
    public async Task SaveAsync_round_trip_persists_every_field_and_FindByIdAsync_returns_it()
    {
        var memberId = MemberId.New();
        var occurredAt = new DateTime(2026, 9, 27, 12, 30, 0, DateTimeKind.Utc);
        var incident = Incident.Create(
            IncidentId.New(), occurredAt, IncidentKind.AnnulledAttempt, memberId, "Annulled retry", "snaps/shot.jpg");

        await _repository.SaveAsync(incident, CancellationToken.None);

        var found = await _repository.FindByIdAsync(incident.Id, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(incident.Id, found!.Id);
        Assert.Equal(occurredAt, found.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, found.OccurredAtUtc.Kind);
        Assert.Equal(IncidentKind.AnnulledAttempt, found.Kind);
        Assert.Equal(memberId, found.MemberId);
        Assert.Equal("Annulled retry", found.Note);
        Assert.Equal("snaps/shot.jpg", found.SnapshotImagePath);
    }

    [Fact]
    public async Task FindByIdAsync_unknown_id_returns_null()
    {
        Assert.Null(await _repository.FindByIdAsync(IncidentId.New(), CancellationToken.None));
    }

    [Fact]
    public async Task ListRecentAsync_returns_newest_first()
    {
        var baseTime = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        var oldest = NoteAt(baseTime);
        var middle = NoteAt(baseTime.AddHours(1));
        var newest = NoteAt(baseTime.AddHours(2));
        // Save out of order to prove ordering comes from OccurredAtUtc, not insertion.
        await _repository.SaveAsync(newest, CancellationToken.None);
        await _repository.SaveAsync(oldest, CancellationToken.None);
        await _repository.SaveAsync(middle, CancellationToken.None);

        var listed = await _repository.ListRecentAsync(10, CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.Equal(newest.Id, listed[0].Id);
        Assert.Equal(middle.Id, listed[1].Id);
        Assert.Equal(oldest.Id, listed[2].Id);
    }

    [Fact]
    public async Task ListRecentAsync_honors_limit()
    {
        var baseTime = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(NoteAt(baseTime.AddMinutes(i)), CancellationToken.None);
        }

        var listed = await _repository.ListRecentAsync(3, CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.True(
            listed[0].OccurredAtUtc > listed[1].OccurredAtUtc &&
            listed[1].OccurredAtUtc > listed[2].OccurredAtUtc,
            "limited listing must still be newest-first");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListRecentAsync_non_positive_limit_returns_empty(int limit)
    {
        await _repository.SaveAsync(NoteAt(DateTime.UtcNow), CancellationToken.None);

        Assert.Empty(await _repository.ListRecentAsync(limit, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_with_null_optionals_round_trips_null_safe()
    {
        var incident = Incident.Create(IncidentId.New(), DateTime.UtcNow, IncidentKind.UnknownFace);

        await _repository.SaveAsync(incident, CancellationToken.None);

        var found = await _repository.FindByIdAsync(incident.Id, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Null(found!.MemberId);
        Assert.Null(found.Note);
        Assert.Null(found.SnapshotImagePath);
        Assert.Equal(IncidentKind.UnknownFace, found.Kind);
    }

    [Fact]
    public async Task SaveAsync_same_id_overwrites_instead_of_duplicating()
    {
        var id = IncidentId.New();
        var baseTime = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        await _repository.SaveAsync(
            Incident.Create(id, baseTime, IncidentKind.UnknownFace, note: "first"), CancellationToken.None);

        await _repository.SaveAsync(
            Incident.Create(id, baseTime.AddHours(1), IncidentKind.AccessDenied, note: "corrected"), CancellationToken.None);

        var listed = await _repository.ListRecentAsync(10, CancellationToken.None);
        var single = Assert.Single(listed);
        Assert.Equal(IncidentKind.AccessDenied, single.Kind);
        Assert.Equal("corrected", single.Note);
    }

    [Fact]
    public void Create_normalizes_timestamp_to_utc_and_blank_optionals_to_null()
    {
        var local = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Local);
        var unspecified = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Unspecified);

        var fromLocal = Incident.Create(IncidentId.New(), local, IncidentKind.ManualNote, note: "  ", snapshotImagePath: "");
        var fromUnspecified = Incident.Create(IncidentId.New(), unspecified, IncidentKind.ManualNote);

        Assert.Equal(DateTimeKind.Utc, fromLocal.OccurredAtUtc.Kind);
        Assert.Equal(local.ToUniversalTime(), fromLocal.OccurredAtUtc);
        Assert.Null(fromLocal.Note);
        Assert.Null(fromLocal.SnapshotImagePath);
        Assert.Equal(DateTimeKind.Utc, fromUnspecified.OccurredAtUtc.Kind);
        Assert.Equal(unspecified.Ticks, fromUnspecified.OccurredAtUtc.Ticks);
    }

    [Fact]
    public void Create_trims_surrounding_whitespace_from_optionals()
    {
        var incident = Incident.Create(
            IncidentId.New(), DateTime.UtcNow, IncidentKind.ManualNote, note: "  Gate left open  ");

        Assert.Equal("Gate left open", incident.Note);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
