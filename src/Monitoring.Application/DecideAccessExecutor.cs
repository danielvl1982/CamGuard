using Microsoft.Agents.AI.Workflows;
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
/// unit tests.
/// </summary>
public sealed class DecideAccessExecutor(IAccessDecisionPolicy accessDecisionPolicy)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(DecideAccessExecutor))
{
    private readonly IAccessDecisionPolicy _accessDecisionPolicy =
        accessDecisionPolicy ?? throw new ArgumentNullException(nameof(accessDecisionPolicy));

    public override ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var decision = input.Distance is { } distance
            ? _accessDecisionPolicy.Decide(distance)
            : new AccessDecisionResult(AccessOutcome.Denied, Distance: null, Reason: "No enrolled member match found");

        return ValueTask.FromResult(input with { Decision = decision });
    }
}
