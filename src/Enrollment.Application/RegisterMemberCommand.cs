using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Registers a new member from identity data and one or more reference photos —
/// per <c>member-enrollment</c> spec's "Member Registration Input" requirement.
/// </summary>
public sealed record RegisterMemberCommand
{
    public RegisterMemberCommand(string fullName, IReadOnlyList<Stream> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        if (photos.Count == 0)
        {
            throw new ArgumentException("At least one reference photo is required.", nameof(photos));
        }

        FullName = fullName;
        Photos = photos;
    }

    public string FullName { get; }

    /// <summary>One or more reference photos, each validated independently by the face-detection gate.</summary>
    public IReadOnlyList<Stream> Photos { get; }
}

/// <summary>
/// Extracts every submitted photo's face embedding, enrolls the member, persists it,
/// and stores every photo's bytes plus per-photo embedding — per <c>member-enrollment</c>
/// spec's face-detection gate and atomic-persistence requirements plus member-crud T2
/// (image persistence). Every submitted photo must independently pass the face-detection
/// gate; if any photo fails (zero or multiple faces detected), the entire enrollment is
/// rejected and nothing is persisted for any of the submitted photos. Likewise, an
/// image-store failure fails the whole registration (<see cref="RegisterMemberError.ImageStoreFailed"/>)
/// BEFORE the member record is written, so no member is ever persisted without its images.
/// Submitted photo streams are copied, never disposed (the caller owns disposal, e.g.
/// <c>Register.razor</c>'s finally block), and are left positioned at 0.
/// </summary>
public sealed class RegisterMemberCommandHandler(
    IFaceEmbeddingExtractor faceEmbeddingExtractor,
    IMemberVectorRepository memberVectorRepository,
    IMemberImageRepository memberImageRepository)
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
        {
            return Result.Failure<MemberId, RegisterMemberError>(RegisterMemberError.InvalidMember);
        }

        // Persist only after every submitted photo passed the face-detection gate and the
        // member's invariants held — no partial record is ever written.
        // Images FIRST, then the member record: an image-store failure returns
        // ImageStoreFailed before SaveAsync is reached, so the member is NOT persisted
        // without its images. (A member-save failure afterwards propagates as today.)
        List<MemberImageId> storedImages = [];
        try
        {
            for (var i = 0; i < command.Photos.Count; i++)
            {
                await using var buffer = await CopyPhotoAsync(command.Photos[i], cancellationToken).ConfigureAwait(false);
                var imageId = await memberImageRepository.SaveAsync(
                    memberId, buffer, embeddings[i], DetectExtension(buffer), cancellationToken).ConfigureAwait(false);
                storedImages.Add(imageId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Best-effort compensation so a mid-batch failure leaves no partial images.
            foreach (var imageId in storedImages)
            {
                try
                {
                    await memberImageRepository.DeleteAsync(imageId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Compensation must not mask the original store failure.
                }
            }

            return Result.Failure<MemberId, RegisterMemberError>(RegisterMemberError.ImageStoreFailed);
        }

        await memberVectorRepository.SaveAsync(enrollment.Value, cancellationToken);

        return Result.Success<MemberId, RegisterMemberError>(memberId);
    }

    private static RegisterMemberError MapExtractionError(FaceExtractionError error) => error switch
    {
        FaceExtractionError.NoFaceDetected => RegisterMemberError.NoFaceDetected,
        FaceExtractionError.MultipleFacesDetected => RegisterMemberError.MultipleFacesDetected,
        _ => RegisterMemberError.ExtractionFailed
    };

    /// <summary>
    /// Copies a submitted photo into a readable buffer for image storage. The caller's
    /// stream is never disposed; when seekable it is rewound before AND after the copy
    /// so it is left positioned at 0, readable for any later use.
    /// </summary>
    private static async Task<MemoryStream> CopyPhotoAsync(Stream photo, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        if (photo.CanSeek)
        {
            photo.Position = 0;
        }

        await photo.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (photo.CanSeek)
        {
            photo.Position = 0;
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Sniffs the image container from magic bytes (the command carries bytes, not file
    /// names). Unknown content falls back to <c>.bin</c>, which the image repository accepts.
    /// </summary>
    private static string DetectExtension(MemoryStream buffer)
    {
        var bytes = buffer.GetBuffer();
        var length = (int)buffer.Length;

        if (length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ".png";
        }

        if (length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return ".gif";
        }

        if (length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return ".bmp";
        }

        if (length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return ".webp";
        }

        return ".bin";
    }
}

public enum RegisterMemberError
{
    NoFaceDetected,
    MultipleFacesDetected,
    ExtractionFailed,
    InvalidMember,
    InvalidName,
    ImageStoreFailed
}
