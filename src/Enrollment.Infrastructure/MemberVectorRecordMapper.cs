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
        Embedding = [.. member.Embedding.Vector]
    };

    public static Member ToMember(MemberVectorRecord record)
    {
        var memberId = MemberId.From(Guid.Parse(record.Id)).Value;
        var embedding = FaceEmbedding.FromVector(record.Embedding).Value;
        return Member.Rehydrate(memberId, record.FullName, embedding, record.IsAnnulled);
    }

    public static string Normalize(string fullName) => fullName.Trim();
}
