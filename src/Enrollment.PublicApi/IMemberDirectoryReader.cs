namespace Enrollment.PublicApi;

/// <summary>
/// The only surface Monitoring (deferred `monitoring-pipeline` change) may reference to query the
/// enrolled-member directory — per <c>member-directory-query</c> spec's "Public API Boundary"
/// requirement. Consumers never need Enrollment's internal persistence, vector store, or Domain
/// value objects.
/// </summary>
public interface IMemberDirectoryReader
{
    Task<MemberMatch?> FindBestMatchAsync(float[] embeddingVector, CancellationToken cancellationToken);
}

/// <summary>Boundary DTO: the closest enrolled member found and its similarity score, or <c>null</c> for no match.</summary>
public sealed record MemberMatch(Guid MemberId, double Score);
