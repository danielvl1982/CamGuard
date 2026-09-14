namespace Enrollment.Application;

/// <summary>Registers a new member from identity data and one reference photo.</summary>
public sealed record RegisterMemberCommand(string FullName, Stream Photo);
