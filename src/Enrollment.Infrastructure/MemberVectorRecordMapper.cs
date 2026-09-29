using Enrollment.Domain;

namespace Enrollment.Infrastructure;

/// <summary>Infrastructure-only mapper from the <see cref="Member"/> aggregate to its vector-store record (task 4.5).</summary>
internal static class MemberVectorRecordMapper
{
    public static MemberVectorRecord FromMember(Member member) => new()
    {
        Id = member.Id.Value.ToString(),
        FullName = member.FullName,
        IsAnnulled = member.IsAnnulled,
        RoleId = member.RoleId?.Value.ToString(),
        Embedding = [.. member.Embedding.Vector]
    };

    public static Member ToMember(MemberVectorRecord record)
    {
        var memberId = MemberId.From(Guid.Parse(record.Id)).Value;
        var embedding = FaceEmbedding.FromVector(record.Embedding).Value;
        // A missing or unparsable stored role id reads back as unassigned.
        var roleId = Guid.TryParse(record.RoleId, out var roleGuid) && RoleId.From(roleGuid) is { IsSuccess: true } parsed
            ? parsed.Value
            : null;
        return Member.Rehydrate(memberId, record.FullName, embedding, record.IsAnnulled, roleId);
    }

    public static string Normalize(string fullName) => fullName.Trim();
}
