using Common;
using Enrollment.Application;
using Enrollment.Domain;
using Enrollment.PublicApi;
using FaceAiSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Enrollment.Infrastructure;

/// <summary>
/// Composition root extension for the <c>Enrollment</c> module (design.md "DI / Composition
/// Root") — a single call site wires every port/handler and binds <see cref="VectorStoreOptions"/>,
/// per <c>dotnet-composition-root</c>'s "one extension method per module" rule.
/// </summary>
public static class EnrollmentServiceCollectionExtensions
{
    /// <summary>
    /// Named <see cref="System.Diagnostics.ActivitySource"/>/<see cref="System.Diagnostics.Metrics.Meter"/>
    /// this module opens via <see cref="ObservabilityServiceCollectionExtensions.AddNamedObservabilitySource"/>
    /// (design.md "Observability"). Shared by <see cref="FaceAiSharpEmbeddingExtractor"/> and
    /// <see cref="SqliteVecMemberRepository"/> for their spans.
    /// </summary>
    public const string ActivitySourceName = "CamGuard.Enrollment";

    public const string WatchActivitySourceName = "CamGuard.Enrollment.Watch";

    public static IServiceCollection AddEnrollment(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Deviation from design.md's illustrative `.Configure<IConfiguration>((o, c) => ...)`
        // snippet: that form resolves IConfiguration *from the container*, which only works in a
        // full host (e.g. ASP.NET Core's WebApplicationBuilder) that auto-registers it as a
        // service. AddEnrollment already receives `configuration` as a parameter, so closing over
        // it directly binds correctly in any host, including a bare ServiceCollection (proven by
        // EnrollmentServiceCollectionExtensionsTests, which failed with
        // "No service for type IConfiguration has been registered" before this fix).
        services.AddOptions<VectorStoreOptions>()
            .Configure(o => configuration.GetSection(VectorStoreOptions.SectionName).Bind(o));

        services.AddNamedObservabilitySource(ActivitySourceName);
        services.AddNamedObservabilitySource(WatchActivitySourceName);

        // FaceAiSharp's detector/embeddings-generator wrap loaded ONNX models — expensive to
        // construct, so they are singletons, built only through the documented factory (never a
        // parameterless `new`/service-locator call inside a collaborator, per
        // dotnet-testable-code-design). Native ONNX Runtime binaries must also be referenced by
        // this project directly (see PR1 spike finding, design.md Open Questions) or this factory
        // crashes the process instead of throwing.
        services.TryAddSingleton<IFaceDetectorWithLandmarks>(_ => FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks());
        services.TryAddSingleton<IFaceEmbeddingsGenerator>(_ => FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator());

        services.TryAddScoped<IFaceEmbeddingExtractor, FaceAiSharpEmbeddingExtractor>();
        services.TryAddScoped<IMemberVectorRepository, SqliteVecMemberRepository>();
        services.TryAddScoped<IMemberImageRepository, SqliteVecMemberImageRepository>();
        services.TryAddScoped<IIncidentRepository, SqliteIncidentRepository>();
        services.TryAddScoped<IMemberImageDuplicateValidator>(sp =>
            new MemberImageDuplicateValidator(
                sp.GetRequiredService<IMemberImageRepository>(),
                sp.GetRequiredService<IOptions<VectorStoreOptions>>().Value.DuplicateFaceDistanceThreshold));
        services.TryAddScoped<RegisterMemberCommandHandler>();
        services.TryAddScoped<AddMemberImagesCommandHandler>();
        services.TryAddScoped<EnrollmentService>();
        services.TryAddScoped<QueryByEmbeddingQueryHandler>();
        services.TryAddScoped<IMemberDirectoryReader, MemberDirectoryReader>();

        return services;
    }
}
