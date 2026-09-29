namespace Enrollment.Domain;

/// <summary>Thrown by <see cref="IRoleRepository.SaveAsync"/> when another role already holds the name (case-insensitive).</summary>
public sealed class DuplicateRoleNameException : Exception
{
    public DuplicateRoleNameException(string name, Exception? innerException = null)
        : base($"A role named '{name}' already exists.", innerException)
    {
    }
}
