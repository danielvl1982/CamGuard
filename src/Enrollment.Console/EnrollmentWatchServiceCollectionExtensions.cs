using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Enrollment.Console;

/// <summary>
/// Console-layer composition helper for enrollment watch ingestion, isolated from
/// <c>Enrollment.Infrastructure.AddEnrollment</c> to avoid a circular reference
/// (Infrastructure does not reference Console). Binds <see cref="EnrollmentWatchOptions"/>
/// via the same closure pattern as <c>VectorStoreOptions</c> and registers the
/// watcher archiver/source for the <c>photos/enroll</c> drop folder.
/// </summary>
public static class EnrollmentWatchServiceCollectionExtensions
{
    public static IServiceCollection AddEnrollmentWatch(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EnrollmentWatchOptions>()
            .Configure(o => configuration.GetSection(EnrollmentWatchOptions.SectionName).Bind(o));

        services.AddSingleton<EnrollmentWatchArchiver>();
        services.AddSingleton<EnrollmentWatchIngestionSource>();

        return services;
    }
}
