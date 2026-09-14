namespace Enrollment.Console;

/// <summary>
/// Outcome of running <see cref="FaceAiSharpSpike"/> against one image.
/// </summary>
public enum FaceAiSharpSpikeOutcome
{
    /// <summary>Exactly one face was detected and an embedding was generated.</summary>
    Success,

    /// <summary>The image contained zero detectable faces.</summary>
    NoFaceDetected,

    /// <summary>The image contained more than one detectable face.</summary>
    MultipleFacesDetected,

    /// <summary>Exactly one face was detected but the detector returned no landmarks for it, so alignment/embedding could not run.</summary>
    LandmarksUnavailable,
}

/// <summary>
/// Result of a single <see cref="FaceAiSharpSpike.Run"/> call. Carries enough
/// data to prove the net10.0 compatibility spike without leaking FaceAiSharp
/// types beyond this project.
/// </summary>
public sealed record FaceAiSharpSpikeResult
{
    public required FaceAiSharpSpikeOutcome Outcome { get; init; }

    public required int FacesDetected { get; init; }

    /// <summary>Non-null only when <see cref="Outcome"/> is <see cref="FaceAiSharpSpikeOutcome.Success"/>.</summary>
    public float[]? Embedding { get; init; }

    public static FaceAiSharpSpikeResult NoFaceDetected() =>
        new() { Outcome = FaceAiSharpSpikeOutcome.NoFaceDetected, FacesDetected = 0 };

    public static FaceAiSharpSpikeResult MultipleFacesDetected(int facesDetected) =>
        new() { Outcome = FaceAiSharpSpikeOutcome.MultipleFacesDetected, FacesDetected = facesDetected };

    public static FaceAiSharpSpikeResult LandmarksUnavailable() =>
        new() { Outcome = FaceAiSharpSpikeOutcome.LandmarksUnavailable, FacesDetected = 1 };

    public static FaceAiSharpSpikeResult Success(float[] embedding) =>
        new() { Outcome = FaceAiSharpSpikeOutcome.Success, FacesDetected = 1, Embedding = embedding };
}
