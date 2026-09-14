using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// First workflow stage: runs <see cref="IPersonDetector"/> on the ingested image. Per the
/// <c>person-detection</c> spec, zero detected faces MUST NOT be ignored — it synthesizes a
/// terminal <see cref="AccessDecisionResult"/> so the graph's conditional short-circuit edge
/// (wired in <see cref="MonitoringWorkflowHost"/>) routes straight to <see cref="NotifyExecutor"/>,
/// bypassing <see cref="VerifyIdentityExecutor"/>/<see cref="DecideAccessExecutor"/> entirely.
/// One or more detected faces proceed on the normal edge to <see cref="VerifyIdentityExecutor"/>.
/// Emits the <c>Monitoring.DetectPerson</c> span per design.md's Observability table.
/// </summary>
public sealed class DetectPersonExecutor(
    IPersonDetector personDetector,
    [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(DetectPersonExecutor))
{
    private readonly IPersonDetector _personDetector = personDetector ?? throw new ArgumentNullException(nameof(personDetector));
    private readonly ActivitySource _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

    public override async ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        using Activity? activity = _activitySource.StartActivity("Monitoring.DetectPerson");

        var detection = await _personDetector.DetectAsync(input.ImageStream, cancellationToken).ConfigureAwait(false);

        // PersonDetectionResult models exactly one embedding per image (see IPersonDetector.cs's
        // scoping note), so faces.detected.count is approximated as presence (0/1).
        activity?.SetTag("faces.detected.count", detection.PersonPresent ? 1 : 0);
        activity?.SetTag("person.present", detection.PersonPresent);

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
