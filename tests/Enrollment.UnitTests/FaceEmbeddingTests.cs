using Enrollment.Domain;

namespace Enrollment.UnitTests;

/// <summary>Domain unit tests for <see cref="FaceEmbedding.FromVector"/> validation (task 9.2).</summary>
public sealed class FaceEmbeddingTests
{
    [Fact]
    public void FromVector_with_expected_dimension_and_non_zero_values_succeeds()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.5f;

        var result = FaceEmbedding.FromVector(vector);

        Assert.True(result.IsSuccess);
        Assert.Equal(FaceEmbedding.ExpectedDimension, result.Value.Vector.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(513)]
    public void FromVector_rejects_vector_with_wrong_dimension(int length)
    {
        var vector = new float[length];
        if (length > 0)
        {
            vector[0] = 1f;
        }

        var result = FaceEmbedding.FromVector(vector);

        Assert.True(result.IsFailure);
        Assert.Equal(FaceEmbeddingError.InvalidDimension, result.Error);
    }

    [Fact]
    public void FromVector_rejects_all_zero_vector()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];

        var result = FaceEmbedding.FromVector(vector);

        Assert.True(result.IsFailure);
        Assert.Equal(FaceEmbeddingError.ZeroVector, result.Error);
    }

    [Fact]
    public void FromVector_throws_on_null_vector()
    {
        Assert.Throws<ArgumentNullException>(() => FaceEmbedding.FromVector(null!));
    }

    [Fact]
    public void Two_embeddings_with_equal_components_are_equal()
    {
        var first = new float[FaceEmbedding.ExpectedDimension];
        first[10] = 0.75f;
        var second = (float[])first.Clone();

        var a = FaceEmbedding.FromVector(first).Value;
        var b = FaceEmbedding.FromVector(second).Value;

        Assert.Equal(a, b);
    }
}
