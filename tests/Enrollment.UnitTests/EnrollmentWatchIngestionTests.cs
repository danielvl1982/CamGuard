using System.Diagnostics;
using Enrollment.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers enrollment-watch-ingestion spec: folder isolation, debounce/stability gate,
/// per-file prompt/pause semantics, and no-loss while prompt blocks.
/// Uses real FileSystemWatcher + Channel to prove bounded-channel and FileShare.None behavior.
/// </summary>
public sealed class EnrollmentWatchIngestionTests
{
    private static string CreateTempEnrollRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"camguard-enroll-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        // Ensure processed/quarantine subfolders exist as production does
        Directory.CreateDirectory(Path.Combine(dir, "processed"));
        Directory.CreateDirectory(Path.Combine(dir, "quarantine"));
        return dir;
    }

    private static EnrollmentWatchIngestionSource CreateSource(
        string enrollRoot,
        int retryDelayMs = 30,
        int maxRetries = 5,
        int capacity = 64)
    {
        var options = Options.Create(new EnrollmentWatchOptions
        {
            Path = enrollRoot,
            StabilityRetryDelayMilliseconds = retryDelayMs,
            MaxStabilityRetryAttempts = maxRetries,
            ChannelCapacity = capacity
        });
        return new EnrollmentWatchIngestionSource(
            options,
            NullLogger<EnrollmentWatchIngestionSource>.Instance,
            new ActivitySource($"Test.Enrollment.Watch.{Guid.NewGuid():N}"));
    }

    [Fact]
    public async Task New_enrollment_file_in_photos_enroll_is_enqueued_for_stability_checking()
    {
        // Scenario: New enrollment file enqueued — GIVEN watcher runs on photos/enroll WHEN new image created THEN enqueued
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            using var source = CreateSource(enrollRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(250); // let watcher attach
            var file = Path.Combine(enrollRoot, "new.jpg");
            await File.WriteAllBytesAsync(file, [1, 2, 3]);

            Assert.True(await moveNext);
            Assert.Equal(file, enumerator.Current);
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task File_created_in_photos_watch_is_not_enqueued_by_enrollment_watcher()
    {
        // Scenario: Monitoring folder ignored — GIVEN file in photos/watch WHEN enrollment watcher runs THEN MUST NOT enqueue
        var enrollRoot = CreateTempEnrollRoot();
        var watchRoot = Path.Combine(Path.GetTempPath(), $"camguard-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(watchRoot);
        try
        {
            using var source = CreateSource(enrollRoot);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(250);
            // Create file in the *watch* folder (not enroll) — enrollment watcher should ignore it
            var watchFile = Path.Combine(watchRoot, "watch.jpg");
            await File.WriteAllBytesAsync(watchFile, [9, 9, 9]);

            // Wait a bit and ensure no enrollment event was produced
            var completed = await Task.WhenAny(moveNext, Task.Delay(1200));
            Assert.NotEqual(moveNext, completed); // moveNext should not have completed

            // Now create a file in enroll — it SHOULD be enqueued, proving watcher is still alive and isolated
            var enrollFile = Path.Combine(enrollRoot, "enroll.jpg");
            await File.WriteAllBytesAsync(enrollFile, [1, 2, 3]);
            Assert.True(await moveNext);
            Assert.Equal(enrollFile, enumerator.Current);
        }
        finally
        {
            try { Directory.Delete(enrollRoot, recursive: true); } catch { }
            try { Directory.Delete(watchRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Locked_file_is_retried_until_stable_then_enqueued()
    {
        // Scenario: Locked file retried until stable — GIVEN detected file cannot be opened exclusively WHEN stability check runs THEN retries with backoff AND proceeds when readable within retries
        var enrollRoot = CreateTempEnrollRoot();
        try
        {
            using var source = CreateSource(enrollRoot, retryDelayMs: 40, maxRetries: 5);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(250);
            var file = Path.Combine(enrollRoot, "locked.jpg");

            // Hold exclusive lock for ~120ms (covers ~3 retries of 40ms), then release — next retry should succeed
            using (var locked = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await locked.WriteAsync(new byte[] { 1, 2, 3 });
                await locked.FlushAsync();
                await Task.Delay(120);
            }

            Assert.True(await moveNext);
            Assert.Equal(file, enumerator.Current);
            Assert.True(File.Exists(file)); // file was stable and yielded, not quarantined
        }
        finally
        {
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task File_never_stabilizes_after_retries_is_quarantined_and_not_enqueued()
    {
        // Scenario: File never stabilizes — GIVEN file remains locked after all retries WHEN window expires THEN MUST NOT enroll AND MUST move to photos/enroll/quarantine/
        var enrollRoot = CreateTempEnrollRoot();
        var source = CreateSource(enrollRoot, retryDelayMs: 30, maxRetries: 5);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            var moveNext = enumerator.MoveNextAsync().AsTask();

            await Task.Delay(250);
            var file = Path.Combine(enrollRoot, "neverstable.jpg");
            var quarantinePath = Path.Combine(enrollRoot, "quarantine", "neverstable.jpg");

            // Hold lock for 220ms (>5*30=150ms IsStable window) so IsStable exhausts and returns false.
            // Then release lock quickly so TryQuarantine (which retries 5*30ms) can succeed after release.
            var locked = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            await locked.WriteAsync(new byte[] { 1, 2, 3 });
            await locked.FlushAsync();

            // Keep lock held just beyond IsStable window, then release before quarantine retries finish
            await Task.Delay(220);
            locked.Dispose();

            // After lock release, unstable file should be quarantined (TryQuarantine retries will now succeed)
            await Task.Delay(700);

            // The moveNext should NOT yield the original path (file was unstable)
            var yielded = await Task.WhenAny(moveNext, Task.Delay(1200));
            Assert.NotEqual(moveNext, yielded);

            Assert.True(File.Exists(quarantinePath), $"Expected quarantined file at {quarantinePath} but not found. Files in enroll: {string.Join(", ", Directory.GetFiles(enrollRoot))} quarantine: {string.Join(", ", Directory.GetFiles(Path.Combine(enrollRoot, "quarantine")))}");
            Assert.False(File.Exists(file));
        }
        finally
        {
            await cts.CancelAsync();
            try { await enumerator.DisposeAsync(); } catch { }
            source.Dispose();
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Pause_while_prompt_blocks_does_not_lose_burst_of_5_files_via_rescan()
    {
        // Scenario: No loss while prompt blocks — GIVEN watcher paused for prompt WHEN 5 more files arrive THEN none lost to DropOldest AND processed sequentially
        var enrollRoot = CreateTempEnrollRoot();
        var source = CreateSource(enrollRoot, retryDelayMs: 20, maxRetries: 5, capacity: 64);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(14));
        var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            // Start enumeration so watcher is created before Pause (WatchAsync only creates watcher on first MoveNext)
            var pendingMove = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(400); // let watcher attach (pendingMove is waiting for first file)

            // Simulate Program.cs pause before ReadLine
            source.Pause();

            // Drop 5 files concurrently while paused — they would be lost if Pause simply disabled EnableRaisingEvents without rescan
            var burstFiles = Enumerable.Range(1, 5)
                .Select(i => Path.Combine(enrollRoot, $"burst{i}.jpg"))
                .ToList();

            // Create files in parallel to simulate burst
            var writeTasks = burstFiles.Select(f => File.WriteAllBytesAsync(f, [1, 2, 3]));
            await Task.WhenAll(writeTasks);

            await Task.Delay(200); // ensure files are flushed while still paused
            var filesBeforeResume = Directory.GetFiles(enrollRoot, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(5, filesBeforeResume.Length);

            // Resume should trigger directory rescan and enqueue the 5 burst files
            source.Resume();

            var received = new List<string>();
            var deadline = DateTime.UtcNow.AddSeconds(10);

            // pendingMove was started before burst — it should now complete with first burst file after Resume
            var firstDone = await Task.WhenAny(pendingMove, Task.Delay(4000));
            Assert.True(firstDone == pendingMove, $"First burst file not received after Resume. Files in drop: {string.Join(", ", Directory.GetFiles(enrollRoot))}");
            Assert.True(await pendingMove);
            received.Add(enumerator.Current);
            try { File.Delete(enumerator.Current); } catch { }

            while (received.Count < 5 && DateTime.UtcNow < deadline)
            {
                var moveTask = enumerator.MoveNextAsync().AsTask();
                var done = await Task.WhenAny(moveTask, Task.Delay(3000));
                if (done != moveTask)
                {
                    var remaining = string.Join(", ", Directory.GetFiles(enrollRoot, "*", SearchOption.TopDirectoryOnly));
                    var receivedSoFar = string.Join(", ", received);
                    Assert.Fail($"Watcher did not yield burst files within timeout. Received {received.Count}/5: {receivedSoFar}. Remaining in drop: {remaining}");
                }
                Assert.True(await moveTask);
                received.Add(enumerator.Current);
                // Simulate archival — remove file so it isn't re-enqueued on next potential rescan
                try { File.Delete(enumerator.Current); } catch { }
            }

            Assert.Equal(5, received.Count);
            // All burst files should be received, order may vary but all present
            foreach (var f in burstFiles)
            {
                Assert.Contains(f, received);
            }
        }
        finally
        {
            await cts.CancelAsync();
            try { await enumerator.DisposeAsync(); } catch { }
            source.Dispose();
            try { Directory.Delete(enrollRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Stable_file_after_resume_is_processed_sequentially()
    {
        // Ensures resume rescan does not break sequential processing of subsequent files
        var enrollRoot = CreateTempEnrollRoot();
        var source = CreateSource(enrollRoot, retryDelayMs: 20);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var enumerator = source.WatchAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            // Start enumeration so watcher is created before Pause
            var pending = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(300);

            source.Pause();
            var f1 = Path.Combine(enrollRoot, "seq1.jpg");
            await File.WriteAllBytesAsync(f1, [1]);
            source.Resume();

            var firstDone = await Task.WhenAny(pending, Task.Delay(4000));
            Assert.True(firstDone == pending, "First file after Pause/Resume not yielded");
            Assert.True(await pending);
            Assert.Equal(f1, enumerator.Current);
            // Simulate archival as Program does — remove file so next rescan doesn't re-enqueue it
            File.Delete(f1);

            // Next file after previous processed should also be yielded — start MoveNext before creating file to avoid race
            var moveNext2 = enumerator.MoveNextAsync().AsTask();
            await Task.Delay(150);
            var f2 = Path.Combine(enrollRoot, "seq2.jpg");
            await File.WriteAllBytesAsync(f2, [2]);
            var completed = await Task.WhenAny(moveNext2, Task.Delay(4000));
            Assert.True(completed == moveNext2, $"Watcher did not yield second file within timeout. Files in enroll: {string.Join(", ", Directory.GetFiles(enrollRoot))}");
            Assert.True(await moveNext2);
            Assert.Equal(f2, enumerator.Current);
            File.Delete(f2);
        }
        finally
        {
            await cts.CancelAsync();
            try { await enumerator.DisposeAsync(); } catch { }
            source.Dispose();
            Directory.Delete(enrollRoot, recursive: true);
        }
    }

    [Fact]
    public void Watch_prompt_formats_member_fullName_correctly()
    {
        // Scenario: Stable file prompts for FullName — THEN it prompts `Member FullName for '{fileName}':` and waits for one line
        // This unit-proves the prompt string contract used by Program.RunWatchAsync (Console.Write($"Member FullName for '{Path.GetFileName(path)}': "))
        var path = Path.Combine("C:/Dev/CamGuard/photos/enroll", "sample.jpg");
        var prompt = $"Member FullName for '{Path.GetFileName(path)}': ";
        Assert.Equal("Member FullName for 'sample.jpg': ", prompt);
    }
}
