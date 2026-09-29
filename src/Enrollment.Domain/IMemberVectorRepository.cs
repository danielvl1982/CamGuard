namespace Enrollment.Domain;

/// <summary>
/// Port for persisting and querying enrolled members' face embeddings. The only
/// Infrastructure adapter is <c>SqliteVecMemberRepository</c> (Phase 4), the sole
/// class referencing the <c>CommunityToolkit.VectorData.SqliteVec</c> preview package.
/// </summary>
public interface IMemberVectorRepository
{
    Task SaveAsync(Member member, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the soft-delete ("anulado") flag on an enrolled member (member-crud T1).
    /// <c>true</c> hides the member from <see cref="ListAllAsync"/> and from recognition
    /// (<see cref="FindNearestAsync"/>, OD1); <c>false</c> restores it.
    /// Throws <see cref="KeyNotFoundException"/> when no member with <paramref name="id"/> exists.
    /// </summary>
    Task SetAnnulledAsync(MemberId id, bool isAnnulled, CancellationToken cancellationToken);

    /// <summary>
    /// Changes an enrolled member's <c>FullName</c>, preserving its embedding and annulled flag.
    /// Throws <see cref="KeyNotFoundException"/> when no member with <paramref name="id"/> exists and
    /// <see cref="ArgumentException"/> when <paramref name="fullName"/> is blank.
    /// </summary>
    Task RenameAsync(MemberId id, string fullName, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns the member to a role (or clears it with <c>null</c>), preserving its embedding, name and
    /// annulled flag. Throws <see cref="KeyNotFoundException"/> when no member with <paramref name="id"/> exists.
    /// The write is conditional on the role still existing (checked in the same statement), so it never
    /// stores a dangling id: a role deleted in the meantime yields <see cref="RoleAssignmentResult.RoleNotFound"/>
    /// and changes nothing.
    /// </summary>
    Task<RoleAssignmentResult> SetRoleAsync(MemberId id, RoleId? roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the closest <b>non-annulled</b> enrolled member and its similarity score,
    /// or <c>null</c> when none are enrolled (or all are annulled). Annulled members are
    /// never returned (OD1: recognition must not grant them access).
    /// </summary>
    Task<(MemberId Id, double Score)?> FindNearestAsync(FaceEmbedding query, CancellationToken cancellationToken);

    /// <summary>Finds a member by normalized <c>FullName</c> (trim + <see cref="StringComparison.OrdinalIgnoreCase"/>), or <c>null</c> when none matches.</summary>
    Task<Member?> FindByFullNameAsync(string fullName, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the member with the given id, or <c>null</c> when unknown. Unlike
    /// <see cref="ListAllAsync"/>, annulled members ARE returned: the duplicate-face
    /// gate (member-crud T3, OD2) matches against annulled members' images, so the
    /// handler needs their display names to report the match.
    /// </summary>
    Task<Member?> FindByIdAsync(MemberId id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns every <b>non-annulled</b> enrolled member in storage order (order is not
    /// guaranteed), or an empty list when none are enrolled.
    /// </summary>
    Task<IReadOnlyList<Member>> ListAllAsync(CancellationToken cancellationToken);
}

public enum RoleAssignmentResult
{
    Assigned,
    RoleNotFound
}
