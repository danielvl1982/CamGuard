using System.Diagnostics;
using Common;
using CommunityToolkit.VectorData.SqliteVec;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers member-crud T1 (soft delete / "anulado"): domain annul state, repository
/// annul round-trip, list exclusion, recognition exclusion (OD1), and legacy rows
/// (created before the annulled column existed) reading as non-annulled.
/// Persists against real SqliteVec files to prove the migration path end-to-end.
/// </summary>
public sealed class MemberAnnulTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;

    public MemberAnnulTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-annul-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests.Annul"));
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f, float second = 0f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        v[1] = second;
        return FaceEmbedding.FromVector(v).Value;
    }

    [Fact]
    public void Enroll_starts_non_annulled()
    {
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;

        Assert.False(member.IsAnnulled);
    }

    [Fact]
    public void Annul_sets_flag_and_is_idempotent()
    {
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;

        member.Annul();

        Assert.True(member.IsAnnulled);
        member.Annul();
        Assert.True(member.IsAnnulled);
    }

    [Fact]
    public void Rehydrate_defaults_to_non_annulled_but_preserves_explicit_flag()
    {
        var id = MemberId.New();
        var embedding = ValidEmbedding();

        Assert.False(Member.Rehydrate(id, "Ada Lovelace", embedding).IsAnnulled);
        Assert.True(Member.Rehydrate(id, "Ada Lovelace", embedding, isAnnulled: true).IsAnnulled);
    }

    [Fact]
    public async Task SetAnnulledAsync_round_trip_persists_flag_and_restore_returns_member_to_list()
    {
        // GIVEN an enrolled member
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        // WHEN annulled via the repository
        await _repository.SetAnnulledAsync(member.Id, true, CancellationToken.None);

        // THEN the flag survives a full persistence round-trip (FindByFullName is unchanged by T1)
        var annulled = await _repository.FindByFullNameAsync("Ada Lovelace", CancellationToken.None);
        Assert.NotNull(annulled);
        Assert.True(annulled!.IsAnnulled);

        // WHEN restored via the repository
        await _repository.SetAnnulledAsync(member.Id, false, CancellationToken.None);

        // THEN the flag clears and the member is listed again
        var restored = await _repository.FindByFullNameAsync("Ada Lovelace", CancellationToken.None);
        Assert.NotNull(restored);
        Assert.False(restored!.IsAnnulled);
        var listed = await _repository.ListAllAsync(CancellationToken.None);
        Assert.Contains(listed, m => m.Id == member.Id);
    }

    [Fact]
    public async Task SetAnnulledAsync_unknown_id_throws_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _repository.SetAnnulledAsync(MemberId.New(), true, CancellationToken.None));
    }

    [Fact]
    public async Task RenameAsync_persists_new_name_and_preserves_embedding_and_annulled_flag()
    {
        // GIVEN an annulled member with a distinctive embedding
        var embedding = ValidEmbedding(0.3f, 0.4f);
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);
        await _repository.SetAnnulledAsync(member.Id, true, CancellationToken.None);

        // WHEN renamed via the repository
        await _repository.RenameAsync(member.Id, "Grace Hopper", CancellationToken.None);

        // THEN the name changed, embedding and annulled flag are untouched, and the old name is gone
        var renamed = await _repository.FindByIdAsync(member.Id, CancellationToken.None);
        Assert.NotNull(renamed);
        Assert.Equal("Grace Hopper", renamed!.FullName);
        Assert.True(renamed.IsAnnulled);
        Assert.Equal(embedding.Vector.ToArray(), renamed.Embedding.Vector.ToArray());
        Assert.Null(await _repository.FindByFullNameAsync("Ada Lovelace", CancellationToken.None));
    }

    [Fact]
    public async Task RenameAsync_unknown_id_throws_KeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _repository.RenameAsync(MemberId.New(), "Grace Hopper", CancellationToken.None));
    }

    [Fact]
    public async Task RenameAsync_blank_name_throws_and_keeps_stored_name()
    {
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _repository.RenameAsync(member.Id, "  ", CancellationToken.None));

        var stored = await _repository.FindByIdAsync(member.Id, CancellationToken.None);
        Assert.Equal("Ada Lovelace", stored!.FullName);
    }

    [Fact]
    public async Task ListAllAsync_excludes_annulled_members_but_keeps_active_ones()
    {
        // GIVEN one active and one annulled member
        var active = Member.Enroll(MemberId.New(), "Active Member", ValidEmbedding(0.5f)).Value;
        var gone = Member.Enroll(MemberId.New(), "Gone Member", ValidEmbedding(0.6f)).Value;
        await _repository.SaveAsync(active, CancellationToken.None);
        await _repository.SaveAsync(gone, CancellationToken.None);
        await _repository.SetAnnulledAsync(gone.Id, true, CancellationToken.None);

        // WHEN listing
        var members = await _repository.ListAllAsync(CancellationToken.None);

        // THEN only the active member is returned
        var single = Assert.Single(members);
        Assert.Equal(active.Id, single.Id);
    }

    [Fact]
    public async Task FindNearestAsync_skips_annulled_top1_and_returns_nearest_active_member()
    {
        // GIVEN an annulled member whose embedding EXACTLY matches the query
        // (cosine distance 0 — it is unambiguously the top-1 candidate) plus an
        // active member with an orthogonal embedding (cosine distance 1)
        var annulledEmbedding = ValidEmbedding(first: 0.8f);
        var annulled = Member.Enroll(MemberId.New(), "Gone Member", annulledEmbedding).Value;
        var active = Member.Enroll(MemberId.New(), "Active Member", ValidEmbedding(first: 0f, second: 0.8f)).Value;
        await _repository.SaveAsync(annulled, CancellationToken.None);
        await _repository.SaveAsync(active, CancellationToken.None);
        await _repository.SetAnnulledAsync(annulled.Id, true, CancellationToken.None);

        // WHEN recognizing with the annulled member's own embedding
        var match = await _repository.FindNearestAsync(annulledEmbedding, CancellationToken.None);

        // THEN the annulled top-1 is skipped and the active member is returned (OD1)
        Assert.NotNull(match);
        Assert.Equal(active.Id, match!.Value.Id);
    }

    [Fact]
    public async Task FindNearestAsync_returns_null_when_every_member_is_annulled()
    {
        // GIVEN the only enrolled member is annulled
        var embedding = ValidEmbedding(0.8f);
        var member = Member.Enroll(MemberId.New(), "Gone Member", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);
        await _repository.SetAnnulledAsync(member.Id, true, CancellationToken.None);

        // WHEN recognizing with its own embedding
        var match = await _repository.FindNearestAsync(embedding, CancellationToken.None);

        // THEN no match is returned (fail-safe: recognition denies)
        Assert.Null(match);
    }

    /// <summary>Pre-annulment row shape: no annulled column (member-crud T1 legacy case).</summary>
    private sealed class LegacyMemberRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = "";
        [VectorStoreData]
        public string FullName { get; set; } = "";
        [VectorStoreVector(FaceEmbedding.ExpectedDimension, DistanceFunction = DistanceFunction.CosineDistance)]
        public float[] Embedding { get; set; } = new float[FaceEmbedding.ExpectedDimension];
    }

    [Fact]
    public async Task Legacy_rows_without_annulled_column_read_as_non_annulled()
    {
        // GIVEN a row written with the pre-T1 shape (no IsAnnulled column)
        var legacyCollection = new SqliteCollection<string, LegacyMemberRecord>(
            $"Data Source={_dbPath}", "members_test");
        await legacyCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        var id = MemberId.New();
        var embedding = ValidEmbedding(0.7f);
        await legacyCollection.UpsertAsync(new LegacyMemberRecord
        {
            Id = id.Value.ToString(),
            FullName = "Legacy Member",
            Embedding = [.. embedding.Vector]
        }, CancellationToken.None);

        // WHEN the repository (which migrates the table on first touch) reads it
        var members = await _repository.ListAllAsync(CancellationToken.None);

        // THEN the legacy member lists as non-annulled and stays recognizable
        var legacy = Assert.Single(members);
        Assert.Equal(id, legacy.Id);
        Assert.Equal("Legacy Member", legacy.FullName);
        Assert.False(legacy.IsAnnulled);
        var match = await _repository.FindNearestAsync(embedding, CancellationToken.None);
        Assert.NotNull(match);
        Assert.Equal(id, match!.Value.Id);
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
