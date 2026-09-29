using Enrollment.PublicApi;
using Microsoft.Extensions.Logging;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.Infrastructure;

/// <summary>A concrete delivery channel tagged with the <see cref="NotificationChannelKind"/> roles and defaults select by.</summary>
public sealed record NotificationChannelRegistration(NotificationChannelKind Kind, INotificationService Service);

/// <summary>
/// Routes an <see cref="AccessAlert"/> only to the channels that apply to it. Only an
/// <see cref="AccessOutcome.Allowed"/> alert with a member is a recognized identity and uses that member's
/// role channels; every other alert (denied, human verification) carries just a nearest-match id, never an
/// identity, so it uses the default channels. Fail-safe: a failing member lookup falls back to the defaults,
/// and a failing default lookup notifies through every channel rather than dropping a security alert.
/// Each channel is isolated in its own try/catch so one failing never prevents the others from firing.
/// </summary>
public sealed class CompositeNotificationService : INotificationService
{
    private readonly IReadOnlyList<NotificationChannelRegistration> _channels;
    private readonly INotificationRoutingReader _routing;
    private readonly ILogger<CompositeNotificationService> _logger;

    public CompositeNotificationService(
        IEnumerable<NotificationChannelRegistration> channels,
        INotificationRoutingReader routing,
        ILogger<CompositeNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(channels);
        _channels = channels.ToList();
        _routing = routing ?? throw new ArgumentNullException(nameof(routing));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var targets = await ResolveTargetsAsync(alert, cancellationToken).ConfigureAwait(false);

        foreach (var channel in _channels.Where(c => targets is null || targets.Contains(c.Kind)))
        {
            try
            {
                await channel.Service.NotifyAsync(alert, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification channel {Channel} failed; other channels still run.", channel.Service.GetType().Name);
            }
        }
    }

    /// <summary>The channel kinds to notify, or <c>null</c> for "every channel" when routing cannot be resolved.</summary>
    private async Task<IReadOnlySet<NotificationChannelKind>?> ResolveTargetsAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        if (alert is { Outcome: AccessOutcome.Allowed, MemberId: { } memberId })
        {
            try
            {
                return await _routing.GetChannelsForMemberAsync(memberId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Member channel lookup failed for {MemberId}; falling back to default channels.", memberId);
            }
        }

        try
        {
            return await _routing.GetDefaultChannelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Default channel lookup failed; notifying through every channel.");
            return null;
        }
    }
}
