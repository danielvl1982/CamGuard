using Enrollment.Console;
using FaceAiSharp;

namespace Enrollment.UnitTests;

/// <summary>
/// Characterization test for the Phase 2 gate: "does FaceAiSharp run on
/// net10.0 without exception?" (tasks.md 2.5/2.6).
///
/// IMPORTANT — scope of what this test proves: no real photo of a human face
/// is available in this environment, so this test exercises the spike against
/// a faceless synthetic placeholder image only. It proves:
///   1. FaceAiSharpBundleFactory can construct a detector + embeddings
///      generator on net10.0 without throwing (ONNX Runtime loads, bundled
///      models resolve) — the actual net10 compatibility risk from the spike.
///   2. Running detection against an image with zero faces returns the
///      "zero faces detected" outcome, not a crash.
///
/// It does NOT prove that a real face produces a non-null embedding of the
/// expected dimension. That requires a manual follow-up run — before Phase 4
/// (Infrastructure) trusts the embedding dimension/metric — via:
///   dotnet run --project src/Enrollment.Console -- &lt;path-to-a-real-photo&gt;
/// and recording the printed "Embedding dimension" in design.md's Open
/// Questions (task 2.6), replacing the current unconfirmed 512 assumption.
/// </summary>
public sealed class FaceAiSharpNet10CompatibilityTests
{
    private static readonly string FacelessImagePath = Path.Combine(AppContext.BaseDirectory, "testdata", "blank-image.png");

    [Fact]
    public void Constructing_the_detector_and_embeddings_generator_does_not_throw()
    {
        var exception = Record.Exception(() =>
        {
            _ = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
            _ = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Running_the_spike_against_a_faceless_image_reports_zero_faces_without_throwing()
    {
        IFaceDetectorWithLandmarks detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        IFaceEmbeddingsGenerator embeddingsGenerator = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
        var spike = new FaceAiSharpSpike(detector, embeddingsGenerator);

        FaceAiSharpSpikeResult? result = null;
        var exception = Record.Exception(() => result = spike.Run(FacelessImagePath));

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Equal(FaceAiSharpSpikeOutcome.NoFaceDetected, result!.Outcome);
        Assert.Equal(0, result.FacesDetected);
        Assert.Null(result.Embedding);
    }
}
