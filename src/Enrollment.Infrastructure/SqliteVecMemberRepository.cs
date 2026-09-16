using Common;
using CommunityToolkit.VectorData.SqliteVec;
using CSharpFunctionalExtensions;
using Enrollment.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="IMemberVectorRepository"/> adapter wrapping <c>CommunityToolkit.VectorData.SqliteVec</c>
/// — the only class in the solution referencing that preview package, per design.md's rollback
/// plan. Persists/queries <see cref="MemberVectorRecord"/>, mapped from/to <see cref="Member"/> by
/// <see cref="MemberVectorRecordMapper"/> (task 4.5).
/// </summary>
public sealed class SqliteVecMemberRepository : IMemberVectorRepository
{
    private readonly SqliteCollection<string, MemberVectorRecord> _collection;
    private readonly ActivitySource _activitySource;

    public SqliteVecMemberRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _collection = new SqliteCollection<string, MemberVectorRecord>(value.ConnectionString, value.MembersCollectionName);
    }

    public async Task SaveAsync(Member member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.Save");
        var startTimestamp = Stopwatch.GetTimestamp();

        await _collection.EnsureCollectionExistsAsync(cancellationToken);
        await _collection.UpsertAsync(MemberVectorRecordMapper.FromMember(member), cancellationToken);

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }

    public async Task<(MemberId Id, double Score)?> FindNearestAsync(FaceEmbedding query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.Query");
        var startTimestamp = Stopwatch.GetTimestamp();

        await _collection.EnsureCollectionExistsAsync(cancellationToken);

        float[] queryVector = [.. query.Vector];

        await foreach (var result in
            _collection.SearchAsync(queryVector, top: 1, cancellationToken: cancellationToken))
        {
            activity?.SetTag("match.score", result.Score);
            activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            // Defensive: a record whose stored key is not a valid, non-empty Guid should never
            // surface as a match rather than throw — it indicates a corrupted/foreign record.
            var memberId = Guid.TryParse(result.Record.Id, out var parsed)
                ? MemberId.From(parsed)
                : Result.Failure<MemberId, MemberIdError>(MemberIdError.Empty);

            return memberId.IsSuccess ? (memberId.Value, result.Score ?? 0d) : null;
        }

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        return null;
    }
}
