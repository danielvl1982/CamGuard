using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Adds reference photos to an existing member from the detail page
/// (<c>/members/{id}</c>, member-crud T7): extracts every photo's face embedding,
/// runs the shared T3 duplicate-face gate against OTHER members' stored images
/// (owner = the displayed member, so the member's own other images are allowed),
/// then persists via the T2 image store. All-or-nothing: any extraction failure,
/// duplicate match, or store failure persists NOTHING for the submission
/// (validate all before storing any, T3 alta precedent).
/// Submitted photo streams are copied, never disposed (the caller owns disposal),
/// and are left positioned at 0.
/// </summary>
public sealed record AddMemberImagesCommand
{
    public AddMemberImagesCommand(MemberId memberId, IReadOnlyList<Stream> photos)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(photos);
        if (photos.Count == 0)
        {
            throw new ArgumentException("At least one photo is required.", nameof(photos));
        }

        MemberId = memberId;
        Photos = photos;
    }

    public MemberId MemberId { get; }

    /// <summary>One or more photos to add, each validated independently by the face-detection gate.</summary>
    public IReadOnlyList<Stream> Photos { get; }
}

public sealed class AddMemberImagesCommandHandler(
    IFaceEmbeddingExtractor faceEmbeddingExtractor,
    IMemberVectorRepository memberVectorRepository,
    IMemberImageRepository memberImageRepository,
    IMemberImageDuplicateValidator duplicateValidator)
{
    public async Task<Result<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>> HandleAsync(
        AddMemberImagesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await memberVectorRepository.FindByIdAsync(command.MemberId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>(
                AddMemberImagesError.MemberNotFound);
        }

        var embeddings = new List<FaceEmbedding>(command.Photos.Count);
        foreach (var photo in command.Photos)
        {
            var extraction = await faceEmbeddingExtractor.ExtractAsync(photo, cancellationToken)
                .ConfigureAwait(false);
            if (extraction.IsFailure)
            {
                // Atomic all-or-nothing gate: the first photo that fails face
                // detection rejects the whole submission — SaveAsync is never
                // reached, so no image is persisted for any submitted photo.
                return Result.Failure<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>(
                    MapExtractionError(extraction.Error));
            }

            embeddings.Add(extraction.Value);
        }

        // Duplicate-face gate (member-crud T3, OD2): every submitted photo's
        // embedding is matched against OTHER members' stored images — including
        // annulled members' images — BEFORE anything is persisted. The owner is
        // the displayed member, so its own other images never trigger the gate.
        // On a match the whole submission fails and NOTHING is persisted.
        // The stored-image list is fetched ONCE for the whole command and reused
        // across every submitted photo, instead of once per photo.
        var existingImages = await memberImageRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var embedding in embeddings)
        {
            var match = await duplicateValidator.FindMatchAsync(embedding, existingImages, command.MemberId, cancellationToken)
                .ConfigureAwait(false);
            if (match is not null)
            {
                var matchedMember = await memberVectorRepository.FindByIdAsync(match.MatchedMemberId, cancellationToken)
                    .ConfigureAwait(false);
                var matchedName = matchedMember?.FullName ?? match.MatchedMemberId.Value.ToString();
                return Result.Failure<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>(
                    new AddMemberImagesFailure(AddMemberImagesError.MatchesOtherMember, matchedName));
            }
        }

        // Persist only after every photo passed extraction and the duplicate
        // gate — no partial image is ever written on validation failure.
        List<MemberImageId> storedImages = [];
        try
        {
            for (var i = 0; i < command.Photos.Count; i++)
            {
                await using var buffer = await CopyPhotoAsync(command.Photos[i], cancellationToken).ConfigureAwait(false);
                var imageId = await memberImageRepository.SaveAsync(
                    command.MemberId, buffer, embeddings[i], DetectExtension(buffer), cancellationToken).ConfigureAwait(false);
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

            return Result.Failure<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>(
                AddMemberImagesError.ImageStoreFailed);
        }

        return Result.Success<IReadOnlyList<MemberImageId>, AddMemberImagesFailure>(storedImages);
    }

    private static AddMemberImagesError MapExtractionError(FaceExtractionError error) => error switch
    {
        FaceExtractionError.NoFaceDetected => AddMemberImagesError.NoFaceDetected,
        FaceExtractionError.MultipleFacesDetected => AddMemberImagesError.MultipleFacesDetected,
        _ => AddMemberImagesError.ExtractionFailed
    };

    /// <summary>
    /// Copies a submitted photo into a readable buffer for image storage. The caller's
    /// stream is never disposed; when seekable it is rewound before AND after the copy
    /// so it is left positioned at 0, readable for any later use (T2 precedent).
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

public enum AddMemberImagesError
{
    MemberNotFound,
    NoFaceDetected,
    MultipleFacesDetected,
    ExtractionFailed,
    ImageStoreFailed,
    /// <summary>
    /// A submitted photo's face matches ANOTHER member's stored image (member-crud
    /// T3, OD2 — annulled members' images participate). The matched member's display
    /// name travels on <see cref="AddMemberImagesFailure.MatchedMemberName"/> so the
    /// UI can name them; nothing is persisted.
    /// </summary>
    MatchesOtherMember
}

/// <summary>
/// Failure payload for <c>AddMemberImagesCommandHandler</c>: the error kind plus,
/// for <see cref="AddMemberImagesError.MatchesOtherMember"/>, the matched member's
/// display name (resolved via the member repository, annulled members included).
/// Implicitly convertible from <see cref="AddMemberImagesError"/> so gate failures
/// without a payload stay one-liners.
/// </summary>
public sealed record AddMemberImagesFailure(AddMemberImagesError Error, string? MatchedMemberName = null)
{
    public static implicit operator AddMemberImagesFailure(AddMemberImagesError error) => new(error);
}
