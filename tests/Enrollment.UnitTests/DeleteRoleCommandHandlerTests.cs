using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="DeleteRoleCommandHandler"/>.</summary>
public sealed class DeleteRoleCommandHandlerTests
{
    private static Mock<IRoleRepository> Setup(AutoMocker mocker, Role? role, bool inUse)
    {
        var repo = mocker.GetMock<IRoleRepository>();
        repo.Setup(x => x.FindByIdAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        repo.Setup(x => x.IsInUseAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>())).ReturnsAsync(inUse);
        return repo;
    }

    [Fact]
    public async Task Delete_removes_an_unused_role()
    {
        var mocker = new AutoMocker();
        var role = Role.Create(RoleId.New(), "Family", []).Value;
        var repo = Setup(mocker, role, inUse: false);

        var result = await mocker.CreateInstance<DeleteRoleCommandHandler>()
            .HandleAsync(new DeleteRoleCommand(role.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(x => x.DeleteAsync(role.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_rejects_a_role_assigned_to_members()
    {
        var mocker = new AutoMocker();
        var role = Role.Create(RoleId.New(), "Family", []).Value;
        var repo = Setup(mocker, role, inUse: true);

        var result = await mocker.CreateInstance<DeleteRoleCommandHandler>()
            .HandleAsync(new DeleteRoleCommand(role.Id), CancellationToken.None);

        Assert.Equal(DeleteRoleError.RoleInUse, result.Error);
        repo.Verify(x => x.DeleteAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_fails_for_unknown_role()
    {
        var mocker = new AutoMocker();
        var repo = Setup(mocker, role: null, inUse: false);

        var result = await mocker.CreateInstance<DeleteRoleCommandHandler>()
            .HandleAsync(new DeleteRoleCommand(RoleId.New()), CancellationToken.None);

        Assert.Equal(DeleteRoleError.RoleNotFound, result.Error);
        repo.Verify(x => x.DeleteAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
