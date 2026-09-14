using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>Domain unit tests for the <see cref="AccessDecisionResult"/> value object.</summary>
public sealed class AccessDecisionResultTests
{
    [Fact]
    public void Two_results_with_equal_components_are_equal()
    {
        var first = new AccessDecisionResult(AccessOutcome.Allowed, 0.1, "Close match");
        var second = new AccessDecisionResult(AccessOutcome.Allowed, 0.1, "Close match");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Results_with_different_outcomes_are_not_equal()
    {
        var allowed = new AccessDecisionResult(AccessOutcome.Allowed, 0.1, "Close match");
        var denied = new AccessDecisionResult(AccessOutcome.Denied, 0.9, "Far match");

        Assert.NotEqual(allowed, denied);
    }

    [Fact]
    public void Distance_can_be_null_for_the_zero_face_short_circuit_path()
    {
        var result = new AccessDecisionResult(AccessOutcome.HumanVerificationRequired, Distance: null, "No face detected");

        Assert.Null(result.Distance);
        Assert.Equal(AccessOutcome.HumanVerificationRequired, result.Outcome);
    }
}
