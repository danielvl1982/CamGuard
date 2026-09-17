using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// <see cref="INotificationService"/> adapter sending access alerts by SMTP (e.g. a free Gmail
/// account with an app password) using MailKit, per STARTTLS on <see cref="EmailNotificationOptions.SmtpPort"/>.
/// </summary>
public sealed class EmailNotificationService : INotificationService
{
    private readonly EmailNotificationOptions _options;

    public EmailNotificationService(IOptions<EmailNotificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (string.IsNullOrWhiteSpace(_options.SmtpHost)
            || string.IsNullOrWhiteSpace(_options.FromAddress)
            || string.IsNullOrWhiteSpace(_options.ToAddress))
        {
            throw new InvalidOperationException(
                "EmailNotification options are incomplete; SmtpHost, FromAddress and ToAddress are required.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.FromAddress));
        message.To.Add(MailboxAddress.Parse(_options.ToAddress));
        message.Subject = $"CamGuard access alert: {alert.Outcome}";
        message.Body = new TextPart(TextFormat.Plain) { Text = BuildBody(alert) };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildBody(AccessAlert alert) =>
        $"Outcome: {alert.Outcome}\nMemberId: {alert.MemberId}\nDistance: {alert.Distance}\nImage: {alert.ImageReference}\nOccurredAtUtc: {alert.OccurredAtUtc:O}\nReason: {alert.Reason}";
}
