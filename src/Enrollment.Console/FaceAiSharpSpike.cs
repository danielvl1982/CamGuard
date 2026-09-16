using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Enrollment.Console;

/// <summary>
/// Standalone spike proving FaceAiSharp (SCRFD detector + ArcFace embeddings
/// generator) runs on net10.0 before <c>Enrollment.Infrastructure</c> takes a
/// dependency on it. Deliberately has zero reference to any <c>Enrollment.*</c>
/// project — a failure here must not be hidden behind Domain/Application/DI
/// wiring (see design.md "Spike harness" decision).
///
/// Detector and embeddings generator are injected through the constructor
/// (never resolved internally) so this class can be exercised by
/// <c>FaceAiSharpNet10CompatibilityTests</c> without a live ONNX runtime call
/// per test, per dotnet-testable-code-design.
/// </summary>
public sealed class FaceAiSharpSpike(IFaceDetectorWithLandmarks detector, IFaceEmbeddingsGenerator embeddingsGenerator)
{
    /// <summary>
    /// Runs face detection + embedding extraction on the image at <paramref name="imagePath"/>.
    /// Never throws for a zero-face or multi-face image — those are valid,
    /// expected outcomes surfaced through <see cref="FaceAiSharpSpikeResult"/>.
    /// </summary>
    public FaceAiSharpSpikeResult Run(string imagePath)
    {
        using var image = Image.Load<Rgb24>(imagePath);

        var faces = detector.DetectFaces(image);

        if (faces.Count == 0)
        {
            return FaceAiSharpSpikeResult.NoFaceDetected();
        }

        if (faces.Count > 1)
        {
            return FaceAiSharpSpikeResult.MultipleFacesDetected(faces.Count);
        }

        var face = faces.Single();

        if (face.Landmarks is null || face.Landmarks.Count == 0)
        {
            return FaceAiSharpSpikeResult.LandmarksUnavailable();
        }

        embeddingsGenerator.AlignFaceUsingLandmarks(image, face.Landmarks);
        var embedding = embeddingsGenerator.GenerateEmbedding(image);

        return FaceAiSharpSpikeResult.Success(embedding);
    }
}
