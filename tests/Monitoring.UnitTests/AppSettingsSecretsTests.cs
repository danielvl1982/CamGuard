using Microsoft.Extensions.Configuration;

namespace Monitoring.UnitTests;

/// <summary>
/// Regression guard for the <c>No Delivery Secrets In Source Control</c> spec requirement's
/// "Committed configuration carries no secret" scenario: proves the checked-in
/// <c>src/Monitoring.Host/appsettings.json</c> never carries a real SMTP password so a future
/// accidental commit of a real credential fails this test instead of shipping silently.
/// </summary>
public sealed class AppSettingsSecretsTests
{
    private static readonly string AppSettingsPath = Path.Combine(AppContext.BaseDirectory, "testdata", "appsettings.json");

    private static IConfiguration LoadAppSettings() =>
        new ConfigurationBuilder()
            .AddJsonFile(AppSettingsPath, optional: false)
            .Build();

    [Fact]
    public void Committed_appsettings_carries_no_email_password()
    {
        var configuration = LoadAppSettings();

        var password = configuration["EmailNotification:Password"];

        Assert.True(string.IsNullOrEmpty(password), "EmailNotification:Password must stay empty in the committed appsettings.json; a real SMTP password must never be checked in.");
    }
}
