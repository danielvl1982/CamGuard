namespace Enrollment.Domain;

/// <summary>Port for the persisted default notification channels used for denied/unknown accesses.</summary>
public interface INotificationSettingsRepository
{
    /// <summary>
    /// Returns the stored default channels. When nothing was ever stored, returns every channel
    /// (today's behavior); an explicitly stored empty set means "no notification".
    /// </summary>
    Task<IReadOnlySet<NotificationChannel>> GetDefaultChannelsAsync(CancellationToken cancellationToken);

    /// <summary>Atomically replaces the default channels. Throws <see cref="ArgumentException"/> on an undefined channel.</summary>
    Task SetDefaultChannelsAsync(IReadOnlyCollection<NotificationChannel> channels, CancellationToken cancellationToken);
}
