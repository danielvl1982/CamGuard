using Common;
using CommunityToolkit.VectorData.SqliteVec;
using CSharpFunctionalExtensions;
using Enrollment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using System.Diagnostics;
using System.Globalization;

namespace Enrollment.Infrastructure;

/// <summary>
/// <see cref="IMemberVectorRepository"/> adapter wrapping <c>CommunityToolkit.VectorData.SqliteVec</c>
/// — the only class in the solution referencing that preview package, per design.md's rollback
/// plan. Persists/queries <see cref="MemberVectorRecord"/>, mapped from/to <see cref="Member"/> by
/// <see cref="MemberVectorRecordMapper"/> (task 4.5).
/// </summary>
public sealed class SqliteVecMemberRepository : IMemberVectorRepository
{
    /// <summary>
    /// How many nearest candidates recognition inspects before giving up (OD1 filter depth).
    /// The nearest non-annulled member is found unless at least this many annulled members
    /// are ALL closer than it — in that case (or when every member is annulled) the query
    /// returns <c>null</c>, i.e. recognition denies, which is the fail-safe direction.
    /// </summary>
    private const int RecognitionSearchDepth = 10;

    private readonly SqliteCollection<string, MemberVectorRecord> _collection;
    private readonly string _connectionString;
    private readonly string _collectionName;
    private readonly string _rolesTableName;
    private readonly ActivitySource _activitySource;

    /// <summary>
    /// Caches that the schema has already been ensured for this repository instance.
    /// Safe as an instance-level (not static/shared) cache specifically because this
    /// type is registered Scoped (see <c>EnrollmentServiceCollectionExtensions</c>):
    /// each scope gets its own instance, so a schema check runs at most once per
    /// scope instead of once per public call.
    /// </summary>
    private bool _schemaEnsured;

