using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Single-photo enrollment service for the watch ingestion path, per the
/// <c>member-enrollment</c> delta: validates <c>FullName</c>, branches on
/// <see cref="IMemberVectorRepository.FindByFullNameAsync"/> (trim +
/// <see cref="StringComparison.OrdinalIgnoreCase"/>), enforces exactly one
/// embedding per member via Upsert with reused <see cref="MemberId"/>.
/// </summary>
public sealed class EnrollmentService(
    IFaceEmbeddingExtractor faceEmbeddingExtractor,
    IMemberVectorRepository memberVectorRepository)
{
    public async Task<Result<MemberId, RegisterMemberError>> EnrollAsync(
        string fullName,
        Stream photo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<MemberId, RegisterMemberError>(RegisterMemberError.InvalidName);
        }

        var trimmedName = fullName.Trim();

        var existing = await memberVectorRepository.FindByFullNameAsync(trimmedName, cancellationToken).ConfigureAwait(false);

        var extraction = await faceEmbeddingExtractor.ExtractAsync(photo, cancellationToken).ConfigureAwait(false);
        if (extraction.IsFailure)
        {
            return Result.Failure<MemberId, RegisterMemberError>(MapExtractionError(extraction.Error));
        }

        var memberId = existing?.Id ?? MemberId.New();

        var enrollment = Member.Enroll(memberId, trimmedName, extraction.Value);
        if (enrollment.IsFailure)
        {
            // Member.Enroll only fails on empty FullName (already validated) or null args
            return Result.Failure<MemberId, RegisterMemberError>(
                enrollment.Error == MemberError.EmptyFullName
                    ? RegisterMemberError.InvalidName
                    : RegisterMemberError.InvalidMember);
        }

        await memberVectorRepository.SaveAsync(enrollment.Value, cancellationToken).ConfigureAwait(false);

        return Result.Success<MemberId, RegisterMemberError>(memberId);
    }

    private static RegisterMemberError MapExtractionError(FaceExtractionError error) => error switch
    {
        FaceExtractionError.NoFaceDetected => RegisterMemberError.NoFaceDetected,
        FaceExtractionError.MultipleFacesDetected => RegisterMemberError.MultipleFacesDetected,
        _ => RegisterMemberError.ExtractionFailed
    };
}
