using Microsoft.Agents.AI.Workflows;

namespace Monitoring.Application;

/// <summary>
/// Builds the <c>Microsoft.Agents.AI.Workflows</c> graph wiring all four workflow executors, per
/// design.md's Application Flow: <see cref="DetectPersonExecutor"/> has a conditional
/// short-circuit edge straight to <see cref="NotifyExecutor"/> on the zero-face path (bypassing
/// <see cref="VerifyIdentityExecutor"/>/<see cref="DecideAccessExecutor"/> entirely); the normal
/// path proceeds detect → verify → decide → notify. Driving the graph against real ingestion
/// events (running <c>Workflow</c> per validated file, then archiving) is Infrastructure's
/// responsibility (Phase 4), out of scope here — this host only builds the graph shape.
/// </summary>
public sealed class MonitoringWorkflowHost
{
    public MonitoringWorkflowHost(
        DetectPersonExecutor detectPersonExecutor,
        VerifyIdentityExecutor verifyIdentityExecutor,
        DecideAccessExecutor decideAccessExecutor,
        NotifyExecutor notifyExecutor)
    {
        ArgumentNullException.ThrowIfNull(detectPersonExecutor);
        ArgumentNullException.ThrowIfNull(verifyIdentityExecutor);
        ArgumentNullException.ThrowIfNull(decideAccessExecutor);
        ArgumentNullException.ThrowIfNull(notifyExecutor);

        var detect = detectPersonExecutor.BindExecutor();
        var verify = verifyIdentityExecutor.BindExecutor();
        var decide = decideAccessExecutor.BindExecutor();
        var notify = notifyExecutor.BindExecutor();

        Workflow = new WorkflowBuilder(detect)
            .AddEdge<MonitoringWorkflowContext>(detect, verify, ctx => ctx?.Decision is null)
            .AddEdge<MonitoringWorkflowContext>(detect, notify, ctx => ctx?.Decision is not null)
            .AddEdge(verify, decide)
            .AddEdge(decide, notify)
            .WithOutputFrom(notify)
            .Build();
    }

    /// <summary>The built workflow graph, ready for <c>InProcessExecution</c> (Infrastructure, Phase 4) to run per ingested file.</summary>
    public Workflow Workflow { get; }
}
