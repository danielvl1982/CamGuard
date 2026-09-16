using Enrollment.PublicApi;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Monitoring.Application;

/// <summary>
/// Second workflow stage: queries <see cref="IMemberDirectoryReader"/> (Enrollment's only
/// exposed surface) with the detected face embedding, per the <c>identity-verification</c> spec.
/// <see cref="MemberMatch.Score"/> is forwarded bit-for-bit as <c>Distance</c> — never rounded,
/// truncated, or reinterpreted as a similarity score — because access-decision-policy's threshold
/// boundary evaluation depends on the exact value. Emits the <c>Monitoring.VerifyIdentity</c> span
/// per design.md's Observability table.
/// </summary>
public sealed class VerifyIdentityExecutor(
    IMemberDirectoryReader memberDirectoryReader,
    [FromKeyedServices(MonitoringObservability.ActivitySourceName)] ActivitySource activitySource)
    : Executor<MonitoringWorkflowContext, MonitoringWorkflowContext>(nameof(VerifyIdentityExecutor))
{
    private readonly IMemberDirectoryReader _memberDirectoryReader =
        memberDirectoryReader ?? throw new ArgumentNullException(nameof(memberDirectoryReader));

    private readonly ActivitySource _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

    public override async ValueTask<MonitoringWorkflowContext> HandleAsync(
        MonitoringWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var activity = _activitySource.StartActivity($"{nameof(Monitoring)}.VerifyIdentity");

        var match = await _memberDirectoryReader
            .FindBestMatchAsync(input.EmbeddingVector!, cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag("match.found", match is not null);

        return input with
        {
            CandidateMemberId = match?.MemberId,
            Distance = match?.Score
        };
    }
}
