using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>
/// Best (nearest) cross-member image match found by
/// <see cref="IMemberImageDuplicateValidator"/>.
/// </summary>
/// <param name="MatchedMemberId">Owner of the matched stored image.</param>
/// <param name="Distance">
/// Cosine distance between the candidate and the matched image's embedding
/// (0 = identical, same convention as the vector-store records).
/// </param>
public sealed record DuplicateFaceMatch(MemberId MatchedMemberId, double Distance);

/// <summary>
/// Shared duplicate-face validation seam for alta (registration) and adición (T7
/// add-image): matches a candidate embedding against OTHER members' stored image
/// embeddings. Implemented by <see cref="MemberImageDuplicateValidator"/>.
/// </summary>
public interface IMemberImageDuplicateValidator
{
    /// <summary>
    /// Returns the nearest stored image embedding belonging to a DIFFERENT member
    /// than <paramref name="ownerMemberId"/> whose cosine distance to
    /// <paramref name="candidate"/> is strictly below the configured threshold, or
    /// <c>null</c> when there is no such match. Pass <c>null</c> for
    /// <paramref name="ownerMemberId"/> to exclude nobody (alta: the registering
    /// member does not exist yet); T7 passes the image owner's id so the member's
    /// own other images never trigger the gate. Images carry no annul semantics,
    /// so annulled members' images participate in matching (OD2).
    /// </summary>
    Task<DuplicateFaceMatch?> FindMatchAsync(
        FaceEmbedding candidate,
        MemberId? ownerMemberId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Client-side enumeration over <see cref="IMemberImageRepository.ListAllAsync"/>
/// (which materializes every image's embedding) — the same pattern T1 uses for
/// name lookup. Depends only on the domain port plus a plain threshold value, so
/// Application stays free of options-binding types; the threshold is read from
/// <c>VectorStoreOptions.DuplicateFaceDistanceThreshold</c> at the composition
/// root (<c>AddEnrollment</c>).
/// </summary>
public sealed class MemberImageDuplicateValidator(
    IMemberImageRepository memberImageRepository,
    double distanceThreshold) : IMemberImageDuplicateValidator
{
    public async Task<DuplicateFaceMatch?> FindMatchAsync(
        FaceEmbedding candidate,
        MemberId? ownerMemberId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(memberImageRepository);

        var images = await memberImageRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        DuplicateFaceMatch? best = null;
        foreach (var image in images)
        {
            if (ownerMemberId is not null && image.MemberId == ownerMemberId)
            {
                continue;
            }

            var distance = CosineDistance(candidate.Vector, image.Embedding.Vector);
            if (best is null || distance < best.Distance)
            {
                best = new DuplicateFaceMatch(image.MemberId, distance);
            }
        }

        return best is not null && best.Distance < distanceThreshold ? best : null;
    }

    /// <summary>
    /// Cosine distance = 1 − cosine similarity (0 = identical, 2 = opposite),
    /// matching the <c>CosineDistance</c> convention of the vector-store records.
    /// </summary>
    internal static double CosineDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var dot = 0.0;
        var leftNormSquared = 0.0;
        var rightNormSquared = 0.0;
        for (var i = 0; i < left.Count; i++)
        {
            dot += (double)left[i] * right[i];
            leftNormSquared += (double)left[i] * left[i];
            rightNormSquared += (double)right[i] * right[i];
        }

        return 1.0 - dot / (Math.Sqrt(leftNormSquared) * Math.Sqrt(rightNormSquared));
    }
}
