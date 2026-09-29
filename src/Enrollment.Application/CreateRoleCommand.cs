using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Creates a member role and the notification channels it selects (none = the role does not notify).</summary>
public sealed record CreateRoleCommand(string Name, IReadOnlyCollection<NotificationChannel> Channels);

/// <summary>Validates the name (trimmed, non-blank, bounded) and rejects one already used, ignoring case.</summary>
public sealed class CreateRoleCommandHandler(IRoleRepository roleRepository)
{
    public async Task<Result<RoleId, CreateRoleError>> HandleAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var created = Role.Create(RoleId.New(), command.Name, command.Channels);
        if (created.IsFailure)
        {
            return Result.Failure<RoleId, CreateRoleError>(CreateRoleError.InvalidName);
        }

        var role = created.Value;
        if (await roleRepository.FindByNameAsync(role.Name, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<RoleId, CreateRoleError>(CreateRoleError.NameAlreadyInUse);
        }

        try
        {
            await roleRepository.SaveAsync(role, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateRoleNameException)
        {
            // Lost a race with a concurrent create of the same name.
            return Result.Failure<RoleId, CreateRoleError>(CreateRoleError.NameAlreadyInUse);
        }

        return Result.Success<RoleId, CreateRoleError>(role.Id);
    }
}

public enum CreateRoleError
{
    InvalidName,
    NameAlreadyInUse
}
