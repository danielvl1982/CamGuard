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
}
