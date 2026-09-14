using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common;

/// <summary>
/// Shared OpenTelemetry registration extension (design.md "Observability"): registers a named
/// <see cref="ActivitySource"/> and <see cref="Meter"/> as keyed DI singletons, so producing code
/// depends on an injected instance rather than a static field/global (per
/// dotnet-testable-code-design), and each module (<c>Enrollment</c> today, <c>Monitoring</c>
/// later) can open its own named source without colliding on the shared
/// <see cref="ActivitySource"/>/<see cref="Meter"/> service types. A host process later wires an
/// OpenTelemetry TracerProvider/MeterProvider with <c>AddSource(name)</c>/<c>AddMeter(name)</c>
/// for each name registered here.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddNamedObservabilitySource(this IServiceCollection services, string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        services.TryAddKeyedSingleton(sourceName, (_, key) => new ActivitySource((string)key!));
        services.TryAddKeyedSingleton(sourceName, (_, key) => new Meter((string)key!));

        return services;
    }
}
