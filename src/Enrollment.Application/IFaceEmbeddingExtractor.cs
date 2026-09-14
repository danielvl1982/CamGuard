using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Port wrapping face detection + embedding extraction from a reference photo.
/// The only implementation is <c>FaceAiSharpEmbeddingExtractor</c> (Phase 4,
/// Infrastructure) — this port itself has zero dependency on FaceAiSharp or any
/// other external library.
/// </summary>
public interface IFaceEmbeddingExtractor
{
    Task<Result<FaceEmbedding, FaceExtractionError>> ExtractAsync(Stream photo, CancellationToken cancellationToken);
}

public enum FaceExtractionError
{
    NoFaceDetected,
    MultipleFacesDetected,
    ExtractionFailed
}
