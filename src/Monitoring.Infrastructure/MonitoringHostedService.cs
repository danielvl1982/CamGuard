using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// Drives the folder-watch → workflow → archive loop for the process lifetime — the production
/// host design.md deferred as a follow-up (see <c>MonitoringWorkflowHost</c>'s doc comment and the
/// <c>monitoring-pipeline</c> proposal's Out of Scope list). Each ingested file is processed inside
/// its own DI scope: <see cref="MonitoringWorkflowHost"/> and its four executors are registered
/// Scoped (not Singleton) to avoid capturing <c>Enrollment.PublicApi.IMemberDirectoryReader</c>'s
/// own Scoped lifetime as a captive dependency — see
/// <see cref="MonitoringServiceCollectionExtensions"/>'s registration comment.
/// </summary>
public sealed class MonitoringHostedService(
    IIngestionSource ingestionSource,
    IServiceScopeFactory scopeFactory,
    ILogger<MonitoringHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var path in ingestionSource.WatchAsync(stoppingToken).ConfigureAwait(false))
        {
            await ProcessFileAsync(path, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessFileAsync(string path, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var archiver = scope.ServiceProvider.GetRequiredService<IImageArchiver>();

        try
        {
            var workflowHost = scope.ServiceProvider.GetRequiredService<MonitoringWorkflowHost>();

            bool completed;
            await using (var imageStream = File.OpenRead(path))
            {
                var input = new MonitoringWorkflowContext(path, imageStream, null, null, null, null);
                await using var run = await InProcessExecution.RunAsync(workflowHost.Workflow, input, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                completed = run.OutgoingEvents.OfType<WorkflowOutputEvent>().Single().As<bool>();
            } // stream closed before archival — File.Move requires an unlocked file

            if (completed)
            {
                await archiver.MoveToProcessedAsync(path, cancellationToken).ConfigureAwait(false);
                return;
            }

            logger.LogWarning("Workflow run for {Path} did not complete; moving to quarantine.", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One file's failure must never stop the watch loop for the rest — log and quarantine
            // instead of letting the exception propagate out of ExecuteAsync's foreach.
            logger.LogError(ex, "Workflow run failed for {Path}; moving to quarantine.", path);
        }

        try
        {
            await archiver.MoveToQuarantineAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Quarantining itself is best-effort — the file may already be gone/locked; the watch
            // loop must keep running regardless.
            logger.LogError(ex, "Failed to move {Path} to quarantine after a failed workflow run.", path);
        }
    }
}
