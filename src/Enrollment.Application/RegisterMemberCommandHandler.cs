using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Extracts the photo's face embedding, enrolls the member, and persists it —
/// per <c>member-enrollment</c> spec's face-detection gate and atomic-persistence
/// requirements.
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

        var extraction = await faceEmbeddingExtractor.ExtractAsync(command.Photo, cancellationToken);
        if (extraction.IsFailure)
            return Result.Failure<MemberId, RegisterMemberError>(MapExtractionError(extraction.Error));

        var memberId = MemberId.New();
        var enrollment = Member.Enroll(memberId, command.FullName, extraction.Value);
        if (enrollment.IsFailure)
            return Result.Failure<MemberId, RegisterMemberError>(RegisterMemberError.InvalidMember);

        // Persist only after the embedding passed the face-detection gate and the
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
