using Enrollment.Application;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Enrollment.PublicApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enrollment.UnitTests;

/// <summary>
/// Composition-root smoke test (task 7.1) — proves <c>AddEnrollment(...)</c> registers a fully
/// resolvable object graph, per design.md's "DI / Composition Root": every port/handler resolves
/// without exception from a real <see cref="ServiceProvider"/>, including
/// <see cref="FaceAiSharpEmbeddingExtractor"/>'s FaceAiSharp detector/generator singletons (the
/// PR1 native-ONNX-Runtime finding applied for real, not just via the standalone spike).
/// </summary>
public sealed class EnrollmentServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEnrollment_registers_a_resolvable_object_graph_against_a_temp_sqlite_file()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"camguard-di-smoke-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection([new("VectorStore:ConnectionString", $"Data Source={dbPath}")])
                .Build();

            services.AddEnrollment(configuration);

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();

            var reader = scope.ServiceProvider.GetRequiredService<IMemberDirectoryReader>();
            var registerHandler = scope.ServiceProvider.GetRequiredService<RegisterMemberCommandHandler>();
            var queryHandler = scope.ServiceProvider.GetRequiredService<QueryByEmbeddingQueryHandler>();
            var incidentRepository = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();

            Assert.NotNull(reader);
            Assert.NotNull(registerHandler);
            Assert.NotNull(queryHandler);
            Assert.NotNull(incidentRepository);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }
        }
    }
}
