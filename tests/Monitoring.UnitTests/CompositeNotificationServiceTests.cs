using Enrollment.PublicApi;
using Microsoft.Extensions.Logging;
using Moq;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>Tests for <see cref="CompositeNotificationService"/>: per-alert channel routing, fail-safe fallbacks, and per-channel failure isolation.</summary>
public sealed class CompositeNotificationServiceTests
{
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly Mock<INotificationService> _push = new();
    private readonly Mock<INotificationService> _email = new();
    private readonly Mock<INotificationRoutingReader> _routing = new();
    private readonly Mock<ILogger<CompositeNotificationService>> _logger = new();

    private CompositeNotificationService CreateSut() => new(
        [
            new NotificationChannelRegistration(NotificationChannelKind.Push, _push.Object),
            new NotificationChannelRegistration(NotificationChannelKind.Email, _email.Object),
        ],
        _routing.Object,
        _logger.Object);

    private static AccessAlert Alert(AccessOutcome outcome, Guid? memberId) =>
        new(memberId, 0.3, outcome, "processed/image.png", DateTimeOffset.UtcNow, "reason");

    private void SetupMember(params NotificationChannelKind[] kinds) =>
        _routing.Setup(x => x.GetChannelsForMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kinds.ToHashSet());

    private void SetupDefault(params NotificationChannelKind[] kinds) =>
        _routing.Setup(x => x.GetDefaultChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(kinds.ToHashSet());

    private static void VerifySent(Mock<INotificationService> channel, Times times) =>
        channel.Verify(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()), times);

    private void VerifyLogged(LogLevel level, Times times) =>
        _logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Fact]
    public async Task Recognized_member_is_notified_only_through_the_role_channels()
    {
        SetupMember(NotificationChannelKind.Email);

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Allowed, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Never());
        VerifySent(_email, Times.Once());
    }

    [Fact]
    public async Task Recognized_member_whose_role_has_no_channels_sends_nothing()
    {
        SetupMember();
        SetupDefault(NotificationChannelKind.Push, NotificationChannelKind.Email);

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Allowed, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Never());
        VerifySent(_email, Times.Never());
    }

    [Fact]
    public async Task Recognized_member_without_a_role_uses_whatever_the_reader_resolves()
    {
        // The reader resolves "no role" to the default set; the composite honors what it returns.
        SetupMember(NotificationChannelKind.Push);

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Allowed, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Once());
        VerifySent(_email, Times.Never());
    }

    [Theory]
    [InlineData(AccessOutcome.Denied)]
    [InlineData(AccessOutcome.HumanVerificationRequired)]
    public async Task Non_allowed_outcome_uses_the_default_channels_even_when_the_candidate_member_has_a_role(AccessOutcome outcome)
    {
        SetupMember(NotificationChannelKind.Email);
        SetupDefault(NotificationChannelKind.Push);

        await CreateSut().NotifyAsync(Alert(outcome, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Once());
        VerifySent(_email, Times.Never());
        _routing.Verify(x => x.GetChannelsForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Alert_without_a_member_uses_the_default_channels()
    {
        SetupDefault(NotificationChannelKind.Email);

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Denied, null), CancellationToken.None);

        VerifySent(_push, Times.Never());
        VerifySent(_email, Times.Once());
    }

    [Fact]
    public async Task Empty_default_channels_send_nothing()
    {
        SetupDefault();

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Denied, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Never());
        VerifySent(_email, Times.Never());
    }

    [Fact]
    public async Task Member_lookup_failure_falls_back_to_the_default_channels_and_logs_a_warning()
    {
        _routing.Setup(x => x.GetChannelsForMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        SetupDefault(NotificationChannelKind.Push);

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Allowed, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Once());
        VerifySent(_email, Times.Never());
        VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Default_lookup_failure_notifies_through_every_channel_and_logs_a_warning()
    {
        _routing.Setup(x => x.GetDefaultChannelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Denied, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Once());
        VerifySent(_email, Times.Once());
        VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Member_and_default_lookup_failures_notify_through_every_channel()
    {
        _routing.Setup(x => x.GetChannelsForMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        _routing.Setup(x => x.GetDefaultChannelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Allowed, MemberId), CancellationToken.None);

        VerifySent(_push, Times.Once());
        VerifySent(_email, Times.Once());
        VerifyLogged(LogLevel.Warning, Times.Exactly(2));
    }

    [Fact]
    public async Task One_channel_failure_does_not_block_the_others()
    {
        SetupDefault(NotificationChannelKind.Push, NotificationChannelKind.Email);
        _push.Setup(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated channel failure"));

        await CreateSut().NotifyAsync(Alert(AccessOutcome.Denied, MemberId), CancellationToken.None);

        VerifySent(_email, Times.Once());
        VerifyLogged(LogLevel.Error, Times.Once());
    }
}
