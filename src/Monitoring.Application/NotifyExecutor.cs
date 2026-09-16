using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monitoring.Domain;
using System.Diagnostics;

namespace Monitoring.Application;

/// <summary>
/// Terminal workflow stage: fires an <see cref="AccessAlert"/> for every finalized outcome (per
/// the <c>access-notification</c> spec), persists a terminal <see cref="PendingVerification"/>
/// record whenever the outcome is <see cref="AccessOutcome.HumanVerificationRequired"/> (per the
/// <c>pending-verification-record</c> spec), and isolates notification failures so they can
/// never crash the workflow run or block subsequent archival (per the <c>access-notification</c>
/// spec's "Notification Failure Isolation" requirement). Emits the <c>Monitoring.Notify</c> span
/// per design.md's Observability table.
///
/// <para>
/// The <c>INotificationService.NotifyAsync</c> call is wrapped in a try/catch exactly as
/// design.md specifies for this executor: any exception is caught, logged at <c>Error</c> level,
/// and swallowed — never rethrown. Archival itself (moving the file to <c>processed/</c> or
/// <c>quarantine/</c>) is driven by the workflow host after this executor yields its output, not
/// by this executor directly.
/// </para>
/// </summary>
public sealed class NotifyExecutor(
    INotificationService notificationService,
    IPendingVerificationRepository pendingVerificationRepository,
    TimeProvider timeProvider,
    ILogger<NotifyExecutor> logger,
    [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    : Executor<MonitoringWorkflowContext, bool>(nameof(NotifyExecutor))
{
    /// <summary>
    /// The only shipped <see cref="INotificationService"/> implementation is a logging placeholder
    /// (design.md's Observability table's literal <c>notification.channel</c> tag value) — not a
    /// real delivery channel (proposal's Out of Scope).
    /// </summary>
    private const string NotificationChannel = "logging";

    private readonly INotificationService _notificationService =
        notificationService ?? throw new ArgumentNullException(nameof(notificationService));

    private readonly IPendingVerificationRepository _pendingVerificationRepository =
        pendingVerificationRepository ?? throw new ArgumentNullException(nameof(pendingVerificationRepository));

    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private readonly ILogger<NotifyExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ActivitySource _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

    public override async ValueTask<bool> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var activity = _activitySource.StartActivity($"{nameof(Monitoring)}.Notify");

        var decision = input.Decision
            ?? throw new InvalidOperationException($"{nameof(NotifyExecutor)} requires a resolved {nameof(AccessDecisionResult)}.");

        activity?.SetTag("outcome", decision.Outcome.ToString());
        activity?.SetTag("notification.channel", NotificationChannel);

        var occurredAtUtc = _timeProvider.GetUtcNow();

        var alert = new AccessAlert(
            input.CandidateMemberId,
            decision.Distance,
            decision.Outcome,
            input.ImageReference,
            occurredAtUtc,
            decision.Reason);

        try
        {
            await _notificationService.NotifyAsync(alert, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification failed for {ImageReference}; workflow run continues.", input.ImageReference);
        }

        if (decision.Outcome == AccessOutcome.HumanVerificationRequired)
        {
            var pendingVerification = PendingVerification.Create(
                PendingVerificationId.New(),
                input.ImageReference,
                decision.Distance,
                input.CandidateMemberId,
                occurredAtUtc);

            await _pendingVerificationRepository.SaveAsync(pendingVerification, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
