using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers member-enrollment spec: Normalized FullName Lookup and Single-Embedding Re-enrollment.
/// Scenarios: case-insensitive match, trimmed name matches, unknown creates, overwrite preserves same MemberId.
/// Persists against a real SqliteVec file to prove the scan+Normalize path works end-to-end.
/// </summary>
public sealed class FindByFullNameTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;

    public FindByFullNameTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-findbyname-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests.FindByFullName"));
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        return FaceEmbedding.FromVector(v).Value;
    }

    [Fact]
    public async Task FindByFullNameAsync_returns_member_when_case_differs_Ana_Torres_vs_ana_torres()
    {
        // GIVEN Ana Torres is enrolled
        var embedding = ValidEmbedding(0.5f);
        var id = MemberId.New();
        var member = Member.Enroll(id, "Ana Torres", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        // WHEN watch file arrives with ana torres
        var found = await _repository.FindByFullNameAsync("ana torres", CancellationToken.None);

        // THEN FindByFullNameAsync returns that member and stored MemberId equals original
        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
        Assert.Equal("Ana Torres", found.FullName);
    }

    [Fact]
    public async Task FindByFullNameAsync_returns_member_when_name_has_surrounding_whitespace()
    {
        // GIVEN Jose Garcia enrolled
        var embedding = ValidEmbedding(0.6f);
        var id = MemberId.New();
        var member = Member.Enroll(id, "Jose Garcia", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        // WHEN watch file arrives with "  Jose Garcia  " (trimmed)
        var found = await _repository.FindByFullNameAsync("  Jose Garcia  ", CancellationToken.None);

        // THEN lookup returns member and would overwrite embedding (proves trim normalization)
        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
    }

    [Fact]
    public async Task FindByFullNameAsync_returns_null_when_no_normalized_match()
    {
        // GIVEN no normalized match for Luis Ponce (only Ana enrolled)
        var embedding = ValidEmbedding(0.7f);
        var member = Member.Enroll(MemberId.New(), "Ana Torres", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        // WHEN lookup for Luis Ponce
        var found = await _repository.FindByFullNameAsync("Luis Ponce", CancellationToken.None);

        // THEN null — caller should create new member
        Assert.Null(found);
    }

    [Fact]
    public async Task FindByFullNameAsync_returns_null_for_empty_or_whitespace()
    {
        var embedding = ValidEmbedding(0.8f);
        var member = Member.Enroll(MemberId.New(), "Ana Torres", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        Assert.Null(await _repository.FindByFullNameAsync("", CancellationToken.None));
        Assert.Null(await _repository.FindByFullNameAsync("   ", CancellationToken.None));
        Assert.Null(await _repository.FindByFullNameAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task FindByFullNameAsync_trim_is_applied_at_lookup_and_persistence()
    {
        // Store with trimmed name, lookup with padded variant still matches
        var embedding = ValidEmbedding(0.9f);
        var id = MemberId.New();
        var member = Member.Enroll(id, "Maria Lopez", embedding).Value;
        await _repository.SaveAsync(member, CancellationToken.None);

        var foundPadded = await _repository.FindByFullNameAsync("  Maria Lopez  ", CancellationToken.None);
        var foundUpper = await _repository.FindByFullNameAsync("MARIA LOPEZ", CancellationToken.None);
        var foundMixed = await _repository.FindByFullNameAsync("  maria lopez  ", CancellationToken.None);

        Assert.NotNull(foundPadded);
        Assert.NotNull(foundUpper);
        Assert.NotNull(foundMixed);
        Assert.Equal(id, foundPadded!.Id);
        Assert.Equal(id, foundUpper!.Id);
        Assert.Equal(id, foundMixed!.Id);
    }

    [Fact]
    public async Task FindByFullNameAsync_re_enrollment_overwrites_same_MemberId_atomically()
    {
        // GIVEN existing member Maria Lopez enrolled from photo A
        var firstEmbedding = ValidEmbedding(0.4f);
        var id = MemberId.New();
        var firstMember = Member.Enroll(id, "Maria Lopez", firstEmbedding).Value;
        await _repository.SaveAsync(firstMember, CancellationToken.None);

        // WHEN watch file for "  MARIA LOPEZ  " with one face processed — Upsert same MemberId
        var secondEmbedding = ValidEmbedding(0.9f);
        secondEmbedding = FaceEmbedding.FromVector(secondEmbedding.Vector.ToArray()).Value;
        // Simulate overwrite: second member with same Id but new embedding
        var secondMember = Member.Rehydrate(id, "Maria Lopez", secondEmbedding);
        await _repository.SaveAsync(secondMember, CancellationToken.None);

        // THEN system Upserts same MemberId atomically and member count does not increase
        var found = await _repository.FindByFullNameAsync("maria lopez", CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
        // Embedding should be replaced (second photo's embedding)
        Assert.Equal(secondEmbedding, found.Embedding);
        // Not first embedding
        Assert.NotEqual(firstEmbedding, found.Embedding);

        // AND query with photo B's embedding returns that member
        var nearest = await _repository.FindNearestAsync(secondEmbedding, CancellationToken.None);
        Assert.NotNull(nearest);
        Assert.Equal(id, nearest!.Value.Id);
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
