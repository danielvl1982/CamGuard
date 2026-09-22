using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enrollment.Console;

/// <summary>
/// Enrollment-scoped watcher isolated to <c>photos/enroll</c> (never <c>photos/watch</c>),
/// reusing Monitoring's <c>FileSystemWatcher</c>+<c>Channel</c>+<c>FileShare.None</c>
/// 5×200ms stability gate. Exposes <see cref="Pause"/>/<see cref="Resume"/> so the
/// console can pause while blocked on <c>Console.ReadLine</c> to avoid bounded-channel
/// <c>DropOldest</c> loss, per the enrollment-watch-ingestion spec.
/// </summary>
public sealed class EnrollmentWatchIngestionSource : IAsyncDisposable, IDisposable
{
    private readonly EnrollmentWatchOptions _options;
    private readonly ILogger<EnrollmentWatchIngestionSource> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Channel<string> _channel;
    private FileSystemWatcher? _watcher;

    public EnrollmentWatchIngestionSource(
        IOptions<EnrollmentWatchOptions> options,
        ILogger<EnrollmentWatchIngestionSource> logger,
        [FromKeyedServices("CamGuard.Enrollment.Watch")] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(1, _options.ChannelCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    public async IAsyncEnumerable<string> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.Path);

        _watcher = new FileSystemWatcher(_options.Path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Created += OnCreated;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;

        try
        {
            await foreach (var candidatePath in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await IsStableAsync(candidatePath, cancellationToken).ConfigureAwait(false))
                {
                    yield return candidatePath;
                }
                else
                {
                    // Never stabilized — quarantine per enrollment-watch-ingestion spec
                    await TryQuarantineUnstableAsync(candidatePath, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _watcher.Created -= OnCreated;
            _watcher.Error -= OnError;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void Pause()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
        }
    }

    public void Resume()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = true;
            RescanAndEnqueue();
        }
    }

    private void RescanAndEnqueue()
    {
        try
        {
            // Files that arrived while paused (EnableRaisingEvents=false) were not
            // observed via OnCreated. Re-scan the top-level drop folder so the
            // prompt-blocking scenario does not lose a burst of files (spec: 5 files
            // during prompt must not be dropped). Only top-level files are enqueued;
            // processed/quarantine subfolders are not scanned (SearchOption.TopDirectoryOnly).
            foreach (var file in Directory.EnumerateFiles(_options.Path, "*", SearchOption.TopDirectoryOnly))
            {
                if (Directory.Exists(file))
                {
                    continue;
                }

                if (!_channel.Writer.TryWrite(file))
                {
                    _logger.LogWarning("Enrollment ingestion rescan dropped event for {Path} (channel full).", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Enrollment ingestion rescan failed for {Path}.", _options.Path);
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath))
        {
            return;
        }

        if (!_channel.Writer.TryWrite(e.FullPath))
        {
            _logger.LogWarning("Enrollment ingestion channel is full; dropped event for {Path}.", e.FullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "Enrollment FileSystemWatcher reported an error (possible internal buffer overflow).");
    }

    private async Task<bool> IsStableAsync(string path, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.Watch.Ingest");
        activity?.SetTag("file.path", path);

        for (var attempt = 1; attempt <= _options.MaxStabilityRetryAttempts; attempt++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                activity?.SetTag("debounce.attempts", attempt);
                return true;
            }
            catch (FileNotFoundException)
            {
                activity?.SetTag("debounce.attempts", attempt);
                return false;
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.StabilityRetryDelayMilliseconds), cancellationToken).ConfigureAwait(false);
            }
        }

        activity?.SetTag("debounce.attempts", _options.MaxStabilityRetryAttempts);
        _logger.LogWarning("Enrollment file {Path} did not stabilize after {Attempts} attempts; quarantining.", path, _options.MaxStabilityRetryAttempts);
        return false;
    }

    private async Task TryQuarantineUnstableAsync(string sourcePath, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxStabilityRetryAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return;
                }

                var quarantineFolder = Path.Combine(_options.Path, "quarantine");
                Directory.CreateDirectory(quarantineFolder);
                var targetPath = Path.Combine(quarantineFolder, Path.GetFileName(sourcePath));
                File.Move(sourcePath, targetPath, overwrite: true);
                _logger.LogInformation("Quarantined unstable enrollment file {Source} -> {Target}.", sourcePath, targetPath);
                return;
            }
            catch (IOException) when (attempt < _options.MaxStabilityRetryAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.StabilityRetryDelayMilliseconds), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to quarantine unstable enrollment file {Path}.", sourcePath);
                return;
            }
        }

        // Final attempt after retries
        try
        {
            if (File.Exists(sourcePath))
            {
                var quarantineFolder = Path.Combine(_options.Path, "quarantine");
                Directory.CreateDirectory(quarantineFolder);
                var targetPath = Path.Combine(quarantineFolder, Path.GetFileName(sourcePath));
                File.Move(sourcePath, targetPath, overwrite: true);
                _logger.LogInformation("Quarantined unstable enrollment file {Source} -> {Target}.", sourcePath, targetPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to quarantine unstable enrollment file {Path}.", sourcePath);
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _watcher?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _watcher?.Dispose();
        return ValueTask.CompletedTask;
    }
}
