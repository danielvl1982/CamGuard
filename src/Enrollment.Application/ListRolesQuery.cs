using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Lists every role with its channels, ordered by name.</summary>
public sealed record ListRolesQuery;

public sealed class ListRolesQueryHandler(IRoleRepository roleRepository)
{
    public Task<IReadOnlyList<Role>> HandleAsync(ListRolesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return roleRepository.ListAllAsync(cancellationToken);
    }
}