    public SqliteVecMemberRepository(
        IOptions<VectorStoreOptions> options,
        [FromKeyedServices(EnrollmentServiceCollectionExtensions.ActivitySourceName)] ActivitySource activitySource)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _activitySource = activitySource;
        _connectionString = value.ConnectionString;
        _collectionName = value.MembersCollectionName;
        _rolesTableName = value.RolesTableName;
        _collection = new SqliteCollection<string, MemberVectorRecord>(value.ConnectionString, value.MembersCollectionName);
    }

    /// <summary>
    /// Ensures the vector table exists AND carries the <c>IsAnnulled</c> column.
    /// Probe finding (member-crud T1, verified against a real local SQLite store with
    /// CommunityToolkit.VectorData.SqliteVec 1.0.1-preview):
    /// <see cref="SqliteCollection{TKey, TRecord}.EnsureCollectionExistsAsync"/> is
    /// CREATE TABLE IF NOT EXISTS — it does NOT add a column to an EXISTING table.
    /// Adding <c>IsAnnulled</c> to the record and calling Ensure alone leaves legacy
    /// tables without the column, and every Upsert/Get then fails with
    /// "SQLite Error 1: 'table ... has no column named IsAnnulled'".
    /// Raw ALTER TABLE via Microsoft.Data.Sqlite (already referenced by this project —
    /// no new package) is therefore required. The provider stores a bool
    /// [VectorStoreData] property as "IsAnnulled" INTEGER NOT NULL; the migration uses
    /// NOT NULL DEFAULT 0 so pre-existing rows read back as non-annulled
    /// (verified: legacy row round-trips with IsAnnulled=False, vector search works).
    /// </summary>
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await _collection.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync("IsAnnulled", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        // Nullable: existing members simply have no role until one is assigned.
        await EnsureColumnAsync("RoleId", "TEXT NULL", cancellationToken).ConfigureAwait(false);

        _schemaEnsured = true;
    }

    private async Task EnsureColumnAsync(string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        // Identifiers cannot be parameterized — quote the configured collection name
        // (it is the physical table name, verified empirically) and escape quotes.
        // NOTE: the table-valued pragma requires the name as a SINGLE-quoted string
        // literal; double quotes are parsed as an identifier and fail with
        // 'no such column ... should this be a string literal in single-quotes?'.
        var quotedTable = "\"" + _collectionName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var pragmaTableArg = "'" + _collectionName.Replace("'", "''", StringComparison.Ordinal) + "'";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info({pragmaTableArg}) WHERE name = '{columnName}'";
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

    public async Task SaveAsync(Member member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.Save");
        var startTimestamp = Stopwatch.GetTimestamp();

        await EnsureSchemaAsync(cancellationToken);
        await _collection.UpsertAsync(MemberVectorRecordMapper.FromMember(member), cancellationToken);

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }

    public async Task SetAnnulledAsync(MemberId id, bool isAnnulled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.SetAnnulled");
        activity?.SetTag("member.annulled", isAnnulled);

        await EnsureSchemaAsync(cancellationToken);

        // Vectors must be materialized: ToMember requires a validated FaceEmbedding,
        // and the re-upsert below must preserve the member's existing embedding.
        var retrieval = new RecordRetrievalOptions { IncludeVectors = true };
        var record = await _collection.GetAsync(id.Value.ToString(), retrieval, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            throw new KeyNotFoundException($"No enrolled member with id '{id.Value}'.");
        }

        var current = MemberVectorRecordMapper.ToMember(record);
        var updated = Member.Rehydrate(current.Id, current.FullName, current.Embedding, isAnnulled, current.RoleId);
        await _collection.UpsertAsync(MemberVectorRecordMapper.FromMember(updated), cancellationToken);
    }

    public async Task<RoleAssignmentResult> SetRoleAsync(MemberId id, RoleId? roleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.SetRole");

        await EnsureSchemaAsync(cancellationToken);

        // RoleId is a plain column, so a single conditional UPDATE writes only it (the embedding is
        // untouched) and checks the role still exists in the same statement: SQLite serializes it
        // against a concurrent role delete, so a dangling id can never be stored.
        var quotedMembers = Quote(_collectionName);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var update = connection.CreateCommand();
        update.CommandText =
            $"UPDATE {quotedMembers} SET \"RoleId\" = @role WHERE \"Id\" = @id " +
            $"AND (@role IS NULL OR EXISTS (SELECT 1 FROM {Quote(_rolesTableName)} WHERE \"Id\" = @role))";
        update.Parameters.AddWithValue("@id", id.Value.ToString());
        update.Parameters.AddWithValue("@role", roleId is null ? DBNull.Value : roleId.Value.ToString());

        int changed;
        try
        {
            changed = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (roleId is not null && ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            // The roles table is only created with the first saved role: no role can exist yet.
            changed = 0;
        }

        if (changed > 0)
        {
            return RoleAssignmentResult.Assigned;
        }

        // Nothing updated: the member is unknown, or the role vanished.
        await using var exists = connection.CreateCommand();
        exists.CommandText = $"SELECT EXISTS(SELECT 1 FROM {quotedMembers} WHERE \"Id\" = @id)";
        exists.Parameters.AddWithValue("@id", id.Value.ToString());
        return Convert.ToInt32(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1
            ? RoleAssignmentResult.RoleNotFound
            : throw new KeyNotFoundException($"No enrolled member with id '{id.Value}'.");
    }

    private static string Quote(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public async Task RenameAsync(MemberId id, string fullName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.Rename");

        await EnsureSchemaAsync(cancellationToken);

        // Vectors must be materialized so the re-upsert preserves the existing embedding.
        var retrieval = new RecordRetrievalOptions { IncludeVectors = true };
        var record = await _collection.GetAsync(id.Value.ToString(), retrieval, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            throw new KeyNotFoundException($"No enrolled member with id '{id.Value}'.");
        }

        // The blank-name guard above makes a Rename failure unreachable.
        var member = MemberVectorRecordMapper.ToMember(record);
        member.Rename(fullName);
        await _collection.UpsertAsync(MemberVectorRecordMapper.FromMember(member), cancellationToken);
    }

    public async Task<(MemberId Id, double Score)?> FindNearestAsync(FaceEmbedding query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.Query");
        var startTimestamp = Stopwatch.GetTimestamp();

        await EnsureSchemaAsync(cancellationToken);

        float[] queryVector = [.. query.Vector];

        // OD1: an annulled member must never be returned by recognition. A plain
        // SearchAsync(top: 1) can return an annulled top-1, so fetch a small top-K
        // (search-result records carry their data fields — IsAnnulled included,
        // verified empirically) and return the first non-annulled candidate.
        await foreach (var result in
            _collection.SearchAsync(queryVector, top: RecognitionSearchDepth, cancellationToken: cancellationToken))
        {
            if (result.Record.IsAnnulled)
            {
                continue;
            }

            activity?.SetTag("match.score", result.Score);
            activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            // Defensive: a record whose stored key is not a valid, non-empty Guid should never
            // surface as a match rather than throw — it indicates a corrupted/foreign record.
            var memberId = Guid.TryParse(result.Record.Id, out var parsed)
                ? MemberId.From(parsed)
                : Result.Failure<MemberId, MemberIdError>(MemberIdError.Empty);

            if (memberId.IsSuccess)
            {
                return (memberId.Value, result.Score ?? 0d);
            }
        }

        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        return null;
    }

    /// <summary>
    /// Point lookup by id for the duplicate-face gate's name resolution (member-crud
    /// T3): returns annulled members too (OD2 matches against their images, so their
    /// names must resolve), unlike <see cref="ListAllAsync"/> which hides them.
    /// </summary>
    public async Task<Member?> FindByIdAsync(MemberId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.FindById");

        await EnsureSchemaAsync(cancellationToken);

        // Vectors must be materialized: the mapper requires a validated FaceEmbedding.
        var retrieval = new RecordRetrievalOptions { IncludeVectors = true };
        var record = await _collection.GetAsync(id.Value.ToString(), retrieval, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return null;
        }

        return MemberVectorRecordMapper.ToMember(record);
    }

    public async Task<Member?> FindByFullNameAsync(string fullName, CancellationToken cancellationToken)    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var normalized = MemberVectorRecordMapper.Normalize(fullName);

        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.FindByFullName");
        activity?.SetTag("lookup.fullName", normalized);

        await EnsureSchemaAsync(cancellationToken);

        var options = new FilteredRecordRetrievalOptions<MemberVectorRecord> { IncludeVectors = true };

        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(MemberVectorRecordMapper.Normalize(record.FullName), normalized, StringComparison.OrdinalIgnoreCase))
            {
                activity?.SetTag("lookup.found", true);
                return MemberVectorRecordMapper.ToMember(record);
            }
        }

        activity?.SetTag("lookup.found", false);
        return null;
    }

    public async Task<IReadOnlyList<Member>> ListAllAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Enrollment.VectorStore.ListAll");
        var startTimestamp = Stopwatch.GetTimestamp();

        // Required: enumerating a collection that was never created throws VectorStoreException
        // (verified against CommunityToolkit.VectorData.SqliteVec 1.0.1-preview).
        await EnsureSchemaAsync(cancellationToken);

        // Vectors must be materialized even though a directory only displays identity fields:
        // IncludeVectors=false returns records with a null Embedding, and Member.Rehydrate
        // (via MemberVectorRecordMapper.ToMember) requires a validated FaceEmbedding.
        var options = new FilteredRecordRetrievalOptions<MemberVectorRecord> { IncludeVectors = true };

        var members = new List<Member>();

        await foreach (var record in _collection.GetAsync(r => true, int.MaxValue, options, cancellationToken).ConfigureAwait(false))
        {
            // OD1: annulled members are hidden from the directory list.
            if (!record.IsAnnulled)
            {
                members.Add(MemberVectorRecordMapper.ToMember(record));
            }
        }

        activity?.SetTag("count", members.Count);
        activity?.SetTag("latency", Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

        return members;
    }
}
