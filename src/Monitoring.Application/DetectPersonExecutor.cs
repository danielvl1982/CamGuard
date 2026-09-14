using Microsoft.Agents.AI.Workflows;
using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// First workflow stage: runs <see cref="IPersonDetector"/> on the ingested image. Per the
/// <c>person-detection</c> spec, zero detected faces MUST NOT be ignored — it synthesizes a
/// terminal <see cref="AccessDecisionResult"/> so the graph's conditional short-circuit edge
/// (wired in <see cref="MonitoringWorkflowHost"/>) routes straight to <see cref="NotifyExecutor"/>,
/// bypassing <see cref="VerifyIdentityExecutor"/>/<see cref="DecideAccessExecutor"/> entirely.
/// One or more detected faces proceed on the normal edge to <see cref="VerifyIdentityExecutor"/>.
/// </summary>
public sealed class DetectPersonExecutor(IPersonDetector personDetector)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(DetectPersonExecutor))
{
    private readonly IPersonDetector _personDetector = personDetector ?? throw new ArgumentNullException(nameof(personDetector));

    public override async ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var detection = await _personDetector.DetectAsync(input.ImageStream, cancellationToken).ConfigureAwait(false);

        if (!detection.PersonPresent)
        {
            // Zero-face short-circuit: never ignored, never silently dropped — routes directly to
            // NotifyExecutor per the person-detection spec's "Zero-Face Routing" requirement.
            var shortCircuitDecision = new AccessDecisionResult(
                AccessOutcome.HumanVerificationRequired,
                Distance: null,
                Reason: "No face detected");

            return input with { EmbeddingVector = null, Decision = shortCircuitDecision };
        }

        return input with { EmbeddingVector = detection.EmbeddingVector, Decision = null };
    }
}
