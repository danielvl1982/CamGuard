using Enrollment.Domain;

namespace Enrollment.Infrastructure;

/// <summary>Infrastructure-only mapper from the <see cref="Member"/> aggregate to its vector-store record (task 4.5).</summary>
internal static class MemberVectorRecordMapper
{
    public static MemberVectorRecord FromMember(Member member) => new()
    {
        Id = member.Id.Value.ToString(),
        FullName = member.FullName,
        Embedding = [.. member.Embedding.Vector]
    };
}
