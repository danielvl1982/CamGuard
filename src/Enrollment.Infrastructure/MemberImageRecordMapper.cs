using Enrollment.Domain;

namespace Enrollment.Infrastructure;

/// <summary>Infrastructure-only mapper between the <see cref="MemberImage"/> read-model and its vector-store record.</summary>
internal static class MemberImageRecordMapper
{
    public static MemberImageRecord FromImage(MemberImage image, string filePath) => new()
    {
        Id = image.Id.Value.ToString(),
        MemberId = image.MemberId.Value.ToString(),
        FilePath = filePath,
        StoredAtUtc = image.StoredAtUtc.ToUniversalTime().ToString("O"),
        Embedding = [.. image.Embedding.Vector]
    };

    public static MemberImage ToImage(MemberImageRecord record)
    {
        var imageId = MemberImageId.From(Guid.Parse(record.Id)).Value;
        var memberId = MemberId.From(Guid.Parse(record.MemberId)).Value;
        var embedding = FaceEmbedding.FromVector(record.Embedding).Value;
        var storedAtUtc = DateTime.Parse(record.StoredAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
        return MemberImage.Rehydrate(imageId, memberId, Path.GetFileName(record.FilePath), embedding, storedAtUtc);
    }
}
