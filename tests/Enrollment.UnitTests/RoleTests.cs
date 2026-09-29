using Enrollment.Domain;

namespace Enrollment.UnitTests;

/// <summary>Domain tests for <see cref="Role"/> and <see cref="RoleId"/>.</summary>
public sealed class RoleTests
{
    [Fact]
    public void Create_trims_name_and_collapses_duplicate_channels()
    {
        var result = Role.Create(RoleId.New(), "  Family  ", [NotificationChannel.Push, NotificationChannel.Push]);

        Assert.True(result.IsSuccess);
        Assert.Equal("Family", result.Value.Name);
        Assert.Equal([NotificationChannel.Push], result.Value.Channels);
    }

    [Fact]
    public void Create_allows_no_channels()
    {
        var role = Role.Create(RoleId.New(), "Guest", []).Value;

        Assert.Empty(role.Channels);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string name)
    {
        var result = Role.Create(RoleId.New(), name, []);

        Assert.True(result.IsFailure);
        Assert.Equal(RoleError.EmptyName, result.Error);
    }

    [Fact]
    public void Create_rejects_name_over_max_length_but_accepts_the_limit()
    {
        Assert.Equal(RoleError.NameTooLong, Role.Create(RoleId.New(), new string('a', Role.MaxNameLength + 1), []).Error);
        Assert.True(Role.Create(RoleId.New(), new string('a', Role.MaxNameLength), []).IsSuccess);
    }

    [Fact]
    public void Rename_updates_name_and_invalid_rename_leaves_it_unchanged()
    {
        var role = Role.Create(RoleId.New(), "Family", []).Value;

        Assert.True(role.Rename(" Friends ").IsSuccess);
        Assert.Equal("Friends", role.Name);

        Assert.Equal(RoleError.EmptyName, role.Rename(" ").Error);
        Assert.Equal("Friends", role.Name);
    }

    [Fact]
    public void SetChannels_replaces_the_set()
    {
        var role = Role.Create(RoleId.New(), "Family", [NotificationChannel.Push]).Value;

        role.SetChannels([NotificationChannel.Email]);

        Assert.Equal([NotificationChannel.Email], role.Channels);
    }

    [Fact]
    public void Create_rejects_an_undefined_channel()
    {
        var result = Role.Create(RoleId.New(), "Family", [NotificationChannel.Push, (NotificationChannel)99]);

        Assert.Equal(RoleError.InvalidChannel, result.Error);
    }

    [Fact]
    public void SetChannels_rejects_an_undefined_channel_and_leaves_the_set_unchanged()
    {
        var role = Role.Create(RoleId.New(), "Family", [NotificationChannel.Push]).Value;

        var result = role.SetChannels([NotificationChannel.Email, (NotificationChannel)99]);

        Assert.Equal(RoleError.InvalidChannel, result.Error);
        Assert.Equal([NotificationChannel.Push], role.Channels);
    }

    [Fact]
    public void RoleId_rejects_empty_guid()
    {
        Assert.True(RoleId.From(Guid.Empty).IsFailure);
        Assert.True(RoleId.From(Guid.NewGuid()).IsSuccess);
    }
}
