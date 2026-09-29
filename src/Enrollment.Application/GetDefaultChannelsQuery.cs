using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Reads the default notification channels used for denied/unknown accesses.</summary>
public sealed record GetDefaultChannelsQuery;

public sealed class GetDefaultChannelsQueryHandler(INotificationSettingsRepository settingsRepository)
{
    public Task<IReadOnlySet<NotificationChannel>> HandleAsync(GetDefaultChannelsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return settingsRepository.GetDefaultChannelsAsync(cancellationToken);
    }
}
