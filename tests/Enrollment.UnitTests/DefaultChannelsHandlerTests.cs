using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for the default notification channels use cases.</summary>
public sealed class DefaultChannelsHandlerTests
{
    [Fact]
    public async Task Get_returns_the_repository_channels()
    {
        var mocker = new AutoMocker();
        IReadOnlySet<NotificationChannel> channels = new HashSet<NotificationChannel> { NotificationChannel.Push };
        mocker.GetMock<INotificationSettingsRepository>()
            .Setup(x => x.GetDefaultChannelsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(channels);

        var result = await mocker.CreateInstance<GetDefaultChannelsQueryHandler>()
            .HandleAsync(new GetDefaultChannelsQuery(), CancellationToken.None);

        Assert.Same(channels, result);
    }

    [Fact]
    public async Task Set_persists_valid_channels()
    {
        var mocker = new AutoMocker();
        NotificationChannel[] channels = [NotificationChannel.Email];

        var result = await mocker.CreateInstance<SetDefaultChannelsCommandHandler>()
            .HandleAsync(new SetDefaultChannelsCommand(channels), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<INotificationSettingsRepository>()
            .Verify(x => x.SetDefaultChannelsAsync(channels, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_rejects_an_undefined_channel_without_persisting()
    {
        var mocker = new AutoMocker();

        var result = await mocker.CreateInstance<SetDefaultChannelsCommandHandler>()
            .HandleAsync(new SetDefaultChannelsCommand([(NotificationChannel)99]), CancellationToken.None);

        Assert.Equal(SetDefaultChannelsError.InvalidChannel, result.Error);
        mocker.GetMock<INotificationSettingsRepository>().VerifyNoOtherCalls();
    }
}
