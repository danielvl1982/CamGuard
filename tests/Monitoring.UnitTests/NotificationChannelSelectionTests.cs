using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monitoring.Application;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="MonitoringServiceCollectionExtensions.AddMonitoring"/>'s environment-gated
/// <see cref="INotificationService"/> selection: Development keeps the logging placeholder,
/// every other environment gets the real channels composed through <see cref="CompositeNotificationService"/>.
/// </summary>
public sealed class NotificationChannelSelectionTests
{
    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    [Fact]
    public void Development_environment_resolves_the_logging_placeholder()
    {
        var services = new ServiceCollection();
        services.AddMonitoring(EmptyConfiguration(), new FakeHostEnvironment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        var notificationService = provider.GetRequiredService<INotificationService>();

        Assert.IsType<LoggingNotificationService>(notificationService);
    }

    [Fact]
    public void Non_development_environment_resolves_the_composite_of_real_channels()
    {
        var services = new ServiceCollection();
        services.AddMonitoring(EmptyConfiguration(), new FakeHostEnvironment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        var notificationService = provider.GetRequiredService<INotificationService>();

        Assert.IsType<CompositeNotificationService>(notificationService);
    }
}
