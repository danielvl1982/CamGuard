namespace Enrollment.Domain;

/// <summary>Port for persisting roles together with their notification channels.</summary>
public interface IRoleRepository
{
    /// <summary>
    /// Inserts or updates the role and replaces its stored channel set. Role names are unique
    /// case-insensitively; a violation surfaces as a provider exception, so callers check
    /// <see cref="FindByNameAsync"/> first.
    /// </summary>
    Task SaveAsync(Role role, CancellationToken cancellationToken);

    Task<Role?> FindByIdAsync(RoleId id, CancellationToken cancellationToken);

    /// <summary>Finds a role by name (trimmed, case-insensitive), or <c>null</c> when none matches.</summary>
    Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>Returns every role ordered by name.</summary>
    Task<IReadOnlyList<Role>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the role and its channels. Deleting an unknown role is a no-op.</summary>
    Task DeleteAsync(RoleId id, CancellationToken cancellationToken);

    /// <summary>Whether any member (annulled ones included) is currently assigned to the role.</summary>
    Task<bool> IsInUseAsync(RoleId id, CancellationToken cancellationToken);
}
