using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="AssignMemberRoleCommandHandler"/>.</summary>
public sealed class AssignMemberRoleCommandHandlerTests
{
    private static Member NewMember()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.9f;
        return Member.Enroll(MemberId.New(), "Ada Lovelace", FaceEmbedding.FromVector(vector).Value).Value;
    }

    private static Role NewRole() => Role.Create(RoleId.New(), "Family", []).Value;

    private static void Setup(AutoMocker mocker, Member? member, Role? role)
    {
        mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.FindByIdAsync(It.IsAny<MemberId>(), It.IsAny<CancellationToken>())).ReturnsAsync(member);
        mocker.GetMock<IRoleRepository>()
            .Setup(x => x.FindByIdAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
    }

    private static void VerifyNotAssigned(AutoMocker mocker) =>
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.SetRoleAsync(It.IsAny<MemberId>(), It.IsAny<RoleId?>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Assign_sets_the_role_on_an_existing_member()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        var role = NewRole();
        Setup(mocker, member, role);

        var result = await mocker.CreateInstance<AssignMemberRoleCommandHandler>()
            .HandleAsync(new AssignMemberRoleCommand(member.Id, role.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.SetRoleAsync(member.Id, role.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_null_clears_the_role_without_looking_up_a_role()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        Setup(mocker, member, role: null);

        var result = await mocker.CreateInstance<AssignMemberRoleCommandHandler>()
            .HandleAsync(new AssignMemberRoleCommand(member.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.SetRoleAsync(member.Id, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_fails_when_the_role_vanishes_before_the_write()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        var role = NewRole();
        Setup(mocker, member, role);
        mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.SetRoleAsync(member.Id, role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoleAssignmentResult.RoleNotFound);

        var result = await mocker.CreateInstance<AssignMemberRoleCommandHandler>()
            .HandleAsync(new AssignMemberRoleCommand(member.Id, role.Id), CancellationToken.None);

        Assert.Equal(AssignMemberRoleError.RoleNotFound, result.Error);
    }

    [Fact]
    public async Task Assign_fails_for_unknown_member()
    {
        var mocker = new AutoMocker();
        Setup(mocker, member: null, NewRole());

        var result = await mocker.CreateInstance<AssignMemberRoleCommandHandler>()
            .HandleAsync(new AssignMemberRoleCommand(MemberId.New(), RoleId.New()), CancellationToken.None);

        Assert.Equal(AssignMemberRoleError.MemberNotFound, result.Error);
        VerifyNotAssigned(mocker);
    }

    [Fact]
    public async Task Assign_fails_for_unknown_role()
    {
        var mocker = new AutoMocker();
        Setup(mocker, NewMember(), role: null);

        var result = await mocker.CreateInstance<AssignMemberRoleCommandHandler>()
            .HandleAsync(new AssignMemberRoleCommand(MemberId.New(), RoleId.New()), CancellationToken.None);

        Assert.Equal(AssignMemberRoleError.RoleNotFound, result.Error);
        VerifyNotAssigned(mocker);
    }
}
