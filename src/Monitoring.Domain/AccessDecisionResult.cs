namespace Monitoring.Domain;

/// <summary>
/// The resolved outcome of a single access decision, carrying the cosine <see cref="Distance"/>
/// value that produced it (never a similarity score — per the resolved Enrollment naming
/// convention) and a human-readable <see cref="Reason"/>. Value object: no identity, immutable,
/// per the <c>value-object</c> skill.
/// </summary>
public sealed record AccessDecisionResult(AccessOutcome Outcome, double? Distance, string Reason);
