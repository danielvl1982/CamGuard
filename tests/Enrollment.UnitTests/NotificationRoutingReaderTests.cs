using Enrollment.Domain;
using Enrollment.PublicApi;
using Moq;
using Moq.AutoMock;

namespace Enrollment.UnitTests;

/// <summary>Tests for <see cref="NotificationRoutingReader"/>: role/default channel resolution and domain-to-boundary mapping.</summary>
public sealed class NotificationRoutingReaderTests
{
    private readonly AutoMocker _mocker = new();

    private INotificationRoutingReader CreateSut() => _mocker.CreateInstance<NotificationRoutingReader>();

    private static Member CreateMember(RoleId? roleId)
    {
        var vector = new float[FaceEmbedding.ExpectedDimension];
        vector[0] = 0.9f;
        return Member.Rehydrate(MemberId.New(), "Ada", FaceEmbedding.FromVector(vector).Value, false, roleId);
    }

    private void SetupMember(Guid memberId, Member? member) =>
        _mocker.GetMock<IMemberVectorRepository>()
            .Setup(x => x.FindByIdAsync(It.Is<MemberId>(m => m.Value == memberId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

    private void SetupRole(Role? role) =>
        _mocker.GetMock<IRoleRepository>()
            .Setup(x => x.FindByIdAsync(It.IsAny<RoleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

    private void SetupDefault(params NotificationChannel[] channels) =>
        _mocker.GetMock<INotificationSettingsRepository>()
            .Setup(x => x.GetDefaultChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels.ToHashSet());

    [Fact]
    public async Task Member_with_a_role_gets_the_mapped_role_channels()
    {
        var role = Role.Rehydrate(RoleId.New(), "Parent", [NotificationChannel.Push]);
        var member = CreateMember(role.Id);
        SetupMember(member.Id.Value, member);
        SetupRole(role);
        SetupDefault(NotificationChannel.Email);

        var channels = await CreateSut().GetChannelsForMemberAsync(member.Id.Value, CancellationToken.None);

        Assert.Equal([NotificationChannelKind.Push], channels);
    }

    [Fact]
    public async Task Role_with_no_channels_yields_an_empty_set_not_the_default()
    {
        var role = Role.Rehydrate(RoleId.New(), "Guest", []);
        var member = CreateMember(role.Id);
        SetupMember(member.Id.Value, member);
        SetupRole(role);
        SetupDefault(NotificationChannel.Email);

        var channels = await CreateSut().GetChannelsForMemberAsync(member.Id.Value, CancellationToken.None);

        Assert.Empty(channels);
    }

    [Fact]
    public async Task Member_without_a_role_falls_back_to_the_default_channels()
    {
        var member = CreateMember(null);
        SetupMember(member.Id.Value, member);
        SetupDefault(NotificationChannel.Push, NotificationChannel.Email);

        var channels = await CreateSut().GetChannelsForMemberAsync(member.Id.Value, CancellationToken.None);

        Assert.Equal([NotificationChannelKind.Push, NotificationChannelKind.Email], channels.OrderBy(c => c));
    }

    [Fact]
    public async Task Unknown_member_and_dangling_role_fall_back_to_the_default_channels()
    {
        var unknownId = Guid.NewGuid();
        SetupMember(unknownId, null);
        var dangling = CreateMember(RoleId.New());
        SetupMember(dangling.Id.Value, dangling);
        SetupRole(null);
        SetupDefault(NotificationChannel.Email);
        var sut = CreateSut();

        Assert.Equal([NotificationChannelKind.Email], await sut.GetChannelsForMemberAsync(unknownId, CancellationToken.None));
        Assert.Equal([NotificationChannelKind.Email], await sut.GetChannelsForMemberAsync(dangling.Id.Value, CancellationToken.None));
    }

    [Fact]
    public async Task Default_channels_are_mapped_from_the_settings_repository()
    {
        SetupDefault(NotificationChannel.Email);

        var channels = await CreateSut().GetDefaultChannelsAsync(CancellationToken.None);

        Assert.Equal([NotificationChannelKind.Email], channels);
    }
}
