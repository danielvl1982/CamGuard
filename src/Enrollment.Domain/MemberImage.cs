using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// Read-model for one persisted reference photo of a member (member-crud T2):
/// the image bytes live on disk (see the Infrastructure adapter), while the
/// per-photo <see cref="FaceEmbedding"/> and file location live in the vector
/// store. Images are evidence, not access — they carry no annul semantics and
/// may be stored for annulled members.
/// </summary>
public sealed class MemberImage : EntityBase<MemberImageId>
{
    private MemberImage(MemberImageId id, MemberId memberId, string fileName, FaceEmbedding embedding, DateTime storedAtUtc)
    {
        Id = id;
        MemberId = memberId;
        FileName = fileName;
        Embedding = embedding;
        StoredAtUtc = storedAtUtc;
    }

    public MemberId MemberId { get; }

    /// <summary>File name only (no directory), e.g. <c>&lt;imageId&gt;.jpg</c>.</summary>
    public string FileName { get; }

    /// <summary>The embedding extracted from THIS photo (not the member's primary embedding).</summary>
    public FaceEmbedding Embedding { get; }

    /// <summary>When the image was stored, always UTC.</summary>
    public DateTime StoredAtUtc { get; }

    /// <summary>Reconstitutes a member image from persistence without raising domain events.</summary>
    public static MemberImage Rehydrate(MemberImageId id, MemberId memberId, string fileName, FaceEmbedding embedding, DateTime storedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return new MemberImage(id, memberId, fileName, embedding, storedAtUtc);
    }

    /// <summary>
    /// Creates a member image for a freshly stored photo, stamping it with the
    /// current UTC time. Used by tests and the Infrastructure adapter.
    /// </summary>
    public static Result<MemberImage, MemberImageError> Create(MemberImageId id, MemberId memberId, string fileName, FaceEmbedding embedding)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(embedding);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result.Failure<MemberImage, MemberImageError>(MemberImageError.EmptyFileName);
        }

        return Result.Success<MemberImage, MemberImageError>(
            new MemberImage(id, memberId, fileName, embedding, DateTime.UtcNow));
    }
}

public enum MemberImageError
{
    EmptyFileName
}
