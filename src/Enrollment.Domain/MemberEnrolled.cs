namespace Enrollment.Domain;

/// <summary>Raised when <see cref="Member.Enroll"/> succeeds.</summary>
public sealed record MemberEnrolled(MemberId MemberId) : IDomainEvent;
