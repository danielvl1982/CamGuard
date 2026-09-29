using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>Strongly typed identity for <see cref="Role"/>. Rejects <see cref="Guid.Empty"/>.</summary>
public sealed record RoleId : IComparable<RoleId>
{
    private RoleId(Guid value) => Value = value;

    public Guid Value { get; }

    public int CompareTo(RoleId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <summary>Generates a new, always-valid identity for a role being created.</summary>
    public static RoleId New() => new(Guid.NewGuid());

    /// <summary>Constructs a <see cref="RoleId"/> from an existing value, rejecting <see cref="Guid.Empty"/>.</summary>
    public static Result<RoleId, RoleIdError> From(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<RoleId, RoleIdError>(RoleIdError.Empty)
            : Result.Success<RoleId, RoleIdError>(new RoleId(value));

    public override string ToString() => Value.ToString();
}

public enum RoleIdError
{
    Empty
}
