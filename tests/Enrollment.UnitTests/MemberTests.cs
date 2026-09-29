using Enrollment.Domain;

namespace Enrollment.UnitTests;

/// <summary>Domain unit tests for <see cref="Member.Enroll"/> invariants (task 9.1).</summary>
public sealed class MemberTests
{
    private static FaceEmbedding ValidEmbedding() => FaceEmbedding.FromVector(BuildNonZeroVector()).Value;

    private static float[] BuildNonZeroVector()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.42f;
        return vector;
    }

    [Fact]
    public void Enroll_with_non_empty_full_name_and_valid_embedding_succeeds()
    {
        var id = MemberId.New();
        var embedding = ValidEmbedding();

        var result = Member.Enroll(id, "Ada Lovelace", embedding);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal("Ada Lovelace", result.Value.FullName);
        Assert.Equal(embedding, result.Value.Embedding);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Enroll_rejects_empty_or_whitespace_full_name(string fullName)
    {
        var result = Member.Enroll(MemberId.New(), fullName, ValidEmbedding());

        Assert.True(result.IsFailure);
        Assert.Equal(MemberError.EmptyFullName, result.Error);
    }

    [Fact]
    public void Rename_with_valid_name_trims_and_updates_full_name()
    {
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;

        var result = member.Rename("  Grace Hopper ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Grace Hopper", member.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_with_blank_name_fails_and_keeps_current_name(string? fullName)
    {
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;

        var result = member.Rename(fullName!);

        Assert.True(result.IsFailure);
        Assert.Equal(MemberError.EmptyFullName, result.Error);
        Assert.Equal("Ada Lovelace", member.FullName);
    }
}
