using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>Strongly typed identity for <see cref="Member"/>. Rejects <see cref="Guid.Empty"/>.</summary>
public sealed record MemberId : IComparable<MemberId>
{
    private MemberId(Guid value) => Value = value;

    public Guid Value { get; }

    public int CompareTo(MemberId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <summary>Generates a new, always-valid identity for a member being enrolled.</summary>
    public static MemberId New() => new(Guid.NewGuid());

    /// <summary>Constructs a <see cref="MemberId"/> from an existing value, rejecting <see cref="Guid.Empty"/>.</summary>
    public static Result<MemberId, MemberIdError> From(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<MemberId, MemberIdError>(MemberIdError.Empty)
            : Result.Success<MemberId, MemberIdError>(new MemberId(value));

    public override string ToString() => Value.ToString();
}

public enum MemberIdError
{
    Empty
}
