using System.Diagnostics;
using Common;
using Enrollment.Application;
using Enrollment.Domain;
using Enrollment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Enrollment.UnitTests;

/// <summary>
/// Duplicate-name races against real SQLite: two independent repositories (one connection each, as two
/// scoped requests would have) write the same file at the same time.
/// </summary>
public sealed class RoleConcurrencyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"camguard-roles-race-{Guid.NewGuid():N}.db");
    private readonly IOptions<VectorStoreOptions> _options;

    public RoleConcurrencyTests() =>
        _options = Options.Create(new VectorStoreOptions { ConnectionString = $"Data Source={_dbPath}" });

    /// <summary>A repository whose schema is already ensured, so only the writes race.</summary>
    private async Task<SqliteRoleRepository> NewRepositoryAsync()
    {
        var repository = new SqliteRoleRepository(_options, new ActivitySource("Enrollment.UnitTests.RoleRace"));
        await repository.ListAllAsync(CancellationToken.None);
        return repository;
    }

    [Fact]
    public async Task Concurrent_saves_of_the_same_name_let_exactly_one_win_with_a_typed_duplicate_error()
    {
        var first = await NewRepositoryAsync();
        var second = await NewRepositoryAsync();

        var results = await Task.WhenAll(
            Attempt(first, "Family"),
            Attempt(second, "family"));

        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r is DuplicateRoleNameException);
        Assert.Single(await first.ListAllAsync(CancellationToken.None));

        static async Task<Exception?> Attempt(SqliteRoleRepository repository, string name)
        {
            try
            {
                await Task.Yield();
                await repository.SaveAsync(Role.Create(RoleId.New(), name, [NotificationChannel.Push]).Value, CancellationToken.None);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_name_yield_one_Created_and_one_DuplicateName()
    {
        var first = new CreateRoleCommandHandler(await NewRepositoryAsync());
        var second = new CreateRoleCommandHandler(await NewRepositoryAsync());

        var results = await Task.WhenAll(
            Task.Run(() => first.HandleAsync(new CreateRoleCommand("Family", []), CancellationToken.None)),
            Task.Run(() => second.HandleAsync(new CreateRoleCommand("FAMILY", []), CancellationToken.None)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error == CreateRoleError.NameAlreadyInUse);
    }

    [Fact]
    public async Task Concurrent_renames_to_the_same_name_yield_one_success_and_one_DuplicateName()
    {
        var seed = await NewRepositoryAsync();
        var a = Role.Create(RoleId.New(), "A", []).Value;
        var b = Role.Create(RoleId.New(), "B", []).Value;
        await seed.SaveAsync(a, CancellationToken.None);
        await seed.SaveAsync(b, CancellationToken.None);
        var first = new UpdateRoleCommandHandler(await NewRepositoryAsync());
        var second = new UpdateRoleCommandHandler(await NewRepositoryAsync());

        var results = await Task.WhenAll(
            Task.Run(() => first.HandleAsync(new UpdateRoleCommand(a.Id, "Shared", []), CancellationToken.None)),
            Task.Run(() => second.HandleAsync(new UpdateRoleCommand(b.Id, "SHARED", []), CancellationToken.None)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error == UpdateRoleError.NameAlreadyInUse);
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
