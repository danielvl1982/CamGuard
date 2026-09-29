using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="UpdateRoleCommandHandler"/>.</summary>
public sealed class UpdateRoleCommandHandlerTests
{
    private static Role NewRole(string name = "Family") =>
        Role.Create(RoleId.New(), name, [NotificationChannel.Push]).Value;

    private static Mock<IRoleRepository> Setup(AutoMocker mocker, Role? byId, Role? byName)
    {
        var repo = mocker.GetMock<IRoleRepository>();
        repo.Setup(x => x.FindByIdAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>())).ReturnsAsync(byId);
        repo.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(byName);
        return repo;
    }

    [Fact]
    public async Task Update_saves_new_name_and_channels()
    {
        var mocker = new AutoMocker();
        var role = NewRole();
        var repo = Setup(mocker, role, byName: null);

        var result = await mocker.CreateInstance<UpdateRoleCommandHandler>()
            .HandleAsync(new UpdateRoleCommand(role.Id, " Friends ", [NotificationChannel.Email]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(x => x.SaveAsync(
            It.Is<Role>(r => r.Id.Equals(role.Id) && r.Name == "Friends"
                && r.Channels.Count == 1 && r.Channels.Contains(NotificationChannel.Email)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_allows_keeping_own_name_with_different_casing()
    {
        var mocker = new AutoMocker();
        var role = NewRole();
        var repo = Setup(mocker, role, byName: role);

        var result = await mocker.CreateInstance<UpdateRoleCommandHandler>()
            .HandleAsync(new UpdateRoleCommand(role.Id, "FAMILY", []), CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_rejects_name_held_by_another_role()
    {
        var mocker = new AutoMocker();
        var repo = Setup(mocker, NewRole(), byName: NewRole("Friends"));

        var result = await mocker.CreateInstance<UpdateRoleCommandHandler>()
            .HandleAsync(new UpdateRoleCommand(RoleId.New(), "Friends", []), CancellationToken.None);

        Assert.Equal(UpdateRoleError.NameAlreadyInUse, result.Error);
        repo.Verify(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_rejects_blank_name()
    {
        var mocker = new AutoMocker();
        var repo = Setup(mocker, NewRole(), byName: null);

        var result = await mocker.CreateInstance<UpdateRoleCommandHandler>()
            .HandleAsync(new UpdateRoleCommand(RoleId.New(), " ", []), CancellationToken.None);

        Assert.Equal(UpdateRoleError.InvalidName, result.Error);
        repo.Verify(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_fails_for_unknown_role()
    {
        var mocker = new AutoMocker();
        var repo = Setup(mocker, byId: null, byName: null);

        var result = await mocker.CreateInstance<UpdateRoleCommandHandler>()
            .HandleAsync(new UpdateRoleCommand(RoleId.New(), "Family", []), CancellationToken.None);

        Assert.Equal(UpdateRoleError.RoleNotFound, result.Error);
        repo.Verify(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
