namespace Monitoring.Application;

/// <summary>
/// Named <see cref="System.Diagnostics.ActivitySource"/>/<see cref="System.Diagnostics.Metrics.Meter"/>
/// this module opens via <c>Common.ObservabilityServiceCollectionExtensions.AddNamedObservabilitySource</c>
/// (design.md "Observability").
///
/// <para>
/// Defined here in <c>Monitoring.Application</c> — not <c>Monitoring.Infrastructure</c>, unlike
/// <c>Enrollment.Infrastructure.EnrollmentServiceCollectionExtensions.ActivitySourceName</c> — because
/// design.md's Observability table instruments the workflow executors themselves (this project),
/// each injecting this named <see cref="System.Diagnostics.ActivitySource"/> via
/// <c>[FromKeyedServices(...)]</c>. Application must never reference <c>Monitoring.Infrastructure</c>
/// (dependency-direction rule), so the constant lives on the side that needs it first; Infrastructure's
/// <c>MonitoringServiceCollectionExtensions.AddMonitoring(...)</c> reuses this exact same value when
/// it registers the keyed singleton and when its own adapters (<c>FileSystemWatchIngestionSource</c>,
/// <c>FaceAiSharpPersonDetector</c>) inject the same named source for their own spans.
/// </para>
/// </summary>
public static class MonitoringObservability
{
    public const string ActivitySourceName = "CamGuard.Monitoring";
}
