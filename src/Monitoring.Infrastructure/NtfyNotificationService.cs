using System.Text;
using Microsoft.Extensions.Options;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.Infrastructure;

/// <summary>
/// <see cref="INotificationService"/> adapter posting access alerts to a ntfy.sh topic — a free,
/// account-less push-notification channel. Registered as a typed client
/// (<see cref="MonitoringServiceCollectionExtensions.AddMonitoring"/>) so its <see cref="HttpClient"/>
/// always comes from <c>IHttpClientFactory</c>, never <c>new HttpClient()</c>.
/// </summary>
public sealed class NtfyNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly NtfyOptions _options;

    public NtfyNotificationService(HttpClient httpClient, IOptions<NtfyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task NotifyAsync(AccessAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (string.IsNullOrWhiteSpace(_options.Topic))
        {
            throw new InvalidOperationException("Ntfy:Topic is not configured; cannot send a push notification.");
        }

        var uri = new Uri($"{_options.BaseUrl.TrimEnd('/')}/{_options.Topic}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(BuildBody(alert), Encoding.UTF8)
        };
        request.Headers.TryAddWithoutValidation("Title", "CamGuard Access Alert");
        request.Headers.TryAddWithoutValidation("Priority", MapPriority(alert.Outcome));
        request.Headers.TryAddWithoutValidation("Tags", MapTags(alert.Outcome));

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static string MapPriority(AccessOutcome outcome) => outcome switch
    {
        AccessOutcome.Allowed => "default",
        AccessOutcome.Denied => "high",
        AccessOutcome.HumanVerificationRequired => "high",
        _ => "default"
    };

    private static string MapTags(AccessOutcome outcome) => outcome switch
    {
        AccessOutcome.Allowed => "white_check_mark",
        AccessOutcome.Denied => "no_entry",
        AccessOutcome.HumanVerificationRequired => "warning",
        _ => string.Empty
    };

    private static string BuildBody(AccessAlert alert) =>
        $"Outcome: {alert.Outcome}\nMemberId: {alert.MemberId}\nDistance: {alert.Distance}\nImage: {alert.ImageReference}\nOccurredAtUtc: {alert.OccurredAtUtc:O}\nReason: {alert.Reason}";
}
