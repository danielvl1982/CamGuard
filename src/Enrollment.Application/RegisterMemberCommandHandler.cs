using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Extracts every submitted photo's face embedding, enrolls the member, and persists it —
/// per <c>member-enrollment</c> spec's face-detection gate and atomic-persistence
/// requirements. Every submitted photo must independently pass the face-detection gate;
/// if any photo fails (zero or multiple faces detected), the entire enrollment is
/// rejected and nothing is persisted for any of the submitted photos.
/// </summary>
public sealed class RegisterMemberCommandHandler(
    IFaceEmbeddingExtractor faceEmbeddingExtractor,
    IMemberVectorRepository memberVectorRepository)
{
    public async Task<Result<MemberId, RegisterMemberError>> HandleAsync(
        RegisterMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var embeddings = new List<FaceEmbedding>(command.Photos.Count);
        foreach (var photo in command.Photos)
        {
            var extraction = await faceEmbeddingExtractor.ExtractAsync(photo, cancellationToken);
            if (extraction.IsFailure)
            {
                // Atomic all-or-nothing gate: the first photo that fails face detection
                // rejects the whole enrollment immediately — no embedding is extracted
                // from any remaining photo, and SaveAsync is never reached, so no vector
                // record is persisted for any of the submitted photos.
                return Result.Failure<MemberId, RegisterMemberError>(MapExtractionError(extraction.Error));
            }

            embeddings.Add(extraction.Value);
        }

        var memberId = MemberId.New();

        // Member currently models exactly one FaceEmbedding (see design.md's Domain
        // Model). When every submitted photo passes the gate, the first photo's
        // embedding is the primary embedding persisted for the member; the spec does
        // not require combining multiple embeddings into one record, only that
        // persistence be atomic across all submitted photos.
        var enrollment = Member.Enroll(memberId, command.FullName, embeddings[0]);
        if (enrollment.IsFailure)
            return Result.Failure<MemberId, RegisterMemberError>(RegisterMemberError.InvalidMember);

        // Persist only after every submitted photo passed the face-detection gate and the
        // member's invariants held — no partial record is ever written.
        await memberVectorRepository.SaveAsync(enrollment.Value, cancellationToken);

        return Result.Success<MemberId, RegisterMemberError>(memberId);
    }

    private static RegisterMemberError MapExtractionError(FaceExtractionError error) => error switch
    {
        FaceExtractionError.NoFaceDetected => RegisterMemberError.NoFaceDetected,
        FaceExtractionError.MultipleFacesDetected => RegisterMemberError.MultipleFacesDetected,
        _ => RegisterMemberError.ExtractionFailed
    };
}

public enum RegisterMemberError
{
    NoFaceDetected,
    MultipleFacesDetected,
    ExtractionFailed,
    InvalidMember
}
