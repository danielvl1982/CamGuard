using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monitoring.Domain;
using Monitoring.Infrastructure;

namespace Monitoring.UnitTests;

/// <summary>
/// Tests for <see cref="AccessDecisionPolicy"/>, per the <c>access-decision-policy</c> spec:
/// independently configured thresholds partition the distance range into exactly three outcomes,
/// and an invalid threshold pair fails fast at options-binding time.
/// </summary>
public sealed class AccessDecisionPolicyTests
{
    private static AccessDecisionPolicy CreatePolicy(double allowedMax, double deniedMin) =>
        new(Options.Create(new AccessDecisionOptions { AllowedMaxDistance = allowedMax, DeniedMinDistance = deniedMin }));

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.35)]
    public void Distance_at_or_below_AllowedMaxDistance_resolves_Allowed(double distance)
    {
        var policy = CreatePolicy(allowedMax: 0.35, deniedMin: 0.55);

        var result = policy.Decide(distance);

        Assert.Equal(AccessOutcome.Allowed, result.Outcome);
        Assert.Equal(distance, result.Distance);
    }

    [Theory]
    [InlineData(0.55)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Distance_at_or_above_DeniedMinDistance_resolves_Denied(double distance)
    {
        var policy = CreatePolicy(allowedMax: 0.35, deniedMin: 0.55);

        var result = policy.Decide(distance);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(distance, result.Distance);
    }

    [Theory]
    [InlineData(0.36)]
    [InlineData(0.45)]
    [InlineData(0.54)]
    public void Distance_strictly_between_thresholds_resolves_HumanVerificationRequired(double distance)
    {
        var policy = CreatePolicy(allowedMax: 0.35, deniedMin: 0.55);

        var result = policy.Decide(distance);

        Assert.Equal(AccessOutcome.HumanVerificationRequired, result.Outcome);
        Assert.Equal(distance, result.Distance);
    }

    [Fact]
    public void AddMonitoring_fails_fast_when_AllowedMaxDistance_is_not_strictly_less_than_DeniedMinDistance()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("AccessDecision:AllowedMaxDistance", "0.6"),
                new("AccessDecision:DeniedMinDistance", "0.5"),
            ])
            .Build();

        services.AddMonitoring(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<AccessDecisionOptions>>().Value);
    }

    [Fact]
    public void AddMonitoring_starts_normally_when_AllowedMaxDistance_is_strictly_less_than_DeniedMinDistance()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("AccessDecision:AllowedMaxDistance", "0.35"),
                new("AccessDecision:DeniedMinDistance", "0.55"),
            ])
            .Build();

        services.AddMonitoring(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AccessDecisionOptions>>().Value;

        Assert.Equal(0.35, options.AllowedMaxDistance);
        Assert.Equal(0.55, options.DeniedMinDistance);
    }
}
