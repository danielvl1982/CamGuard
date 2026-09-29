using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="ListRolesQueryHandler"/>.</summary>
public sealed class ListRolesQueryHandlerTests
{
    [Fact]
    public async Task List_returns_the_repository_roles()
    {
        var mocker = new AutoMocker();
        IReadOnlyList<Role> roles = [Role.Create(RoleId.New(), "Family", [NotificationChannel.Push]).Value];
        mocker.GetMock<IRoleRepository>()
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await mocker.CreateInstance<ListRolesQueryHandler>()
            .HandleAsync(new ListRolesQuery(), CancellationToken.None);

        Assert.Same(roles, result);
    }
}
