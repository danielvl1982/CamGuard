using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Queries the directory for the closest enrolled member to a face embedding.</summary>
public sealed record QueryByEmbeddingQuery(float[] EmbeddingVector);

/// <summary>
/// Validates the query embedding, then deterministically returns the closest
/// enrolled member (or none) — per <c>member-directory-query</c> spec.
/// </summary>
public sealed class QueryByEmbeddingQueryHandler(IMemberVectorRepository memberVectorRepository)
{
    public async Task<Result<(MemberId Id, double Score)?, QueryByEmbeddingError>> HandleAsync(
        QueryByEmbeddingQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var embedding = FaceEmbedding.FromVector(query.EmbeddingVector);
        if (embedding.IsFailure)
        {
            return Result.Failure<(MemberId Id, double Score)?, QueryByEmbeddingError>(QueryByEmbeddingError.InvalidEmbedding);
        }

        var match = await memberVectorRepository.FindNearestAsync(embedding.Value, cancellationToken);

        return Result.Success<(MemberId Id, double Score)?, QueryByEmbeddingError>(match);
    }
}

public enum QueryByEmbeddingError
{
    InvalidEmbedding
}