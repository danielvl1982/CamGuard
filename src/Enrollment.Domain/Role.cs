using CSharpFunctionalExtensions;

namespace Enrollment.Domain;

/// <summary>
/// A "family" role a member can be assigned to. It selects which <see cref="NotificationChannel"/>s
/// are used when a member holding it is recognized; an empty channel set means the role does not notify.
/// </summary>
public sealed class Role : Entity<RoleId>
{
    public const int MaxNameLength = 50;

    private Role(RoleId id, string name, HashSet<NotificationChannel> channels)
    {
        Id = id;
        Name = name;
        Channels = channels;
    }

    public string Name { get; private set; }

    public IReadOnlySet<NotificationChannel> Channels { get; private set; }

    /// <summary>Renames the role. The name is trimmed; blank or over-long names are rejected and leave the role unchanged.</summary>
    public UnitResult<RoleError> Rename(string name)
    {
        var validated = ValidateName(name);
        if (validated.IsFailure)
        {
            return UnitResult.Failure(validated.Error);
        }

        Name = validated.Value;
        return UnitResult.Success<RoleError>();
    }

    /// <summary>
    /// Replaces the channel set (duplicates collapse). An empty set means the role does not notify;
    /// an undefined channel value is rejected and leaves the role unchanged.
    /// </summary>
    public UnitResult<RoleError> SetChannels(IEnumerable<NotificationChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        var set = new HashSet<NotificationChannel>(channels);
        if (!AllDefined(set))
        {
            return UnitResult.Failure(RoleError.InvalidChannel);
        }

        Channels = set;
        return UnitResult.Success<RoleError>();
    }

    /// <summary>Reconstitutes a role from persistence without validating the stored name again beyond non-blank.</summary>
    public static Role Rehydrate(RoleId id, string name, IEnumerable<NotificationChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role(id, name, [.. channels]);
    }

    public static Result<Role, RoleError> Create(RoleId id, string name, IEnumerable<NotificationChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(channels);

        var validated = ValidateName(name);
        if (validated.IsFailure)
        {
            return Result.Failure<Role, RoleError>(validated.Error);
        }

        var set = new HashSet<NotificationChannel>(channels);
        return AllDefined(set)
            ? Result.Success<Role, RoleError>(new Role(id, validated.Value, set))
            : Result.Failure<Role, RoleError>(RoleError.InvalidChannel);
    }

    private static bool AllDefined(IEnumerable<NotificationChannel> channels) => channels.All(Enum.IsDefined);

    private static Result<string, RoleError> ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string, RoleError>(RoleError.EmptyName);
        }

        var trimmed = name.Trim();
        return trimmed.Length > MaxNameLength
            ? Result.Failure<string, RoleError>(RoleError.NameTooLong)
            : Result.Success<string, RoleError>(trimmed);
    }
}

public enum RoleError
{
    EmptyName,
    NameTooLong,
    InvalidChannel
}
