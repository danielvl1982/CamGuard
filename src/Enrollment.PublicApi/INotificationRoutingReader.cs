namespace Enrollment.PublicApi;

/// <summary>
/// Read-only surface Monitoring uses to learn which notification channels apply to an alert:
/// the channels of a recognized member's role, or the configured default set. Exposes its own
/// <see cref="NotificationChannelKind"/> so consumers never reference Enrollment's Domain.
/// </summary>
public interface INotificationRoutingReader
{
    /// <summary>
    /// Channels of the member's role. A member that is unknown, annulled-and-missing, or has no
    /// (or a dangling) role resolves to the default channels instead. An empty set means "notify nobody".
    /// </summary>
    Task<IReadOnlySet<NotificationChannelKind>> GetChannelsForMemberAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>The persisted default channels used for denied/unknown alerts. An empty set means "notify nobody".</summary>
    Task<IReadOnlySet<NotificationChannelKind>> GetDefaultChannelsAsync(CancellationToken cancellationToken);
}

/// <summary>Boundary mirror of Enrollment's notification channel enum.</summary>
public enum NotificationChannelKind
{
    Push,
    Email
}
