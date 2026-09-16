using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monitoring.Application;

namespace Monitoring.Infrastructure;

/// <summary>
/// Composition root extension for the <c>Monitoring</c> module (design.md "DI / Composition
/// Root") — mirrors <c>Enrollment.Infrastructure.EnrollmentServiceCollectionExtensions</c>: one
/// extension method, Options bound by closing over the <c>configuration</c> parameter
/// directly (never resolved from the container), per <c>dotnet-composition-root</c>'s "one
/// extension method per module" rule.
/// </summary>
public static class MonitoringServiceCollectionExtensions
{
    /// <summary>
    /// Re-exports <see cref="MonitoringObservability.ActivitySourceName"/> for Infrastructure
    /// consumers (<see cref="FileSystemWatchIngestionSource"/>, <see cref="FaceAiSharpPersonDetector"/>,
    /// and this method's own <see cref="Common.ObservabilityServiceCollectionExtensions.AddNamedObservabilitySource"/> call) — the canonical value
    /// is defined in <c>Monitoring.Application</c> because the workflow executors need it too and
    /// Application must never reference Infrastructure (see <see cref="MonitoringObservability"/>'s doc comment).
    /// </summary>
    public const string ActivitySourceName = MonitoringObservability.ActivitySourceName;

    public static IServiceCollection AddMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Mirrors AddEnrollment(...) exactly: closes over `configuration` directly, never
        // `.Configure<IConfiguration>((o, c) => ...)` resolved from the container — that form only
        // works in a full host that auto-registers IConfiguration as a service, and this exact
        // mistake was already caught and fixed once in ai-access-control's PR3
        // (EnrollmentServiceCollectionExtensions's own deviation note).
        services.AddOptions<AccessDecisionOptions>()
            .Configure(o => configuration.GetSection(AccessDecisionOptions.SectionName).Bind(o))
            .Validate(
                o => o.AllowedMaxDistance < o.DeniedMinDistance,
                "AllowedMaxDistance must be strictly less than DeniedMinDistance.");

        services.AddOptions<WatchFolderOptions>()
            .Configure(o => configuration.GetSection(WatchFolderOptions.SectionName).Bind(o));

        services.AddNamedObservabilitySource(ActivitySourceName);

        // Wraps DateTimeOffset.UtcNow — required by NotifyExecutor/PendingVerification.Create per
        // design.md ("via injected TimeProvider, never DateTime.UtcNow"); nothing else in the
        // solution registers TimeProvider yet.
        services.TryAddSingleton(TimeProvider.System);

        // FaceAiSharpPersonDetector owns the single-consumer channel serializing all detect/embed
        // calls (design.md "Concurrency Design") — singleton so exactly one channel/consumer loop
        // exists per process.
        services.TryAddSingleton<IPersonDetector, FaceAiSharpPersonDetector>();

        // Scoped, matching Enrollment.PublicApi.IMemberDirectoryReader's own Scoped registration
        // (EnrollmentServiceCollectionExtensions) — none of these three hold state that requires a
        // wider lifetime.
        services.TryAddScoped<IAccessDecisionPolicy, AccessDecisionPolicy>();
        services.TryAddScoped<INotificationService, LoggingNotificationService>();
        services.TryAddScoped<IImageArchiver, FileSystemImageArchiver>();

        // Owns the FileSystemWatcher + debounce channel for the process lifetime.
        services.TryAddSingleton<IIngestionSource, FileSystemWatchIngestionSource>();

        // Singleton so PendingVerification records accumulate across the process lifetime rather
        // than being discarded per scope (design.md's Open Questions default).
        services.TryAddSingleton<IPendingVerificationRepository, InMemoryPendingVerificationRepository>();

        // Deviation from design.md's illustrative `TryAddSingleton<MonitoringWorkflowHost>()`:
        // VerifyIdentityExecutor depends on Enrollment.PublicApi.IMemberDirectoryReader, which
        // AddEnrollment(...) registers Scoped — out of Monitoring's control. A Singleton
        // MonitoringWorkflowHost would capture that Scoped dependency (and this module's own
        // Scoped IAccessDecisionPolicy/INotificationService/IImageArchiver) forever as a captive
        // dependency, an invalid lifetime combination the .NET DI container rejects when scope
        // validation is enabled. Registering the host and its four executors Scoped instead keeps
        // every dependency direction valid (Scoped may depend on Scoped or Singleton) while still
        // building one workflow graph per resolution, per design.md's Application Flow.
        services.TryAddScoped<DetectPersonExecutor>();
        services.TryAddScoped<VerifyIdentityExecutor>();
        services.TryAddScoped<DecideAccessExecutor>();
        services.TryAddScoped<NotifyExecutor>();
        services.TryAddScoped<MonitoringWorkflowHost>();

        // Drives the folder-watch → workflow → archive loop for the process lifetime (see
        // MonitoringHostedService's doc comment); resolves the Scoped MonitoringWorkflowHost
        // through a fresh IServiceScope per ingested file rather than depending on it directly.
        services.AddHostedService<MonitoringHostedService>();

        return services;
    }
}
