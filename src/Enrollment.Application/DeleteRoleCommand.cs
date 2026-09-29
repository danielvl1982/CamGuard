using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Deletes a role that no member holds.</summary>
public sealed record DeleteRoleCommand(RoleId RoleId);

/// <summary>Rejects deleting a role still assigned to any member (annulled ones included).</summary>
public sealed class DeleteRoleCommandHandler(IRoleRepository roleRepository)
{
    public async Task<UnitResult<DeleteRoleError>> HandleAsync(
        DeleteRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await roleRepository.FindByIdAsync(command.RoleId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UnitResult.Failure(DeleteRoleError.RoleNotFound);
        }

        if (await roleRepository.IsInUseAsync(command.RoleId, cancellationToken).ConfigureAwait(false))
        {
            return UnitResult.Failure(DeleteRoleError.RoleInUse);
        }

        await roleRepository.DeleteAsync(command.RoleId, cancellationToken).ConfigureAwait(false);

        return UnitResult.Success<DeleteRoleError>();
    }
}

public enum DeleteRoleError
{
    RoleNotFound,
    RoleInUse
}
