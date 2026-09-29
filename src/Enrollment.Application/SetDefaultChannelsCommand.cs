using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Replaces the default notification channels (empty = no notification for denied/unknown accesses).</summary>
public sealed record SetDefaultChannelsCommand(IReadOnlyCollection<NotificationChannel> Channels);

/// <summary>Rejects undefined channel values, then persists the new default set.</summary>
public sealed class SetDefaultChannelsCommandHandler(INotificationSettingsRepository settingsRepository)
{
    public async Task<UnitResult<SetDefaultChannelsError>> HandleAsync(
        SetDefaultChannelsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Channels);

        if (command.Channels.Any(c => !Enum.IsDefined(c)))
        {
            return UnitResult.Failure(SetDefaultChannelsError.InvalidChannel);
        }

        await settingsRepository.SetDefaultChannelsAsync(command.Channels, cancellationToken).ConfigureAwait(false);

        return UnitResult.Success<SetDefaultChannelsError>();
    }
}

public enum SetDefaultChannelsError
{
    InvalidChannel
}
