using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// A validated face-embedding vector produced by <c>IFaceEmbeddingExtractor</c>
/// (Application layer) or supplied directly to a directory query.
/// </summary>
public sealed record FaceEmbedding
{
    /// <summary>
    /// Expected embedding vector length for the ArcFace/LResNet100E-IR model family
    /// (FaceAiSharp's bundled ONNX model), per the convention documented in research.md.
    ///
    /// <para>
    /// <b>PROVISIONAL — UNCONFIRMED as of PR2.</b> The PR1 net10 compatibility spike
    /// (<c>FaceAiSharpNet10CompatibilityTests</c>) only exercised a synthetic faceless
    /// placeholder image, because no real reference photo was available in that
    /// session. It proved the FaceAiSharp/ONNX Runtime package graph loads and runs on
    /// net10.0 without crashing, and that a faceless image correctly reports zero
    /// detected faces — it did NOT exercise the embedding-extraction path, which
    /// requires exactly one detected face. This constant is therefore a documented
    /// assumption, not a confirmed measurement.
    /// </para>
    /// <para>
    /// Required manual follow-up before Phase 4 (Infrastructure) lets
    /// <c>FaceAiSharpEmbeddingExtractor</c> depend on this value being correct: run
    /// <c>dotnet run --project src/Enrollment.Console -- &lt;path-to-a-real-reference-photo&gt;</c>
    /// locally, confirm <c>Outcome: Success</c>, and record the printed
    /// "Embedding dimension:" value in design.md's Open Questions, replacing this
    /// assumption with the confirmed number (and updating this constant if it differs).
    /// </para>
    /// </summary>
    public const int ExpectedDimension = 512;

    private FaceEmbedding(IReadOnlyList<float> vector) => Vector = vector;

    public IReadOnlyList<float> Vector { get; }

    /// <summary>
    /// Validates <paramref name="vector"/> has length <see cref="ExpectedDimension"/>
    /// and is not an all-zero vector, then wraps it as a <see cref="FaceEmbedding"/>.
    /// </summary>
    public static Result<FaceEmbedding, FaceEmbeddingError> FromVector(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Length != ExpectedDimension)
            return Result.Failure<FaceEmbedding, FaceEmbeddingError>(FaceEmbeddingError.InvalidDimension);

        if (Array.TrueForAll(vector, static component => component == 0f))
            return Result.Failure<FaceEmbedding, FaceEmbeddingError>(FaceEmbeddingError.ZeroVector);

        return Result.Success<FaceEmbedding, FaceEmbeddingError>(new FaceEmbedding(vector));
    }

    // Vector is IReadOnlyList<float>, and arrays do not provide structural equality —
    // override the compiler-generated record equality so two embeddings with the same
    // component values compare equal regardless of the backing array instance.
    public bool Equals(FaceEmbedding? other) => other is not null && Vector.SequenceEqual(other.Vector);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in Vector)
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }
}

public enum FaceEmbeddingError
{
    InvalidDimension,
    ZeroVector
}
