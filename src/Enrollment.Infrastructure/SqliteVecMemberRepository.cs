using Common;
using CommunityToolkit.VectorData.SqliteVec;
using CSharpFunctionalExtensions;
using Enrollment.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
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

    public async Task<Member?> FindByFullNameAsync(string fullName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var normalized = MemberVectorRecordMapper.Normalize(fullName);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.FindByFullName");
        activity?.SetTag("lookup.fullName", normalized);

        await _collection.EnsureCollectionExistsAsync(cancellationToken);

        var options = new FilteredRecordRetrievalOptions<MemberVectorRecord> { IncludeVectors = true };

        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(MemberVectorRecordMapper.Normalize(record.FullName), normalized, StringComparison.OrdinalIgnoreCase))
            {
                activity?.SetTag("lookup.found", true);
                return MemberVectorRecordMapper.ToMember(record);
            }
        }

        activity?.SetTag("lookup.found", false);
        return null;
    }

    public async Task<IReadOnlyList<Member>> ListAllAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.ListAll");
        var startTimestamp = Stopwatch.GetTimestamp();

        // Required: enumerating a collection that was never created throws VectorStoreException
        // (verified against CommunityToolkit.VectorData.SqliteVec 1.0.1-preview).
        await _collection.EnsureCollectionExistsAsync(cancellationToken);

        // Vectors must be materialized even though a directory only displays identity fields:
        // IncludeVectors=false returns records with a null Embedding, and Member.Rehydrate
        // (via MemberVectorRecordMapper.ToMember) requires a validated FaceEmbedding.
        var options = new FilteredRecordRetrievalOptions<MemberVectorRecord> { IncludeVectors = true };

        var members = new List<Member>();

        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            members.Add(MemberVectorRecordMapper.ToMember(record));
        }

        activity?.SetTag("count", members.Count);
        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

        return members;
    }
}
