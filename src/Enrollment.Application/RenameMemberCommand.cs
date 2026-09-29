using CSharpFunctionalExtensions;
using Enrollment.Domain;

namespace Enrollment.Application;

/// <summary>Renames an existing member from the detail page (<c>/members/{id}</c>).</summary>
public sealed record RenameMemberCommand(MemberId MemberId, string FullName);

/// <summary>
/// Validates and applies a member rename. Names are trimmed and compared case-insensitively
/// (the same normalization <see cref="IMemberVectorRepository.FindByFullNameAsync"/> uses), and
/// a name already held by ANOTHER member — annulled ones included, since the watch ingestion
/// path treats the name as the member's identity — is rejected. Renaming a member to its own
/// current name (any casing) is a no-op success.
/// </summary>
public sealed class RenameMemberCommandHandler(IMemberVectorRepository memberVectorRepository)
{
    public async Task<UnitResult<RenameMemberError>> HandleAsync(
        RenameMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await memberVectorRepository.FindByIdAsync(command.MemberId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return UnitResult.Failure(RenameMemberError.MemberNotFound);
        }

        var rename = member.Rename(command.FullName);
        if (rename.IsFailure)
        {
            return UnitResult.Failure(RenameMemberError.InvalidName);
        }

        var existing = await memberVectorRepository.FindByFullNameAsync(member.FullName, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // Same member: keep the stored name when nothing changed, otherwise (casing-only change) persist it.
            if (!existing.Id.Equals(member.Id))
            {
                return UnitResult.Failure(RenameMemberError.NameAlreadyInUse);
            }

            if (string.Equals(existing.FullName, member.FullName, StringComparison.Ordinal))
            {
                return UnitResult.Success<RenameMemberError>();
            }
        }

        await memberVectorRepository.RenameAsync(member.Id, member.FullName, cancellationToken)
            .ConfigureAwait(false);

        return UnitResult.Success<RenameMemberError>();
    }
}

public enum RenameMemberError
{
    InvalidName,
    MemberNotFound,
    NameAlreadyInUse
}
