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

        return await roleRepository.DeleteIfUnusedAsync(command.RoleId, cancellationToken).ConfigureAwait(false) switch
        {
            RoleDeletionResult.Deleted => UnitResult.Success<DeleteRoleError>(),
            RoleDeletionResult.InUse => UnitResult.Failure(DeleteRoleError.RoleInUse),
            _ => UnitResult.Failure(DeleteRoleError.RoleNotFound),
        };
    }
}

public enum DeleteRoleError
{
    RoleNotFound,
    RoleInUse
}
