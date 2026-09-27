using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>Strongly typed identity for a stored <see cref="MemberImage"/>. Rejects <see cref="Guid.Empty"/>.</summary>
public sealed record MemberImageId : IComparable<MemberImageId>
{
    private MemberImageId(Guid value) => Value = value;

    public Guid Value { get; }

    public int CompareTo(MemberImageId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <summary>Generates a new, always-valid identity for a member image being stored.</summary>
    public static MemberImageId New() => new(Guid.NewGuid());

    /// <summary>Constructs a <see cref="MemberImageId"/> from an existing value, rejecting <see cref="Guid.Empty"/>.</summary>
    public static Result<MemberImageId, MemberImageIdError> From(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<MemberImageId, MemberImageIdError>(MemberImageIdError.Empty)
            : Result.Success<MemberImageId, MemberImageIdError>(new MemberImageId(value));

    public override string ToString() => Value.ToString();
}

public enum MemberImageIdError
{
    Empty
}
