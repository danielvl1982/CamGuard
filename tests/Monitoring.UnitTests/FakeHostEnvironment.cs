using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Monitoring.UnitTests;

/// <summary>Minimal <see cref="IHostEnvironment"/> test double so tests can call <c>AddMonitoring</c> without a full host.</summary>
internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Monitoring.UnitTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
