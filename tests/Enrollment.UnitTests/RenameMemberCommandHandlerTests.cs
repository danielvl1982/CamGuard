using Enrollment.Application;
using Enrollment.Domain;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Application handler tests for <see cref="RenameMemberCommandHandler"/>.</summary>
public sealed class RenameMemberCommandHandlerTests
{
    private static FaceEmbedding ValidEmbedding()
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.9f;
        return FaceEmbedding.FromVector(vector).Value;
    }

    private static Member NewMember(string name = "Ada Lovelace") =>
        Member.Enroll(MemberId.New(), name, ValidEmbedding()).Value;

    private static void SetupFind(AutoMocker mocker, Member? byId, Member? byName)
    {
        var repo = mocker.GetMock<IMemberVectorRepository>();
        repo.Setup(x => x.FindByIdAsync(It.IsAny<MemberId>(), It.IsAny<CancellationToken>())).ReturnsAsync(byId);
        repo.Setup(x => x.FindByFullNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(byName);
    }

    private static void VerifyNotRenamed(AutoMocker mocker) =>
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.RenameAsync(It.IsAny<MemberId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Rename_persists_trimmed_name_when_no_other_member_has_it()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        SetupFind(mocker, member, byName: null);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(member.Id, "  Grace Hopper  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.RenameAsync(member.Id, "Grace Hopper", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rename_rejects_blank_name_without_persisting(string name)
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        SetupFind(mocker, member, byName: null);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(member.Id, name), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RenameMemberError.InvalidName, result.Error);
        VerifyNotRenamed(mocker);
    }

    [Fact]
    public async Task Rename_rejects_name_held_by_another_member()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        var other = NewMember("Grace Hopper");
        SetupFind(mocker, member, byName: other);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(member.Id, "grace hopper"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RenameMemberError.NameAlreadyInUse, result.Error);
        VerifyNotRenamed(mocker);
    }

    [Fact]
    public async Task Rename_returns_MemberNotFound_for_unknown_member()
    {
        var mocker = new AutoMocker();
        SetupFind(mocker, byId: null, byName: null);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(MemberId.New(), "Grace Hopper"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RenameMemberError.MemberNotFound, result.Error);
        VerifyNotRenamed(mocker);
    }

    [Fact]
    public async Task Rename_to_own_current_name_is_a_no_op_success()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        var stored = Member.Rehydrate(member.Id, "Ada Lovelace", ValidEmbedding());
        SetupFind(mocker, member, byName: stored);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(member.Id, " Ada Lovelace "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        VerifyNotRenamed(mocker);
    }

    [Fact]
    public async Task Rename_changing_only_casing_of_own_name_is_persisted()
    {
        var mocker = new AutoMocker();
        var member = NewMember();
        var stored = Member.Rehydrate(member.Id, "Ada Lovelace", ValidEmbedding());
        SetupFind(mocker, member, byName: stored);

        var result = await mocker.CreateInstance<RenameMemberCommandHandler>()
            .HandleAsync(new RenameMemberCommand(member.Id, "ada lovelace"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mocker.GetMock<IMemberVectorRepository>().Verify(
            x => x.RenameAsync(member.Id, "ada lovelace", It.IsAny<CancellationToken>()), Times.Once);
    }
}
