using Enrollment.Domain;

namespace Enrollment.PublicApi;

/// <summary>
/// Resolves alert channels from Enrollment's member, role and default-settings repositories and
/// maps the domain <see cref="NotificationChannel"/> to the boundary <see cref="NotificationChannelKind"/>.
/// Repository failures propagate: the caller (Monitoring) owns the fail-safe fallback.
/// </summary>
public sealed class NotificationRoutingReader(
    IMemberVectorRepository members,
    IRoleRepository roles,
    INotificationSettingsRepository settings) : INotificationRoutingReader
{
    public async Task<IReadOnlySet<NotificationChannelKind>> GetChannelsForMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var id = MemberId.From(memberId);
        var member = id.IsSuccess ? await members.FindByIdAsync(id.Value, cancellationToken) : null;
        if (member?.RoleId is { } roleId)
        {
            var role = await roles.FindByIdAsync(roleId, cancellationToken);
            if (role is not null)
            {
                return Map(role.Channels);
            }
        }

        return await GetDefaultChannelsAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<NotificationChannelKind>> GetDefaultChannelsAsync(CancellationToken cancellationToken) =>
        Map(await settings.GetDefaultChannelsAsync(cancellationToken));

    private static IReadOnlySet<NotificationChannelKind> Map(IEnumerable<NotificationChannel> channels) =>
        channels.Select(c => c switch
        {
            NotificationChannel.Push => NotificationChannelKind.Push,
            NotificationChannel.Email => NotificationChannelKind.Email,
            _ => throw new ArgumentOutOfRangeException(nameof(channels), c, "Unknown notification channel."),
        }).ToHashSet();
}
