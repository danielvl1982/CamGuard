using Monitoring.Domain;

namespace Monitoring.Application;

/// <summary>
/// The single message envelope threaded through the <c>Microsoft.Agents.AI.Workflows</c>
/// detect → verify → decide → notify graph (see design.md's Application Flow). Each executor
/// returns an updated copy (records are immutable) populated with that stage's result; the
/// zero-face short-circuit and the normal verify/decide path both converge on
/// <see cref="NotifyExecutor"/> by producing the same envelope type with <see cref="Decision"/>
/// already set.
/// </summary>
/// <param name="ImageReference">Path/reference to the ingested file, carried end to end for archival/audit.</param>
/// <param name="ImageStream">The already-opened image stream. Opening/closing it is an Infrastructure I/O concern; Application only forwards it.</param>
/// <param name="EmbeddingVector">Set by <see cref="DetectPersonExecutor"/> when a face was detected; <c>null</c> on the zero-face short-circuit path.</param>
/// <param name="CandidateMemberId">Set by <see cref="VerifyIdentityExecutor"/> when a candidate match was found.</param>
/// <param name="Distance">Set by <see cref="VerifyIdentityExecutor"/>, forwarded unmodified from <c>IMemberDirectoryReader</c> — never rounded/truncated.</param>
/// <param name="Decision">Set by <see cref="DecideAccessExecutor"/> (normal path) or synthesized directly by <see cref="DetectPersonExecutor"/> (zero-face short-circuit). A non-null value signals "ready for <see cref="NotifyExecutor"/>".</param>
public sealed record MonitoringWorkflowContext(
    string ImageReference,
    Stream ImageStream,
    float[]? EmbeddingVector,
    Guid? CandidateMemberId,
    double? Distance,
    AccessDecisionResult? Decision);
