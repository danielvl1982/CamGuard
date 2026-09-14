using System.Diagnostics;
using Enrollment.PublicApi;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Monitoring.Application;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Folder-watch → workflow → decision integration-style test, per design.md's Testability
/// section: a real temp watch-folder root, a real <see cref="FileSystemWatchIngestionSource"/> and
/// <see cref="FileSystemImageArchiver"/>, but a mocked <see cref="IPersonDetector"/>/
/// <see cref="IMemberDirectoryReader"/> — no real FaceAiSharp/ONNX or Enrollment call is needed
/// here; that's already covered by PR2's manual smoke test and Enrollment's own test suite.
///
/// <para>
/// No production "ingestion runner" wiring exists yet (per PR2's apply-progress: design.md's
/// Solution Layout has no Host/Worker project and no task requested one) — Infrastructure's own
/// pieces (<see cref="FileSystemWatchIngestionSource"/>, <see cref="MonitoringWorkflowHost"/>,
/// <see cref="FileSystemImageArchiver"/>) are proven correct individually elsewhere, so this test
/// drives the same watch → run-workflow → archive loop a future host would, exactly as design.md's
/// own Testability section describes it structurally.
/// </para>
/// </summary>
public sealed class MonitoringWorkflowIntegrationTests
{
    private static readonly string FixtureImagePath = Path.Combine(AppContext.BaseDirectory, "testdata", "blank-image.png");

    [Fact]
    public async Task Dropped_file_is_archived_and_notified_exactly_once()
    {
        string watchRoot = Directory.CreateTempSubdirectory("camguard-integration-").FullName;
        try
        {
            var watchFolderOptions = Options.Create(new WatchFolderOptions
            {
                Path = watchRoot,
                StabilityRetryDelayMilliseconds = 20,
                MaxStabilityRetryAttempts = 25,
            });

            using var activitySource = new ActivitySource("Test.Monitoring.Integration");

            var personDetectorMock = new Mock<IPersonDetector>();
            personDetectorMock
                .Setup(x => x.DetectAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PersonDetectionResult(true, [0.1f, 0.2f, 0.3f]));

            var memberDirectoryReaderMock = new Mock<IMemberDirectoryReader>();
            memberDirectoryReaderMock
                .Setup(x => x.FindBestMatchAsync(It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberMatch(Guid.NewGuid(), 0.1)); // well below the default AllowedMaxDistance

            var notificationServiceMock = new Mock<INotificationService>();
            notificationServiceMock
                .Setup(x => x.NotifyAsync(It.IsAny<AccessAlert>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var pendingVerificationRepositoryMock = new Mock<IPendingVerificationRepository>();
            var accessDecisionPolicy = new AccessDecisionPolicy(Options.Create(new AccessDecisionOptions()));

            var detect = new DetectPersonExecutor(personDetectorMock.Object, activitySource);
            var verify = new VerifyIdentityExecutor(memberDirectoryReaderMock.Object, activitySource);
            var decide = new DecideAccessExecutor(accessDecisionPolicy, activitySource);
            var notify = new NotifyExecutor(
                notificationServiceMock.Object,
                pendingVerificationRepositoryMock.Object,
                TimeProvider.System,
                NullLogger<NotifyExecutor>.Instance,
                activitySource);
            var host = new MonitoringWorkflowHost(detect, verify, decide, notify);

            using var ingestionSource = new FileSystemWatchIngestionSource(
                watchFolderOptions, NullLogger<FileSystemWatchIngestionSource>.Instance, activitySource);
            var archiver = new FileSystemImageArchiver(watchFolderOptions);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using IAsyncEnumerator<string> enumerator = ingestionSource.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            Task<bool> moveNextTask = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(200); // let the watcher attach before the fixture is copied in
            string droppedPath = Path.Combine(watchRoot, "dropped.png");
            File.Copy(FixtureImagePath, droppedPath);

            Assert.True(await moveNextTask);
            string validatedPath = enumerator.Current;
            Assert.Equal(droppedPath, validatedPath);

            bool workflowCompleted;
            await using (FileStream imageStream = File.OpenRead(validatedPath))
            {
                var input = new MonitoringWorkflowContext(validatedPath, imageStream, null, null, null, null);
                await using Run run = await InProcessExecution.RunAsync(host.Workflow, input, cancellationToken: cts.Token);
                workflowCompleted = run.OutgoingEvents.OfType<WorkflowOutputEvent>().Single().As<bool>();
            } // stream closed before archival — File.Move requires an unlocked file

            Assert.True(workflowCompleted);

            string archivedPath = await archiver.MoveToProcessedAsync(validatedPath, CancellationToken.None);

            Assert.False(File.Exists(droppedPath));
            Assert.True(File.Exists(archivedPath));
            Assert.Equal(Path.Combine(watchRoot, "processed", "dropped.png"), archivedPath);

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
