using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// Port for the deterministic access decision made from a forwarded cosine-distance value, per
/// the <c>access-decision-policy</c> spec. The only implementation is <c>AccessDecisionPolicy</c>
/// (Infrastructure, <c>AccessDecisionOptions</c>-driven).
/// </summary>
public interface IAccessDecisionPolicy
{
    AccessDecisionResult Decide(double distance);
}
