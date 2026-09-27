using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>Strongly typed identity for a recorded <see cref="Incident"/>. Rejects <see cref="Guid.Empty"/>.</summary>
public sealed record IncidentId : IComparable<IncidentId>
{
    private IncidentId(Guid value) => Value = value;

    public Guid Value { get; }

    public int CompareTo(IncidentId? other) => other is null ? 1 : Value.CompareTo(other.Value);

    /// <summary>Generates a new, always-valid identity for an incident being recorded.</summary>
    public static IncidentId New() => new(Guid.NewGuid());

    /// <summary>Constructs an <see cref="IncidentId"/> from an existing value, rejecting <see cref="Guid.Empty"/>.</summary>
    public static Result<IncidentId, IncidentIdError> From(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<IncidentId, IncidentIdError>(IncidentIdError.Empty)
            : Result.Success<IncidentId, IncidentIdError>(new IncidentId(value));

    public override string ToString() => Value.ToString();
}

public enum IncidentIdError
{
    Empty
}
