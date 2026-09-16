using Common;
using Enrollment.PublicApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Drives the real <see cref="MonitoringHostedService"/> — the production watch → workflow →
/// archive loop — through a DI container wired the same way <c>AddMonitoring(...)</c> wires it
/// (Scoped <see cref="MonitoringWorkflowHost"/>/executors, resolved per file through
/// <see cref="IServiceScopeFactory"/>), with <see cref="IPersonDetector"/>/
/// <see cref="IMemberDirectoryReader"/> mocked exactly as
/// <c>MonitoringWorkflowIntegrationTests</c> mocks them — no real FaceAiSharp/ONNX or Enrollment
/// call is needed here.
/// </summary>
public sealed class MonitoringHostedServiceTests
{
    private static readonly string FixtureImagePath = Path.Combine(AppContext.BaseDirectory, "testdata", "blank-image.png");

    [Fact]
    public async Task Dropped_file_is_processed_archived_and_notified_by_the_hosted_service()
    {
        var watchRoot = Directory.CreateTempSubdirectory("camguard-host-").FullName;
        try
        {
            var notificationServiceMock = new Mock<INotificationService>();
            notificationServiceMock
                .Setup(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddNamedObservabilitySource(MonitoringObservability.ActivitySourceName);
            services.AddSingleton(TimeProvider.System);

            services.AddSingleton(Options.Create(new WatchFolderOptions
            {
                Path = watchRoot,
                StabilityRetryDelayMilliseconds = 20,
                MaxStabilityRetryAttempts = 25,
            }));
            services.AddSingleton(Options.Create(new AccessDecisionOptions()));

            var personDetectorMock = new Mock<IPersonDetector>();
            personDetectorMock
                .Setup(x => x.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PersonDetectionResult(true, [0.1f, 0.2f, 0.3f]));
            services.AddSingleton(personDetectorMock.Object);

            var memberDirectoryReaderMock = new Mock<IMemberDirectoryReader>();
            memberDirectoryReaderMock
                .Setup(x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberMatch(Guid.NewGuid(), 0.1)); // well below the default AllowedMaxDistance
            services.AddSingleton(memberDirectoryReaderMock.Object);

            services.AddSingleton(notificationServiceMock.Object);
            services.AddSingleton(new Mock<IPendingVerificationRepository>().Object);

            services.AddSingleton<IAccessDecisionPolicy, AccessDecisionPolicy>();
            services.AddSingleton<IImageArchiver, FileSystemImageArchiver>();
            services.AddSingleton<IIngestionSource, FileSystemWatchIngestionSource>();

            services.AddScoped<DetectPersonExecutor>();
            services.AddScoped<VerifyIdentityExecutor>();
            services.AddScoped<DecideAccessExecutor>();
            services.AddScoped<NotifyExecutor>();
            services.AddScoped<MonitoringWorkflowHost>();

            await using var provider = services.BuildServiceProvider(validateScopes: true);

            var hostedService = new MonitoringHostedService(
                provider.GetRequiredService<IIngestionSource>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<MonitoringHostedService>.Instance);

            await hostedService.StartAsync(CancellationToken.None);
            try
            {
                await Task.Delay(200); // let the watcher attach before the fixture is copied in
                var droppedPath = Path.Combine(watchRoot, "dropped.png");
                File.Copy(FixtureImagePath, droppedPath);

                var processedPath = Path.Combine(watchRoot, "processed", "dropped.png");
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!File.Exists(processedPath) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(50);
                }

                Assert.True(File.Exists(processedPath), "Dropped file was never archived to processed/.");
                Assert.False(File.Exists(droppedPath));
            }
            finally
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            notificationServiceMock.Verify(
                x => x.NotifyAsync(It.Is<AccessAlert>(a => a.Outcome == AccessOutcome.Allowed), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(watchRoot, recursive: true);
        }
    }
}
