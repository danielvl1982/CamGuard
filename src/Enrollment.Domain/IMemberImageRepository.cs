namespace Enrollment.Domain;

/// <summary>
/// Port for persisting members' reference-photo bytes plus their per-photo
/// embeddings (member-crud T2). The Infrastructure adapter stores files under
/// the configured image root and per-image records in a dedicated vector
/// collection. Images are evidence, not access: saving images for an annulled
/// member is allowed, so implementations must NOT gate on annul state.
/// </summary>
public interface IMemberImageRepository
{
    /// <summary>
    /// Stores one photo's bytes plus its embedding and returns the new image id.
    /// Copies <paramref name="imageBytes"/> without disposing it; the caller owns
    /// disposal. <paramref name="fileExtension"/> includes the leading dot
    /// (e.g. <c>.jpg</c>); blank values fall back to <c>.bin</c>.
    /// </summary>
    Task<MemberImageId> SaveAsync(
        MemberId memberId,
        Stream imageBytes,
        FaceEmbedding embedding,
        string fileExtension,
        CancellationToken cancellationToken);

    /// <summary>Returns every stored image of <paramref name="memberId"/> with vectors materialized.</summary>
    Task<IReadOnlyList<MemberImage>> ListByMemberAsync(MemberId memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns every stored image across ALL members with vectors materialized.
    /// Query surface for member-crud T3 (duplicate-face validation matches a new
    /// embedding against other members' per-image embeddings, excluding the same
    /// member id client-side).
    /// </summary>
    Task<IReadOnlyList<MemberImage>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an image's record AND its file — both or nothing. A file-delete
    /// failure surfaces (the record is left intact, never an orphan) instead of
    /// failing silently. Throws <see cref="KeyNotFoundException"/> when no image
    /// with <paramref name="imageId"/> exists.
    /// </summary>
    Task DeleteAsync(MemberImageId imageId, CancellationToken cancellationToken);
}
