namespace Monitoring.Infrastructure;

/// <summary>
/// Configuration for <see cref="AccessDecisionPolicy"/>'s two named distance thresholds, per the
/// <c>access-decision-policy</c> spec. Bound from configuration via <c>AddMonitoring(...)</c>
/// (design.md "DI / Composition Root") and validated at options-binding time so an invalid
/// threshold pair (<see cref="AllowedMaxDistance"/> &gt;= <see cref="DeniedMinDistance"/>) fails
/// fast instead of silently producing wrong access outcomes.
/// </summary>
public sealed class AccessDecisionOptions
{
    /// <summary>Configuration section name this options type binds from (e.g. <c>appsettings.json</c>).</summary>
    public const string SectionName = "AccessDecision";

    /// <summary>
    /// Maximum cosine distance (inclusive) still resolved as <see cref="Monitoring.Domain.AccessOutcome.Allowed"/>.
    /// Named "distance", never "score"/"similarity" — a lower value means a closer match.
    /// </summary>
    public double AllowedMaxDistance { get; set; } = 0.35;

    /// <summary>
    /// Minimum cosine distance (inclusive) resolved as <see cref="Monitoring.Domain.AccessOutcome.Denied"/>.
    /// A distance strictly between the two thresholds resolves <c>HumanVerificationRequired</c>.
    /// </summary>
    public double DeniedMinDistance { get; set; } = 0.55;
}
