using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// Aggregate root for an authorized member enrolled with exactly one validated
/// face embedding. See <c>member-enrollment</c> spec for enrollment invariants.
/// </summary>
public sealed class Member : EntityBase<MemberId>
{
    private Member(MemberId id, string fullName, FaceEmbedding embedding)
    {
        Id = id;
        FullName = fullName;
        Embedding = embedding;
    }

    public string FullName { get; }

    public FaceEmbedding Embedding { get; }

    /// <summary>Reconstitutes a member from persistence without raising domain events.</summary>
    public static Member Rehydrate(MemberId id, string fullName, FaceEmbedding embedding)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return new Member(id, fullName, embedding);
    }

    /// <summary>
    /// Enrolls a member from identity data and one already-validated face embedding.
    /// Raises <see cref="MemberEnrolled"/> on success.
    /// </summary>
    public static Result<Member, MemberError> Enroll(MemberId id, string fullName, FaceEmbedding embedding)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(embedding);

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<Member, MemberError>(MemberError.EmptyFullName);
        }

        var member = new Member(id, fullName, embedding);
        member.Raise(new MemberEnrolled(id));

        return Result.Success<Member, MemberError>(member);
    }
}

public enum MemberError
{
    EmptyFullName
}
