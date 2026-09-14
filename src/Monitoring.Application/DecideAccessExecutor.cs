using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// Third workflow stage: resolves the final <see cref="AccessDecisionResult"/> from the forwarded
/// distance via <see cref="IAccessDecisionPolicy"/>, per the <c>access-decision-policy</c> spec.
/// When <see cref="MonitoringWorkflowContext.Distance"/> is <c>null</c> — <c>IMemberDirectoryReader</c>
/// returned an explicit no-match, not merely a high-distance match — this stage resolves
/// <see cref="AccessOutcome.Denied"/> directly without calling the policy, per the
/// <c>identity-verification</c> spec's "unrecognized/high-distance outcome" treatment; the policy
/// interface itself stays reserved for the two-threshold gray-zone logic exercised by its own
/// unit tests. Emits the <c>Monitoring.Decide</c> span per design.md's Observability table.
/// </summary>
public sealed class DecideAccessExecutor(
    IAccessDecisionPolicy accessDecisionPolicy,
    [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(DecideAccessExecutor))
{
    private readonly IAccessDecisionPolicy _accessDecisionPolicy =
        accessDecisionPolicy ?? throw new ArgumentNullException(nameof(accessDecisionPolicy));

    private readonly ActivitySource _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

    public override ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        using Activity? activity = _activitySource.StartActivity("Monitoring.Decide");

        var decision = input.Distance is { } distance
            ? _accessDecisionPolicy.Decide(distance)
            : new AccessDecisionResult(AccessOutcome.Denied, Distance: null, Reason: "No enrolled member match found");

        activity?.SetTag("outcome", decision.Outcome.ToString());
        activity?.SetTag("distance", decision.Distance);

        return ValueTask.FromResult(input with { Decision = decision });
    }
}
