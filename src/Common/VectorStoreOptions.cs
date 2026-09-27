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
}
