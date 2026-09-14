using Enrollment.Application;
using Enrollment.Domain;

namespace Enrollment.PublicApi;

/// <summary>
/// Delegates to <see cref="QueryByEmbeddingQueryHandler"/> and maps its internal
/// <c>(MemberId, double)?</c> result to the boundary DTO <see cref="MemberMatch"/>, or
/// <c>null</c> for no-match/failure — the only translation between Enrollment's internals and
/// the public surface (task 6.2).
/// </summary>
public sealed class MemberDirectoryReader(QueryByEmbeddingQueryHandler queryHandler) : IMemberDirectoryReader
{
    public async Task<MemberMatch?> FindBestMatchAsync(float[] embeddingVector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(embeddingVector);

        var query = new QueryByEmbeddingQuery(embeddingVector);
        var result = await queryHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure || result.Value is null)
            return null;

        (MemberId id, double score) = result.Value.Value;
        return new MemberMatch(id.Value, score);
    }
}
