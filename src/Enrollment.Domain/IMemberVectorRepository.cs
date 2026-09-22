namespace Enrollment.Domain;

/// <summary>
/// Port for persisting and querying enrolled members' face embeddings. The only
/// Infrastructure adapter is <c>SqliteVecMemberRepository</c> (Phase 4), the sole
/// class referencing the <c>CommunityToolkit.VectorData.SqliteVec</c> preview package.
/// </summary>
public interface IMemberVectorRepository
{
    Task SaveAsync(Member member, CancellationToken cancellationToken);

    /// <summary>Returns the closest enrolled member and its similarity score, or <c>null</c> when none are enrolled.</summary>
    Task<(MemberId Id, double Score)?> FindNearestAsync(FaceEmbedding query, CancellationToken cancellationToken);

    /// <summary>Finds a member by normalized <c>FullName</c> (trim + <see cref="StringComparison.OrdinalIgnoreCase"/>), or <c>null</c> when none matches.</summary>
    Task<Member?> FindByFullNameAsync(string fullName, CancellationToken cancellationToken);
}
