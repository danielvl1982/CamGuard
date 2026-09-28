using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// Aggregate root for an authorized member enrolled with exactly one validated
/// face embedding. See <c>member-enrollment</c> spec for enrollment invariants.
/// </summary>
public sealed class Member : Entity<MemberId>
{
    private Member(MemberId id, string fullName, FaceEmbedding embedding, bool isAnnulled)
    {
        Id = id;
        FullName = fullName;
        Embedding = embedding;
        IsAnnulled = isAnnulled;
    }

    public string FullName { get; }

    public FaceEmbedding Embedding { get; }

    /// <summary>
    /// Soft-delete flag ("anulado", member-crud T1): annulled members are hidden from
    /// the directory list and excluded from recognition (OD1).
    /// </summary>
    public bool IsAnnulled { get; private set; }

    /// <summary>Soft-deletes the member. Idempotent — re-annulling changes nothing.</summary>
    public void Annul() => IsAnnulled = true;

    /// <summary>
    /// Reconstitutes a member from persistence without raising domain events.
    /// <paramref name="isAnnulled"/> defaults to <c>false</c> so callers reading
    /// pre-annulment rows (no persisted flag) rehydrate as non-annulled.
    /// </summary>
    public static Member Rehydrate(MemberId id, string fullName, FaceEmbedding embedding, bool isAnnulled = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return new Member(id, fullName, embedding, isAnnulled);
    }

    /// <summary>Enrolls a member from identity data and one already-validated face embedding.</summary>
    public static Result<Member, MemberError> Enroll(MemberId id, string fullName, FaceEmbedding embedding)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(embedding);

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<Member, MemberError>(MemberError.EmptyFullName);
        }

        var member = new Member(id, fullName, embedding, isAnnulled: false);

        return Result.Success<Member, MemberError>(member);
    }
}

public enum MemberError
{
    EmptyFullName
}
