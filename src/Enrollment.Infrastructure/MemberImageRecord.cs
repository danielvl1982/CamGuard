using Enrollment.Domain;
using Microsoft.Extensions.VectorData;

namespace Enrollment.Infrastructure;

/// <summary>
/// Infrastructure-only vector-store record for one stored reference photo — kept
/// separate from the <see cref="MemberImage"/> read-model so Domain stays independent
/// of the (preview) vector-store package, mirroring <see cref="MemberVectorRecord"/>.
/// <see cref="Id"/> is a <see cref="string"/> for the same provider key-type reason.
/// The image bytes live on disk at <see cref="FilePath"/> (absolute); only the
/// per-photo embedding and metadata live in this collection (member-crud T2).
/// </summary>
internal sealed class MemberImageRecord
{
    [VectorStoreKey]
    public required string Id { get; init; }

    [VectorStoreData]
    public required string MemberId { get; init; }

    /// <summary>Absolute path of the image file on disk.</summary>
    [VectorStoreData]
    public required string FilePath { get; init; }

    /// <summary>UTC timestamp of storage, ISO 8601 round-trip (<c>O</c>) format.</summary>
    [VectorStoreData]
    public required string StoredAtUtc { get; init; }

    /// <summary>
    /// The embedding extracted from THIS photo — the per-image embedding T3's
    /// duplicate-face validation matches against (cosine distance, 0 = identical).
    /// </summary>
    [VectorStoreVector(FaceEmbedding.ExpectedDimension, DistanceFunction = DistanceFunction.CosineDistance)]
    public required float[] Embedding { get; init; }
}
