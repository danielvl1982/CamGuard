namespace Monitoring.Infrastructure;

/// <summary>
/// Configuration for the <see cref="NtfyNotificationService"/> push-notification channel
/// (ntfy.sh — free, no account/API key required for low-volume public topics).
/// </summary>
public sealed class NtfyOptions
{
    /// <summary>Configuration section name this options type binds from (e.g. <c>appsettings.json</c>).</summary>
    public const string SectionName = "Ntfy";

    /// <summary>Base URL of the ntfy server; defaults to the free public instance.</summary>
    public string BaseUrl { get; set; } = "https://ntfy.sh";

    /// <summary>
    /// Topic to publish alerts to. Public ntfy.sh topics are unauthenticated by default, so this
    /// should be an unguessable name, not a real credential. Empty means notifications are not
    /// configured yet — <see cref="NtfyNotificationService"/> fails fast rather than silently
    /// dropping alerts.
    /// </summary>
    public string Topic { get; set; } = string.Empty;
}
