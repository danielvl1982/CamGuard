using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Assigns a member to a role; a <c>null</c> <paramref name="RoleId"/> clears the assignment.</summary>
public sealed record AssignMemberRoleCommand(MemberId MemberId, RoleId? RoleId);

public sealed class AssignMemberRoleCommandHandler(
    IMemberVectorRepository memberVectorRepository,
    IRoleRepository roleRepository)
{
    public async Task<UnitResult<AssignMemberRoleError>> HandleAsync(
        AssignMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await memberVectorRepository.FindByIdAsync(command.MemberId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UnitResult.Failure(AssignMemberRoleError.MemberNotFound);
        }

        if (command.RoleId is not null
            && await roleRepository.FindByIdAsync(command.RoleId, cancellationToken).ConfigureAwait(false) is null)
        {
            return UnitResult.Failure(AssignMemberRoleError.RoleNotFound);
        }

        await memberVectorRepository.SetRoleAsync(command.MemberId, command.RoleId, cancellationToken)
            .ConfigureAwait(false);

        return UnitResult.Success<AssignMemberRoleError>();
    }
}

public enum AssignMemberRoleError
{
    MemberNotFound,
    RoleNotFound
}
