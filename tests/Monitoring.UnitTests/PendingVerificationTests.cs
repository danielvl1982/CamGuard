using Monitoring.Domain;

namespace Monitoring.UnitTests;

/// <summary>Domain unit tests for <see cref="PendingVerification.Create"/> invariants.</summary>
public sealed class PendingVerificationTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_in_Pending_status()
    {
        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-1.png", distance: null, candidateMemberId: null, OccurredAt);

        Assert.Equal(PendingVerificationStatus.Pending, record.Status);
    }

    [Fact]
    public void Create_from_zero_face_path_allows_null_distance_and_null_candidate()
    {
        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-2.png", distance: null, candidateMemberId: null, OccurredAt);

        Assert.Null(record.Distance);
        Assert.Null(record.CandidateMemberId);
        Assert.Equal("processed/image-2.png", record.ImageReference);
        Assert.Equal(OccurredAt, record.OccurredAtUtc);
    }

    [Fact]
    public void Create_from_mid_threshold_path_retains_distance_and_candidate()
    {
        var candidateMemberId = Guid.NewGuid();

        var record = PendingVerification.Create(
            PendingVerificationId.New(), "processed/image-3.png", distance: 0.55, candidateMemberId, OccurredAt);

        Assert.Equal(0.55, record.Distance);
        Assert.Equal(candidateMemberId, record.CandidateMemberId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_or_whitespace_image_reference(string imageReference)
    {
        Assert.Throws<ArgumentException>(() =>
            PendingVerification.Create(PendingVerificationId.New(), imageReference, null, null, OccurredAt));
    }

    [Fact]
    public void Create_throws_on_null_id()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PendingVerification.Create(null!, "processed/image-4.png", null, null, OccurredAt));
    }
}
