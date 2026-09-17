namespace Monitoring.Infrastructure;

/// <summary>
/// Configuration for the <see cref="EmailNotificationService"/> SMTP channel.
/// <see cref="Password"/> must never be given a real value here — supply it via
/// <c>dotnet user-secrets</c> in development or an environment variable in production.
/// </summary>
public sealed class EmailNotificationOptions
{
    /// <summary>Configuration section name this options type binds from (e.g. <c>appsettings.json</c>).</summary>
    public const string SectionName = "EmailNotification";

    /// <summary>SMTP server host (e.g. <c>smtp.gmail.com</c> for a free Gmail account).</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP port; 587 is the standard STARTTLS submission port.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Sender mailbox address.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Recipient mailbox address that receives access alerts.</summary>
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>SMTP authentication username; empty skips authentication.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>SMTP authentication password/app-password — never commit a real value.</summary>
    public string Password { get; set; } = string.Empty;
}
