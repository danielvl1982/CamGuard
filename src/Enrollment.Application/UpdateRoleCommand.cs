using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Renames a role and replaces its notification channels in one step.</summary>
public sealed record UpdateRoleCommand(RoleId RoleId, string Name, IReadOnlyCollection<NotificationChannel> Channels);

/// <summary>
/// Applies name and channels to an existing role. A name held by ANOTHER role (case-insensitive) is
/// rejected; keeping the role's own name, in any casing, is allowed.
/// </summary>
public sealed class UpdateRoleCommandHandler(IRoleRepository roleRepository)
{
    public async Task<UnitResult<UpdateRoleError>> HandleAsync(
        UpdateRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var role = await roleRepository.FindByIdAsync(command.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return UnitResult.Failure(UpdateRoleError.RoleNotFound);
        }

        if (role.Rename(command.Name).IsFailure)
        {
            return UnitResult.Failure(UpdateRoleError.InvalidName);
        }

        var sameName = await roleRepository.FindByNameAsync(role.Name, cancellationToken).ConfigureAwait(false);
        if (sameName is not null && !sameName.Id.Equals(role.Id))
        {
            return UnitResult.Failure(UpdateRoleError.NameAlreadyInUse);
        }

        role.SetChannels(command.Channels);
        await roleRepository.SaveAsync(role, cancellationToken).ConfigureAwait(false);

        return UnitResult.Success<UpdateRoleError>();
    }
}

public enum UpdateRoleError
{
    InvalidName,
    RoleNotFound,
    NameAlreadyInUse
}
