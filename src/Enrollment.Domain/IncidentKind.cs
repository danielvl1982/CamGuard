namespace Enrollment.Domain;

/// <summary>
/// What happened in a recorded <see cref="Incident"/> (incident-log OI2).
/// The first three variants are written by the automatic capture hook (T3);
/// <see cref="ManualNote"/> is written by the operator form (T4).
/// </summary>
public enum IncidentKind
{
    /// <summary>A face at the door matched no enrolled member.</summary>
    UnknownFace,

    /// <summary>Entry was denied to a detected face.</summary>
    AccessDenied,

    /// <summary>An annulled member attempted to enter.</summary>
    AnnulledAttempt,

    /// <summary>An operator-written note with no automatic trigger.</summary>
    ManualNote
}
