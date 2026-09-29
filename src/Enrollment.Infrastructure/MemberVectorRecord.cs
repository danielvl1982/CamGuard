using Enrollment.Domain;
using Microsoft.Extensions.VectorData;

namespace Enrollment.Infrastructure;

/// <summary>
/// Infrastructure-only vector-store record for an enrolled member's face embedding — kept
/// separate from the <c>Member</c> aggregate so Domain stays independent of the (preview)
/// vector-store package, per design.md's "Domain entity persistence mapping" decision.
/// <see cref="Id"/> is a <see cref="string"/> because <c>SqliteCollection&lt;TKey,TRecord&gt;</c>
/// only supports <see cref="string"/>/<see cref="int"/>/<see cref="long"/> keys — <see cref="Member"/>'s
/// <see cref="MemberId"/> wraps a <see cref="Guid"/>, so it is stored as its string form.
/// </summary>
internal sealed class MemberVectorRecord
{
    [VectorStoreKey]
    public required string Id { get; init; }

    [VectorStoreData]
    public required string FullName { get; init; }

    /// <summary>
    /// Soft-delete flag ("anulado", member-crud T1). Stored as INTEGER 0/1 by the
    /// SqliteVec provider. Pre-annulment rows lack this column entirely and are
    /// backfilled to 0 (non-annulled) by the repository's ALTER TABLE migration —
    /// see <c>SqliteVecMemberRepository</c> for the probe findings.
    /// </summary>
    [VectorStoreData]
    public required bool IsAnnulled { get; init; }

    /// <summary>
    /// Assigned role id in string form, or <c>null</c> when unassigned. Legacy tables get this
    /// column through the repository's ALTER TABLE migration (nullable, no backfill needed).
    /// </summary>
    [VectorStoreData]
    public string? RoleId { get; init; }

    /// <summary>
    /// Cosine distance (0 = identical, 2 = opposite) — the standard comparison convention for
    /// ArcFace-family embeddings. Resolves design.md's open "distance/similarity metric"
    /// question for this phase.
    /// </summary>
    [VectorStoreVector(FaceEmbedding.ExpectedDimension, DistanceFunction = DistanceFunction.CosineDistance)]
    public required float[] Embedding { get; init; }
}
