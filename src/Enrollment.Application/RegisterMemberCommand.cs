namespace Enrollment.Application;

/// <summary>
/// Registers a new member from identity data and one or more reference photos —
/// per <c>member-enrollment</c> spec's "Member Registration Input" requirement.
/// </summary>
public sealed record RegisterMemberCommand
{
    public RegisterMemberCommand(string fullName, IReadOnlyList<Stream> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        if (photos.Count == 0)
            throw new ArgumentException("At least one reference photo is required.", nameof(photos));

        FullName = fullName;
        Photos = photos;
    }

    public string FullName { get; }

    /// <summary>One or more reference photos, each validated independently by the face-detection gate.</summary>
    public IReadOnlyList<Stream> Photos { get; }
}
