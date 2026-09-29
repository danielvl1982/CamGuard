using System.Diagnostics;
using Common;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>Integration tests for <see cref="SqliteRoleRepository"/> against a temp SQLite file.</summary>
public sealed class SqliteRoleRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IOptions<VectorStoreOptions> _options;
    private readonly SqliteRoleRepository _repository;

    public SqliteRoleRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-roles-{Guid.NewGuid():N}.db");
        _options = Options.Create(new VectorStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            MembersCollectionName = "members_test"
        });
        _repository = new SqliteRoleRepository(_options, new ActivitySource("Enrollment.UnitTests.Roles"));
    }

    private static Role NewRole(string name, params NotificationChannel[] channels) =>
        Role.Create(RoleId.New(), name, channels).Value;

    private long CountRows(string table)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public async Task Round_trip_persists_name_and_channels_and_finds_by_id_and_name()
    {
        var role = NewRole("Family", NotificationChannel.Push, NotificationChannel.Email);
        await _repository.SaveAsync(role, CancellationToken.None);

        var byId = await _repository.FindByIdAsync(role.Id, CancellationToken.None);
        var byName = await _repository.FindByNameAsync("  fAMILY ", CancellationToken.None);

        Assert.NotNull(byId);
        Assert.Equal("Family", byId!.Name);
        Assert.Equal(role.Channels.OrderBy(c => c), byId.Channels.OrderBy(c => c));
        Assert.Equal(role.Id, byName?.Id);
    }

    [Fact]
    public async Task Unknown_lookups_return_null()
    {
        Assert.Null(await _repository.FindByIdAsync(RoleId.New(), CancellationToken.None));
        Assert.Null(await _repository.FindByNameAsync("Nobody", CancellationToken.None));
    }

    [Fact]
    public async Task Role_without_channels_round_trips_as_empty_set()
    {
        var role = NewRole("Guest");
        await _repository.SaveAsync(role, CancellationToken.None);

        var found = await _repository.FindByIdAsync(role.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Empty(found!.Channels);
    }

    [Fact]
    public async Task Saving_again_replaces_name_and_channels()
    {
        var role = NewRole("Family", NotificationChannel.Push, NotificationChannel.Email);
        await _repository.SaveAsync(role, CancellationToken.None);

        role.Rename("Relatives");
        role.SetChannels([NotificationChannel.Email]);
        await _repository.SaveAsync(role, CancellationToken.None);

        var found = await _repository.FindByIdAsync(role.Id, CancellationToken.None);
        Assert.Equal("Relatives", found!.Name);
        Assert.Equal([NotificationChannel.Email], found.Channels);
        Assert.Equal(1, CountRows("roles"));
        Assert.Equal(1, CountRows("role_channels"));
    }

    [Fact]
    public async Task Role_names_are_unique_ignoring_case()
    {
        await _repository.SaveAsync(NewRole("Family"), CancellationToken.None);

        await Assert.ThrowsAsync<SqliteException>(() =>
            _repository.SaveAsync(NewRole("FAMILY"), CancellationToken.None));
    }

    [Fact]
    public async Task ListAllAsync_returns_roles_with_their_channels_ordered_by_name()
    {
        await _repository.SaveAsync(NewRole("Zed", NotificationChannel.Push), CancellationToken.None);
        await _repository.SaveAsync(NewRole("Alpha", NotificationChannel.Push, NotificationChannel.Email), CancellationToken.None);
        await _repository.SaveAsync(NewRole("Mid"), CancellationToken.None);

        var roles = await _repository.ListAllAsync(CancellationToken.None);

        Assert.Equal(["Alpha", "Mid", "Zed"], roles.Select(r => r.Name));
        Assert.Equal(2, roles[0].Channels.Count);
        Assert.Empty(roles[1].Channels);
        Assert.Single(roles[2].Channels);
    }

    [Fact]
    public async Task Delete_removes_the_role_and_cascades_to_its_channels()
    {
        var role = NewRole("Family", NotificationChannel.Push, NotificationChannel.Email);
        var other = NewRole("Friends", NotificationChannel.Push);
        await _repository.SaveAsync(role, CancellationToken.None);
        await _repository.SaveAsync(other, CancellationToken.None);

        await _repository.DeleteAsync(role.Id, CancellationToken.None);

        Assert.Null(await _repository.FindByIdAsync(role.Id, CancellationToken.None));
        Assert.NotNull(await _repository.FindByIdAsync(other.Id, CancellationToken.None));
        Assert.Equal(1, CountRows("role_channels"));
    }

    [Fact]
    public async Task IsInUse_is_false_when_the_members_table_does_not_exist_yet()
    {
        var role = NewRole("Family");
        await _repository.SaveAsync(role, CancellationToken.None);

        Assert.False(await _repository.IsInUseAsync(role.Id, CancellationToken.None));
    }

    [Fact]
    public async Task IsInUse_is_true_only_while_a_member_including_an_annulled_one_holds_the_role()
    {
        var members = new SqliteVecMemberRepository(_options, new ActivitySource("Enrollment.UnitTests.Roles.Members"));
        var role = NewRole("Family");
        await _repository.SaveAsync(role, CancellationToken.None);
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.8f;
        var member = Member.Enroll(MemberId.New(), "Ada Lovelace", FaceEmbedding.FromVector(vector).Value).Value;
        await members.SaveAsync(member, CancellationToken.None);
        Assert.False(await _repository.IsInUseAsync(role.Id, CancellationToken.None));

        await members.SetRoleAsync(member.Id, role.Id, CancellationToken.None);
        await members.SetAnnulledAsync(member.Id, true, CancellationToken.None);
        Assert.True(await _repository.IsInUseAsync(role.Id, CancellationToken.None));

        await members.SetRoleAsync(member.Id, null, CancellationToken.None);
        Assert.False(await _repository.IsInUseAsync(role.Id, CancellationToken.None));
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
