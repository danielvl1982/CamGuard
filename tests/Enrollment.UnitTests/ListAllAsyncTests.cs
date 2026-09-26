using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers the <c>ListAllAsync</c> port added for the Members directory page (enrollment-web-ui T2).
/// Scenarios: empty store returns an empty list, every enrolled member is returned, re-enrollment
/// upserts instead of duplicating, and returned members round-trip with valid embeddings.
/// Persists against a real SqliteVec file to prove the full-enumeration path works end-to-end.
/// </summary>
public sealed class ListAllAsyncTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;

    public ListAllAsyncTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-listall-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests.ListAll"));
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        return FaceEmbedding.FromVector(v).Value;
    }

    [Fact]
    public async Task ListAllAsync_returns_empty_list_when_no_members_are_enrolled()
    {
        // GIVEN a fresh store with no saved members
        // WHEN listing all members
        var members = await _repository.ListAllAsync(CancellationToken.None);

        // THEN the list is empty (no VectorStoreException on a never-created collection)
        Assert.NotNull(members);
        Assert.Empty(members);
    }

    [Fact]
    public async Task ListAllAsync_returns_all_enrolled_members()
    {
        // GIVEN three members enrolled
        var ana = Member.Enroll(MemberId.New(), "Ana Torres", ValidEmbedding(0.5f)).Value;
        var jose = Member.Enroll(MemberId.New(), "Jose Garcia", ValidEmbedding(0.6f)).Value;
        var maria = Member.Enroll(MemberId.New(), "Maria Lopez", ValidEmbedding(0.7f)).Value;
        await _repository.SaveAsync(ana, CancellationToken.None);
        await _repository.SaveAsync(jose, CancellationToken.None);
        await _repository.SaveAsync(maria, CancellationToken.None);

        // WHEN listing all members
        var members = await _repository.ListAllAsync(CancellationToken.None);

        // THEN every enrolled member is returned with its original MemberId and FullName
        Assert.Equal(3, members.Count);
        var byId = members.ToDictionary(m => m.Id);
        Assert.Equal("Ana Torres", byId[ana.Id].FullName);
        Assert.Equal("Jose Garcia", byId[jose.Id].FullName);
        Assert.Equal("Maria Lopez", byId[maria.Id].FullName);
    }

    [Fact]
    public async Task ListAllAsync_returns_single_entry_after_re_enrollment_upsert()
    {
        // GIVEN Maria Lopez enrolled once
        var id = MemberId.New();
        await _repository.SaveAsync(Member.Enroll(id, "Maria Lopez", ValidEmbedding(0.4f)).Value, CancellationToken.None);

        // WHEN the same member re-enrolls (Upsert on the same MemberId key)
        var secondEmbedding = ValidEmbedding(0.9f);
        await _repository.SaveAsync(Member.Rehydrate(id, "Maria Lopez", secondEmbedding), CancellationToken.None);

        // THEN the listing still has exactly one entry for that MemberId with the new embedding
        var members = await _repository.ListAllAsync(CancellationToken.None);
        var single = Assert.Single(members);
        Assert.Equal(id, single.Id);
        Assert.Equal(secondEmbedding, single.Embedding);
    }

    [Fact]
    public async Task ListAllAsync_returns_members_with_valid_embeddings()
    {
        // GIVEN a member enrolled with a known embedding
        var embedding = ValidEmbedding(0.85f);
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        // WHEN listing all members
        var members = await _repository.ListAllAsync(CancellationToken.None);

        // THEN the returned member round-trips with its embedding intact (IncludeVectors path)
        var listed = Assert.Single(members);
        Assert.Equal(member.Id, listed.Id);
        Assert.Equal("Ada Lovelace", listed.FullName);
        Assert.Equal(embedding, listed.Embedding);
    }

    [Fact]
    public async Task ListAllAsync_reflects_members_persisted_beforehand_and_stays_stable_across_calls()
    {
        // GIVEN two members enrolled
        await _repository.SaveAsync(Member.Enroll(MemberId.New(), "Ana Torres", ValidEmbedding(0.5f)).Value, CancellationToken.None);
        await _repository.SaveAsync(Member.Enroll(MemberId.New(), "Luis Ponce", ValidEmbedding(0.6f)).Value, CancellationToken.None);

        // WHEN listing twice
        var first = await _repository.ListAllAsync(CancellationToken.None);
        var second = await _repository.ListAllAsync(CancellationToken.None);

        // THEN both calls return the same set of members
        Assert.Equal(2, first.Count);
        Assert.Equal(
            first.Select(m => m.Id).OrderBy(id => id.Value),
            second.Select(m => m.Id).OrderBy(id => id.Value));
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
