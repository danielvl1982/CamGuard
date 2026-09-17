using Microsoft.Extensions.Logging;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// Fans an <see cref="AccessAlert"/> out to every configured real <see cref="INotificationService"/>
/// channel (ntfy, email). Each channel is isolated in its own try/catch so one channel failing
/// (e.g. SMTP misconfigured) never prevents the others from firing — <see cref="NotifyExecutor"/>'s
/// own outer try/catch remains the safety net for the workflow run as a whole.
/// </summary>
public sealed class CompositeNotificationService : INotificationService
{
    private readonly IReadOnlyList<INotificationService> _channels;
    private readonly ILogger<CompositeNotificationService> _logger;

    public CompositeNotificationService(IEnumerable<INotificationService> channels, ILogger<CompositeNotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(channels);
        _channels = channels.ToList();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        foreach (var channel in _channels)
        {
            try
            {
                await channel.NotifyAsync(alert, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification channel {Channel} failed; other channels still run.", channel.GetType().Name);
            }
        }
    }
}
