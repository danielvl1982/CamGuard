using System.Diagnostics;
using Common;
using CommunityToolkit.VectorData.SqliteVec;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace Enrollment.UnitTests;

/// <summary>
/// Covers the member side of roles (member-roles-notifications T2): the domain assignment, its
/// persistence, preservation across rename/annul, and upgrading a members table that predates the
/// <c>RoleId</c> column.
/// </summary>
public sealed class MemberRoleTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVecMemberRepository _repository;
    private readonly SqliteRoleRepository _roles;

    public MemberRoleTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-member-role-{Guid.NewGuid():N}.db");
        var options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteVecMemberRepository(options, new ActivitySource("Enrollment.UnitTests.MemberRole"));
        _roles = new SqliteRoleRepository(options, new ActivitySource("Enrollment.UnitTests.MemberRole.Roles"));
    }

    /// <summary>Persists a role: assignment now only accepts roles that exist.</summary>
    private async Task<RoleId> SaveRoleAsync(string name = "Family")
    {
        var role = Role.Create(RoleId.New(), name, []).Value;
        await _roles.SaveAsync(role, CancellationToken.None);
        return role.Id;
    }

    private static FaceEmbedding ValidEmbedding(float first = 0.8f)
    {
        var v = new float[FaceEmbedding.ExpectedDimension];
        v[0] = first;
        return FaceEmbedding.FromVector(v).Value;
    }

    private static Member NewMember() => Member.Enroll(MemberId.New(), "Ada Lovelace", ValidEmbedding()).Value;

    [Fact]
    public void AssignRole_sets_and_clears_the_role_and_new_members_have_none()
    {
        var member = NewMember();
        var roleId = RoleId.New();
        Assert.Null(member.RoleId);

        member.AssignRole(roleId);
        Assert.Equal(roleId, member.RoleId);

        member.AssignRole(null);
        Assert.Null(member.RoleId);
    }

    [Fact]
    public async Task SetRoleAsync_persists_role_and_preserves_name_embedding_and_annulled_flag()
    {
        var member = NewMember();
        var roleId = await SaveRoleAsync();
        await _repository.SaveAsync(member, CancellationToken.None);

        Assert.Equal(RoleAssignmentResult.Assigned, await _repository.SetRoleAsync(member.Id, roleId, CancellationToken.None));

        var found = await _repository.FindByIdAsync(member.Id, CancellationToken.None);
        Assert.Equal(roleId, found!.RoleId);
        Assert.Equal("Ada Lovelace", found.FullName);
        Assert.False(found.IsAnnulled);
        Assert.Equal(member.Embedding.Vector.ToArray(), found.Embedding.Vector.ToArray());
    }

    [Fact]
    public async Task SetRoleAsync_null_clears_the_role()
    {
        var member = NewMember();
        await _repository.SaveAsync(member, CancellationToken.None);
        await _repository.SetRoleAsync(member.Id, await SaveRoleAsync(), CancellationToken.None);

        await _repository.SetRoleAsync(member.Id, null, CancellationToken.None);

        Assert.Null((await _repository.FindByIdAsync(member.Id, CancellationToken.None))!.RoleId);
    }

    [Fact]
    public async Task SetRoleAsync_null_succeeds_when_the_roles_table_was_never_created()
    {
        // No role was ever saved, so the roles table does not exist yet: clearing must not touch it.
        var member = NewMember();
        await _repository.SaveAsync(member, CancellationToken.None);

        var result = await _repository.SetRoleAsync(member.Id, null, CancellationToken.None);

        Assert.Equal(RoleAssignmentResult.Assigned, result);
        Assert.Null((await _repository.FindByIdAsync(member.Id, CancellationToken.None))!.RoleId);
    }

    [Fact]
    public async Task SetRoleAsync_throws_for_unknown_member()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _repository.SetRoleAsync(MemberId.New(), RoleId.New(), CancellationToken.None));
    }

    [Fact]
    public async Task Role_survives_rename_annul_and_restore()
    {
        var member = NewMember();
        var roleId = await SaveRoleAsync();
        await _repository.SaveAsync(member, CancellationToken.None);
        await _repository.SetRoleAsync(member.Id, roleId, CancellationToken.None);

        await _repository.RenameAsync(member.Id, "Grace Hopper", CancellationToken.None);
        await _repository.SetAnnulledAsync(member.Id, true, CancellationToken.None);
        Assert.Equal(roleId, (await _repository.FindByIdAsync(member.Id, CancellationToken.None))!.RoleId);

        await _repository.SetAnnulledAsync(member.Id, false, CancellationToken.None);
        var found = await _repository.FindByIdAsync(member.Id, CancellationToken.None);
        Assert.Equal(roleId, found!.RoleId);
        Assert.Equal("Grace Hopper", found.FullName);
    }

    /// <summary>Row shape from before roles: has the annulled flag but no <c>RoleId</c> column.</summary>
    private sealed class PreRolesMemberRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = "";
        [VectorStoreData]
        public string FullName { get; set; } = "";
        [VectorStoreData]
        public bool IsAnnulled { get; set; }
        [VectorStoreVector(FaceEmbedding.ExpectedDimension, DistanceFunction = DistanceFunction.CosineDistance)]
        public float[] Embedding { get; set; } = new float[FaceEmbedding.ExpectedDimension];
    }

    [Fact]
    public async Task Existing_members_table_without_RoleId_column_upgrades_without_data_loss()
    {
        var legacyCollection = new SqliteCollection<string, PreRolesMemberRecord>($"Data Source={_dbPath}", "members_test");
        await legacyCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        var id = MemberId.New();
        var embedding = ValidEmbedding(0.7f);
        await legacyCollection.UpsertAsync(new PreRolesMemberRecord
        {
            Id = id.Value.ToString(),
            FullName = "Legacy Member",
            IsAnnulled = false,
            Embedding = [.. embedding.Vector]
        }, CancellationToken.None);

        var members = await _repository.ListAllAsync(CancellationToken.None);

        var legacy = Assert.Single(members);
        Assert.Equal(id, legacy.Id);
        Assert.Equal("Legacy Member", legacy.FullName);
        Assert.Null(legacy.RoleId);
        var match = await _repository.FindNearestAsync(embedding, CancellationToken.None);
        Assert.Equal(id, match!.Value.Id);

        var roleId = await SaveRoleAsync();
        await _repository.SetRoleAsync(id, roleId, CancellationToken.None);
        Assert.Equal(roleId, (await _repository.FindByIdAsync(id, CancellationToken.None))!.RoleId);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }
}
