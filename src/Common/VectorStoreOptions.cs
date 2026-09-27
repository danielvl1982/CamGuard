namespace Common;

/// <summary>
/// Technical configuration for the local vector store used by <c>Enrollment.Infrastructure</c>'s
/// <c>SqliteVecMemberRepository</c>. Bound from configuration via <c>AddEnrollment(...)</c>
/// (see design.md "DI / Composition Root").
/// </summary>
public sealed class VectorStoreOptions
{
    /// <summary>Configuration section name this options type binds from (e.g. <c>appsettings.json</c>).</summary>
    public const string SectionName = "VectorStore";

    /// <summary>SQLite connection string for the vector-store database file.</summary>
    public string ConnectionString { get; set; } = "Data Source=camguard-enrollment.db";

    /// <summary>Name of the vector-store collection/table holding enrolled members' embeddings.</summary>
    public string MembersCollectionName { get; set; } = "members";

    /// <summary>
    /// Root directory for members' reference-photo files (member-crud T2). Image files live at
    /// <c>&lt;MemberImagesRoot&gt;/&lt;memberId&gt;/&lt;imageId&gt;.&lt;ext&gt;</c>. Kept OUT of any
    /// web root (<c>wwwroot</c>) — images are served by a dedicated endpoint (T5), never statically.
    /// </summary>
    public string MemberImagesRoot { get; set; } = "photos/members";

    /// <summary>Name of the vector-store collection/table holding per-image records (own embedding each).</summary>
    public string MemberImagesCollectionName { get; set; } = "member_images";

    /// <summary>
    /// Name of the plain SQLite table holding incident-log records (incident-log T2).
    /// A plain table — not a vector collection — because incidents need no vector
    /// search, only newest-first listing.
    /// </summary>
    public string IncidentsTableName { get; set; } = "incidents";
    /// <summary>
    /// Cosine-DISTANCE threshold for the cross-member duplicate-face gate (member-crud
    /// T3, OD2): a candidate embedding whose nearest stored image of ANOTHER member is
    /// strictly BELOW this distance is rejected as a duplicate. Same convention as
    /// <c>MemberVectorRecord.Embedding</c> — 0 = identical, 2 = opposite, so LOWER means
    /// more similar and a LOWER threshold is stricter (fewer false rejections).
    /// Default 0.4 (≈ cosine similarity 0.6): a conservative same-person cutoff for
    /// ArcFace-family embeddings, which typically place same-person pairs well below
    /// 0.4 distance and different-person pairs well above 0.6, leaving a wide gray
    /// zone — 0.4 stays on the strict side of it to avoid blocking legitimate
    /// registrations. OD2 CALIBRATION PENDING: tune against real photos (measure the
    /// actual same-person vs different-person distance distributions) before relying
    /// on this default in production.
    /// </summary>
    public double DuplicateFaceDistanceThreshold { get; set; } = 0.4;
}
