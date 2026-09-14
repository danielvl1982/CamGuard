namespace Monitoring.Domain;

/// <summary>
/// The three deterministic outcomes an access decision can resolve to, per the
/// <c>access-decision-policy</c> spec's "Three-Outcome Deterministic Decision" requirement.
/// </summary>
public enum AccessOutcome
{
    Allowed,
    Denied,
    HumanVerificationRequired
}
