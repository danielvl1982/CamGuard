using Microsoft.Extensions.Options;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Validation-only tests for <see cref="EmailNotificationService"/> — real SMTP delivery is out of
/// scope for a unit test; this only proves the fail-fast guard on incomplete configuration.
/// </summary>
public sealed class EmailNotificationServiceTests
{
    private static AccessAlert CreateAlert() =>
        new(Guid.NewGuid(), 0.2, AccessOutcome.Allowed, "processed/image.png", DateTimeOffset.UtcNow, "reason");

    [Theory]
    [InlineData("", "from@example.com", "to@example.com")]
    [InlineData("smtp.example.com", "", "to@example.com")]
    [InlineData("smtp.example.com", "from@example.com", "")]
    public async Task Throws_when_a_required_option_is_missing(string smtpHost, string fromAddress, string toAddress)
    {
        var options = Options.Create(new EmailNotificationOptions
        {
            SmtpHost = smtpHost,
            FromAddress = fromAddress,
            ToAddress = toAddress,
        });
        var service = new EmailNotificationService(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.NotifyAsync(CreateAlert(), CancellationToken.None));
    }
}
