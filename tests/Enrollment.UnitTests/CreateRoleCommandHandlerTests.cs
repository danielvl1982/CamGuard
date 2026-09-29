using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="CreateRoleCommandHandler"/>.</summary>
public sealed class CreateRoleCommandHandlerTests
{
    [Fact]
    public async Task Create_saves_role_with_trimmed_name_and_channels()
    {
        var mocker = new AutoMocker();
        var repo = mocker.GetMock<IRoleRepository>();
        repo.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        var result = await mocker.CreateInstance<CreateRoleCommandHandler>()
            .HandleAsync(new CreateRoleCommand("  Family ", [NotificationChannel.Email]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(x => x.SaveAsync(
            It.Is<Role>(r => r.Id.Equals(result.Value) && r.Name == "Family" && r.Channels.Contains(NotificationChannel.Email)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_rejects_blank_name_without_saving()
    {
        var mocker = new AutoMocker();

        var result = await mocker.CreateInstance<CreateRoleCommandHandler>()
            .HandleAsync(new CreateRoleCommand(" ", []), CancellationToken.None);

        Assert.Equal(CreateRoleError.InvalidName, result.Error);
        mocker.GetMock<IRoleRepository>().Verify(
            x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_maps_a_duplicate_name_race_on_save_to_NameAlreadyInUse()
    {
        var mocker = new AutoMocker();
        var repo = mocker.GetMock<IRoleRepository>();
        repo.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);
        repo.Setup(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateRoleNameException("Family"));

        var result = await mocker.CreateInstance<CreateRoleCommandHandler>()
            .HandleAsync(new CreateRoleCommand("Family", []), CancellationToken.None);

        Assert.Equal(CreateRoleError.NameAlreadyInUse, result.Error);
    }

    [Fact]
    public async Task Create_rejects_a_name_already_in_use()
    {
        var mocker = new AutoMocker();
        var existing = Role.Create(RoleId.New(), "family", []).Value;
        var repo = mocker.GetMock<IRoleRepository>();
        repo.Setup(x => x.FindByNameAsync("Family", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await mocker.CreateInstance<CreateRoleCommandHandler>()
            .HandleAsync(new CreateRoleCommand("Family", []), CancellationToken.None);

        Assert.Equal(CreateRoleError.NameAlreadyInUse, result.Error);
        repo.Verify(x => x.SaveAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
