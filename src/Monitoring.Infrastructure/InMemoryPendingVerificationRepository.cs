using System.Collections.Concurrent;
using Monitoring.Application;
using Monitoring.Domain;

namespace Monitoring.Infrastructure;

/// <summary>
/// Simple in-memory <see cref="IPendingVerificationRepository"/> implementation — design.md's
/// stated default persistence for <see cref="PendingVerification"/> records (design.md's Open
/// Questions: "in-memory/file-backed store scoped to Monitoring, upgradeable later without
/// touching Domain"). Registered as a singleton so records accumulate across the process lifetime;
/// records are lost on restart, and replacing this with a durable store never requires a
/// Domain/Application change since both depend only on <see cref="IPendingVerificationRepository"/>.
/// </summary>
public sealed class InMemoryPendingVerificationRepository : IPendingVerificationRepository
{
    private readonly ConcurrentDictionary<PendingVerificationId, PendingVerification> _records = new();

    public Task SaveAsync(PendingVerification pendingVerification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingVerification);
        cancellationToken.ThrowIfCancellationRequested();

        _records[pendingVerification.Id] = pendingVerification;
        return Task.CompletedTask;
    }
}
