namespace Enrollment.Console;

/// <summary>
/// Configuration for the enrollment folder-watch drop location, per the
/// <c>enrollment-watch-ingestion</c> spec. Isolated from <c>photos/watch</c>
/// (Monitoring). Consumed by <see cref="EnrollmentWatchIngestionSource"/> and
/// <see cref="EnrollmentWatchArchiver"/>.
/// </summary>
public sealed class EnrollmentWatchOptions
{
    public const string SectionName = "EnrollmentWatch";

    public string Path { get; set; } = "C:/Dev/CamGuard/photos/enroll";

    public int StabilityRetryDelayMilliseconds { get; set; } = 200;

    public int MaxStabilityRetryAttempts { get; set; } = 5;

    public int ChannelCapacity { get; set; } = 64;
}
