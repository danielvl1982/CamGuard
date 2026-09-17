using System.Net;
using Microsoft.Extensions.Options;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>Tests for <see cref="NtfyNotificationService"/>: request shape sent to ntfy, and the empty-topic fail-fast guard.</summary>
public sealed class NtfyNotificationServiceTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static AccessAlert CreateAlert(AccessOutcome outcome) =>
        new(Guid.NewGuid(), 0.4, outcome, "processed/image.png", DateTimeOffset.UtcNow, "test reason");

    [Fact]
    public async Task Posts_to_base_url_and_topic_with_title_and_body()
    {
        var handler = new FakeHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new NtfyOptions { BaseUrl = "https://ntfy.sh", Topic = "camguard-test" });
        var service = new NtfyNotificationService(httpClient, options);

        await service.NotifyAsync(CreateAlert(AccessOutcome.Denied), CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(new Uri("https://ntfy.sh/camguard-test"), handler.LastRequest!.RequestUri);
        Assert.True(handler.LastRequest.Headers.TryGetValues("Title", out _));
        Assert.True(handler.LastRequest.Headers.TryGetValues("Priority", out _));
        Assert.Contains("Denied", handler.LastRequestBody);
    }

    [Fact]
    public async Task Throws_when_topic_is_not_configured()
    {
        using var httpClient = new HttpClient(new FakeHttpMessageHandler());
        var options = Options.Create(new NtfyOptions { BaseUrl = "https://ntfy.sh", Topic = "" });
        var service = new NtfyNotificationService(httpClient, options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.NotifyAsync(CreateAlert(AccessOutcome.Allowed), CancellationToken.None));
    }
}
