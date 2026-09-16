using Microsoft.Extensions.Options;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.Infrastructure;

/// <summary>
/// Single deterministic <see cref="IAccessDecisionPolicy"/> implementation, per the
/// <c>access-decision-policy</c> spec: <see cref="AccessDecisionOptions.AllowedMaxDistance"/> and
/// <see cref="AccessDecisionOptions.DeniedMinDistance"/> partition the distance range into exactly
/// three outcomes. One deterministic class, not a Strategy — no repeated-branch growth exists
/// (design.md "Ports and Adapters"; exploration.md's decision gate).
/// </summary>
public sealed class AccessDecisionPolicy : IAccessDecisionPolicy
{
    private readonly AccessDecisionOptions _options;

    public AccessDecisionPolicy(IOptions<AccessDecisionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public AccessDecisionResult Decide(double distance)
    {
        if (distance <= _options.AllowedMaxDistance)
        {
            return new AccessDecisionResult(AccessOutcome.Allowed, distance, "Distance at or below AllowedMaxDistance");
        }

        if (distance >= _options.DeniedMinDistance)
        {
            return new AccessDecisionResult(AccessOutcome.Denied, distance, "Distance at or above DeniedMinDistance");
        }

        return new AccessDecisionResult(AccessOutcome.HumanVerificationRequired, distance, "Distance strictly between configured thresholds");
    }
}
