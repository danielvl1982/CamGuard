using Microsoft.Extensions.Logging;
using Moq;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>Tests for <see cref="CompositeNotificationService"/>: every channel fires, and one channel's failure never blocks the others.</summary>
public sealed class CompositeNotificationServiceTests
{
    private static AccessAlert CreateAlert() =>
        new(Guid.NewGuid(), 0.3, AccessOutcome.Allowed, "processed/image.png", DateTimeOffset.UtcNow, "reason");

    [Fact]
    public async Task Calls_every_channel()
    {
        var channelA = new Mock<INotificationService>();
        var channelB = new Mock<INotificationService>();
        var composite = new CompositeNotificationService(
            [channelA.Object, channelB.Object],
            new Mock<ILogger<CompositeNotificationService>>().Object);

        await composite.NotifyAsync(CreateAlert(), CancellationToken.None);

        channelA.Verify(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()), Times.Once);
        channelB.Verify(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task One_channel_failure_does_not_block_the_others()
    {
        var failing = new Mock<INotificationService>();
        failing.Setup(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated channel failure"));
        var succeeding = new Mock<INotificationService>();
        var logger = new Mock<ILogger<CompositeNotificationService>>();
        var composite = new CompositeNotificationService([failing.Object, succeeding.Object], logger.Object);

        await composite.NotifyAsync(CreateAlert(), CancellationToken.None);

        succeeding.Verify(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()), Times.Once);
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
