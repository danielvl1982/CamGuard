namespace Monitoring.Application;

/// <summary>
/// Port wrapping face-detection-as-person-presence-proxy for one ingested image, per the
/// <c>person-detection</c> spec. The only implementation is <c>FaceAiSharpPersonDetector</c>
/// (Infrastructure, channel-serialized) — this port itself has zero dependency on FaceAiSharp,
/// ONNX Runtime, or any other external library.
/// </summary>
public interface IPersonDetector
{
    Task<PersonDetectionResult> DetectAsync(Stream image, CancellationToken cancellationToken);
}

/// <summary>
/// Result of one person-detection call.
///
/// <para>
/// <b>Scoping note:</b> this record deliberately models exactly one embedding per image — this
/// design scopes to single-person-per-image access control (the primary/first detected face
/// only). Multiple simultaneous faces in one frame is explicitly out of scope for this change,
/// not an oversight, and is left for a future change to model as <c>EmbeddingVector[]</c> if ever
/// needed (see design.md's Ports and Adapters section).
/// </para>
/// </summary>
public sealed record PersonDetectionResult(bool PersonPresent, float[]? EmbeddingVector);
