using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Integration test for <see cref="SqliteVecMemberRepository"/> (task 9.6) — round-trip against a
/// temp SQLite file, not mocked, per design.md's Testing Strategy ("Infrastructure" row) and the
/// <c>member-directory-query</c> spec's "Verified Round-Trip" requirement.
/// </summary>
public sealed class SqliteVecMemberRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;

    public SqliteVecMemberRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-enrollment-test-{Guid.NewGuid():N}.db");
        IOptions<VectorStoreOptions> options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests"));
    }

    [Fact]
    public async Task Enrolled_member_is_retrievable_by_a_query_with_its_own_embedding()
    {
        FaceEmbedding embedding = FaceEmbedding.FromVector(ValidVector(0.8f)).Value;
        Member member = Member.Enroll(MemberId.New(), "Ada Lovelace", embedding).Value;

        await _repository.SaveAsync(member, CancellationToken.None);
        (MemberId Id, double Score)? match = await _repository.FindNearestAsync(embedding, CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(member.Id, match!.Value.Id);
    }

    [Fact]
    public async Task Query_against_an_empty_store_returns_no_match()
    {
        FaceEmbedding embedding = FaceEmbedding.FromVector(ValidVector(0.5f)).Value;

        (MemberId Id, double Score)? match = await _repository.FindNearestAsync(embedding, CancellationToken.None);

        Assert.Null(match);
    }

    private static float[] ValidVector(float firstComponent)
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = firstComponent;
        return vector;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (!File.Exists(_dbPath))
            return;

        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup only — a leftover temp file never fails the test run.
        }
    }
}
