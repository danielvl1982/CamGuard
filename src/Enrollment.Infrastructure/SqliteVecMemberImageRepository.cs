using Common;
using CommunityToolkit.VectorData.SqliteVec;
using Enrollment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using System.Diagnostics;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="IMemberImageRepository"/> adapter: image bytes go to files under
/// <c>&lt;MemberImagesRoot&gt;/&lt;memberId&gt;/&lt;imageId&gt;.&lt;ext&gt;</c> while
/// per-photo embeddings + metadata go to a dedicated SqliteVec collection — the only
/// class besides <see cref="SqliteVecMemberRepository"/> referencing that preview
/// package. No annul gating: images are evidence, not access (member-crud T2).
/// </summary>
public sealed class SqliteVecMemberImageRepository : IMemberImageRepository
{
    private readonly SqliteCollection<string, MemberImageRecord> _collection;
    private readonly string _connectionString;
    private readonly string _collectionName;
    private readonly string _imageRoot;
    private readonly ActivitySource _activitySource;

    /// <summary>
    /// Caches that the schema has already been ensured for this repository instance.
    /// Safe as an instance-level (not static/shared) cache specifically because this
    /// type is registered Scoped (see <c>EnrollmentServiceCollectionExtensions</c>):
    /// each scope gets its own instance, so a schema check runs at most once per
    /// scope instead of once per public call.
    /// </summary>
    private bool _schemaEnsured;

    public SqliteVecMemberImageRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _connectionString = value.ConnectionString;
        _collectionName = value.MemberImagesCollectionName;
        _imageRoot = value.MemberImagesRoot;
        _collection = new SqliteCollection<string, MemberImageRecord>(value.ConnectionString, value.MemberImagesCollectionName);
    }

    /// <summary>
    /// Ensures the image collection exists AND carries every data column, reusing the
    /// member-crud T1 probe finding: <c>EnsureCollectionExistsAsync</c> is CREATE TABLE
    /// IF NOT EXISTS and never adds columns, so each data column is migrated with raw
    /// ALTER TABLE via Microsoft.Data.Sqlite, guarded by single-quoted
    /// <c>pragma_table_info</c> + duplicate-column race catch.
    /// </summary>
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync("MemberId", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync("FilePath", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync("StoredAtUtc", "TEXT NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);

        _schemaEnsured = true;
    }

    private async Task EnsureColumnAsync(string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        // Identifiers cannot be parameterized — quote the configured collection name
        // (it is the physical table name) and escape quotes. The table-valued pragma
        // requires the name as a SINGLE-quoted string literal (see SqliteVecMemberRepository).
        var quotedTable = "\"" + _collectionName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var pragmaTableArg = "'" + _collectionName.Replace("'", "''", StringComparison.Ordinal) + "'";
        var columnArg = "'" + columnName.Replace("'", "''", StringComparison.Ordinal) + "'";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info({pragmaTableArg}) WHERE name = {columnArg}";
            var exists = Convert.ToInt32(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
            if (exists)
            {
                return;
            }
        }

        await using (var alter = connection.CreateCommand())
        {
            alter.CommandText = $"ALTER TABLE {quotedTable} ADD COLUMN \"{columnName}\" {columnDefinition}";
            try
            {
                await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
                // Lost a race with another process migrating the same table — the column
                // now exists, so there is nothing left to do.
            }
        }
    }

    public async Task<MemberImageId> SaveAsync(
        MemberId memberId,
        Stream imageBytes,
        FaceEmbedding embedding,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(embedding);

        using var activity = _activitySource.StartActivity("Enrollment.ImageStore.Save");
        var startTimestamp = Stopwatch.GetTimestamp();

        var imageId = MemberImageId.New();
        var extension = NormalizeExtension(fileExtension);
        var memberDirectory = Path.Combine(_imageRoot, memberId.Value.ToString());
        var filePath = Path.GetFullPath(Path.Combine(memberDirectory, imageId.Value.ToString() + extension));

        Directory.CreateDirectory(memberDirectory);

        // Copy the caller's stream without disposing it; read from its current
        // position so a pre-positioned stream just works.
        await using (var file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await imageBytes.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            var image = MemberImage.Create(imageId, memberId, Path.GetFileName(filePath), embedding, DateTime.UtcNow).Value;
            await _collection.UpsertAsync(MemberImageRecordMapper.FromImage(image, filePath), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Record write failed after the file landed: remove the orphan file
            // (best-effort — the original error is what the caller must see).
            try { File.Delete(filePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        return imageId;
    }

    public async Task<IReadOnlyList<MemberImage>> ListByMemberAsync(MemberId memberId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        using var activity = _activitySource.StartActivity("Enrollment.ImageStore.ListByMember");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        // Vectors must be materialized: MemberImage carries the per-photo embedding
        // (T3 matches against it), and the mapper requires a validated FaceEmbedding.
        var options = new FilteredRecordRetrievalOptions<MemberImageRecord> { IncludeVectors = true };
        var memberKey = memberId.Value.ToString();

        var images = new List<MemberImage>();
        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(record.MemberId, memberKey, StringComparison.OrdinalIgnoreCase))
            {
                images.Add(MemberImageRecordMapper.ToImage(record));
            }
        }

        images.Sort(static (a, b) => a.StoredAtUtc.CompareTo(b.StoredAtUtc));

        activity?.SetTag("count", images.Count);
        return images;
    }

    public async Task<IReadOnlyList<MemberImage>> ListAllAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.ImageStore.ListAll");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var options = new FilteredRecordRetrievalOptions<MemberImageRecord> { IncludeVectors = true };

        var images = new List<MemberImage>();
        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            images.Add(MemberImageRecordMapper.ToImage(record));
        }

        activity?.SetTag("count", images.Count);
        return images;
    }

    public async Task DeleteAsync(MemberImageId imageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageId);

        using var activity = _activitySource.StartActivity("Enrollment.ImageStore.Delete");

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var record = await _collection.GetAsync(imageId.Value.ToString(), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            throw new KeyNotFoundException($"No stored image with id '{imageId.Value}'.");
        }

        // File first, then record: a file-delete failure surfaces with the record
        // intact (nothing silently orphaned). File.Delete on a missing file is a
        // silent no-op, so a manually removed file still deletes cleanly.
        File.Delete(record.FilePath);
        await _collection.DeleteAsync(imageId.Value.ToString(), cancellationToken).ConfigureAwait(false);
    }

    internal static string NormalizeExtension(string fileExtension) =>
        string.IsNullOrWhiteSpace(fileExtension)
            ? ".bin"
            : "." + fileExtension.Trim().TrimStart('.').ToLowerInvariant();
}
