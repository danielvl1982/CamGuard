using Enrollment.PublicApi;
using Microsoft.Agents.AI.Workflows;

namespace Monitoring.Application;

/// <summary>
/// Second workflow stage: queries <see cref="IMemberDirectoryReader"/> (Enrollment's only
/// exposed surface) with the detected face embedding, per the <c>identity-verification</c> spec.
/// <see cref="MemberMatch.Score"/> is forwarded bit-for-bit as <c>Distance</c> — never rounded,
/// truncated, or reinterpreted as a similarity score — because access-decision-policy's threshold
/// boundary evaluation depends on the exact value.
/// </summary>
public sealed class VerifyIdentityExecutor(IMemberDirectoryReader memberDirectoryReader)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(VerifyIdentityExecutor))
{
    private readonly IMemberDirectoryReader _memberDirectoryReader =
        memberDirectoryReader ?? throw new ArgumentNullException(nameof(memberDirectoryReader));

    public override async ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var match = await _memberDirectoryReader
            .FindBestMatchAsync(input.EmbeddingVector!, cancellationToken)
            .ConfigureAwait(false);

        return input with
        {
            CandidateMemberId = match?.MemberId,
            Distance = match?.Score
        };
    }
}
