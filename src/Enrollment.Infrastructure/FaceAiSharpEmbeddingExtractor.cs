using System.Diagnostics;
using System.Linq;
using CSharpFunctionalExtensions;
using Enrollment.Application;
using Enrollment.Domain;
using FaceAiSharp;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Enrollment.Infrastructure;

/// <summary>
/// FaceAiSharp-backed <see cref="IFaceEmbeddingExtractor"/>: SCRFD face-detection gate (reject 0
/// or 2+ faces) + ArcFace embedding, per <c>member-enrollment</c> spec's face-detection-gate
/// requirements and design.md's "Ports and Adapters". Detector/generator are constructor-injected
/// — never resolved internally — mirroring the PR1 spike (<c>Enrollment.Console.FaceAiSharpSpike</c>)
/// per dotnet-testable-code-design.
/// </summary>
public sealed class FaceAiSharpEmbeddingExtractor(
    IFaceDetectorWithLandmarks detector,
    IFaceEmbeddingsGenerator embeddingsGenerator,
    [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    : IFaceEmbeddingExtractor
{
    public Task<Result<FaceEmbedding, FaceExtractionError>> ExtractAsync(Stream photo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);
        cancellationToken.ThrowIfCancellationRequested();

        using Activity? activity = activitySource.StartActivity("Enrollment.ExtractEmbedding");

        using Image<Rgb24> image = Image.Load<Rgb24>(photo);

        IReadOnlyCollection<FaceDetectorResult> faces = detector.DetectFaces(image);
        activity?.SetTag("faces.detected.count", faces.Count);

        if (faces.Count == 0)
            return Failed(activity, FaceExtractionError.NoFaceDetected);

        if (faces.Count > 1)
            return Failed(activity, FaceExtractionError.MultipleFacesDetected);

        FaceDetectorResult face = faces.First();
        if (face.Landmarks is null || face.Landmarks.Count == 0)
            return Failed(activity, FaceExtractionError.ExtractionFailed);

        embeddingsGenerator.AlignFaceUsingLandmarks(image, face.Landmarks);
        float[] vector = embeddingsGenerator.GenerateEmbedding(image);

        Result<FaceEmbedding, FaceEmbeddingError> embedding = FaceEmbedding.FromVector(vector);
        if (embedding.IsFailure)
            return Failed(activity, FaceExtractionError.ExtractionFailed);

        activity?.SetTag("outcome", "Success");
        return Task.FromResult(Result.Success<FaceEmbedding, FaceExtractionError>(embedding.Value));
    }

    private static Task<Result<FaceEmbedding, FaceExtractionError>> Failed(Activity? activity, FaceExtractionError error)
    {
        activity?.SetTag("outcome", error.ToString());
        return Task.FromResult(Result.Failure<FaceEmbedding, FaceExtractionError>(error));
    }
}
