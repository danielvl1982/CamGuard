using Microsoft.Extensions.Logging;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// Logging-only <see cref="INotificationService"/> placeholder, per the <c>access-notification</c>
/// spec — not a real delivery channel (proposal's Out of Scope). Logs the full <see cref="AccessAlert"/>
/// at <c>Information</c> level for every outcome, including <c>Allowed</c>. A thrown exception here
/// is caught and logged by the caller (<c>NotifyExecutor</c>), never left to crash the workflow run.
/// </summary>
public sealed class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    private readonly ILogger<LoggingNotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Access alert: Outcome={Outcome} MemberId={MemberId} Distance={Distance} Image={ImageReference} OccurredAtUtc={OccurredAtUtc} Reason={Reason}",
            alert.Outcome,
            alert.MemberId,
            alert.Distance,
            alert.ImageReference,
            alert.OccurredAtUtc,
            alert.Reason);

        return Task.CompletedTask;
    }
}
