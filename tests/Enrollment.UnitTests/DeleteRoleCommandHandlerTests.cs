using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="DeleteRoleCommandHandler"/>.</summary>
public sealed class DeleteRoleCommandHandlerTests
{
    [Theory]
    [InlineData(RoleDeletionResult.InUse, DeleteRoleError.RoleInUse)]
    [InlineData(RoleDeletionResult.NotFound, DeleteRoleError.RoleNotFound)]
    public async Task Delete_maps_a_refused_deletion_to_its_typed_error(RoleDeletionResult outcome, DeleteRoleError expected)
    {
        var mocker = new AutoMocker();
        var roleId = RoleId.New();
        mocker.GetMock<IRoleRepository>()
            .Setup(x => x.DeleteIfUnusedAsync(roleId, It.IsAny<CancellationToken>())).ReturnsAsync(outcome);

        var result = await mocker.CreateInstance<DeleteRoleCommandHandler>()
            .HandleAsync(new DeleteRoleCommand(roleId), CancellationToken.None);

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Delete_succeeds_when_the_repository_deleted_the_role()
    {
        var mocker = new AutoMocker();
        var roleId = RoleId.New();
        mocker.GetMock<IRoleRepository>()
            .Setup(x => x.DeleteIfUnusedAsync(roleId, It.IsAny<CancellationToken>())).ReturnsAsync(RoleDeletionResult.Deleted);

        var result = await mocker.CreateInstance<DeleteRoleCommandHandler>()
            .HandleAsync(new DeleteRoleCommand(roleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
