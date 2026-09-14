using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>Domain unit tests for <see cref="PendingVerificationId"/>, mirroring <c>MemberIdTests</c>' shape.</summary>
public sealed class PendingVerificationIdTests
{
    [Fact]
    public void New_never_returns_an_empty_guid()
    {
        var id = PendingVerificationId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void From_rejects_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => PendingVerificationId.From(Guid.Empty));
    }

    [Fact]
    public void From_accepts_non_empty_guid()
    {
        var guid = Guid.NewGuid();

        var id = PendingVerificationId.From(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Two_ids_with_the_same_value_are_equal()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(PendingVerificationId.From(guid), PendingVerificationId.From(guid));
    }
}
