namespace Enrollment.Application;

/// <summary>Queries the directory for the closest enrolled member to a face embedding.</summary>
public sealed record QueryByEmbeddingQuery(float[] EmbeddingVector);
