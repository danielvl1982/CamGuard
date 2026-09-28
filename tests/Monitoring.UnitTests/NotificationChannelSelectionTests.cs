using System.Collections.Generic;
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

    // Non-Development registers the real Ntfy/Email channels, which now fail fast (Fix B) unless
    // Topic/SmtpHost/FromAddress/ToAddress are configured.
    private static IConfiguration ConfiguredNotificationChannelsConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ntfy:Topic"] = "camguard-alerts",
            ["EmailNotification:SmtpHost"] = "smtp.example.com",
            ["EmailNotification:FromAddress"] = "alerts@example.com",
            ["EmailNotification:ToAddress"] = "security@example.com",
        })
        .Build();

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
        services.AddMonitoring(ConfiguredNotificationChannelsConfiguration(), new FakeHostEnvironment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        var notificationService = provider.GetRequiredService<INotificationService>();

        Assert.IsType<CompositeNotificationService>(notificationService);
    }

    [Theory]
    [InlineData("Ntfy:Topic", "")]
    [InlineData("EmailNotification:SmtpHost", "")]
    [InlineData("EmailNotification:FromAddress", "")]
    [InlineData("EmailNotification:ToAddress", "")]
    public void Non_development_environment_fails_fast_on_missing_notification_options(string keyToBlank, string blankValue)
    {
        var values = new Dictionary<string, string?>
        {
            ["Ntfy:Topic"] = "camguard-alerts",
            ["EmailNotification:SmtpHost"] = "smtp.example.com",
            ["EmailNotification:FromAddress"] = "alerts@example.com",
            ["EmailNotification:ToAddress"] = "security@example.com",
            [keyToBlank] = blankValue,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddMonitoring(configuration, new FakeHostEnvironment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            () => provider.GetRequiredService<INotificationService>());
    }
}
