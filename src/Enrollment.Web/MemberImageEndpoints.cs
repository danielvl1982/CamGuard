using Common;
using Enrollment.Domain;
using Microsoft.Extensions.Options;

namespace Enrollment.Web;

/// <summary>
/// Member image-serving endpoint (member-crud T5). Image files live OUTSIDE any
/// web root (<see cref="VectorStoreOptions.MemberImagesRoot"/>, default
/// <c>photos/members</c>), so bytes are streamed by this minimal API instead of
/// static files. Path traversal is impossible by construction: the route carries
/// only ids, the file served is the one named by the member's own
/// <see cref="MemberImage"/> record (never a raw filename from the route), and the
/// resolved path is additionally confined to the member's folder.
/// </summary>
public static class MemberImageEndpoints
{
    public static void MapMemberImageEndpoints(this WebApplication app)
    {
        // Minimal-API handlers resolve their parameters from the per-request scope,
        // so the scoped repositories below are never captured by a singleton.
        app.MapGet("/api/members/{id:guid}/images/{imageId:guid}", async (
            Guid id,
            Guid imageId,
            IMemberImageRepository images,
            IMemberVectorRepository members,
            IOptions<VectorStoreOptions> options,
            CancellationToken cancellationToken) =>
        {
            var memberId = MemberId.From(id);
            var memberImageId = MemberImageId.From(imageId);
            if (memberId.IsFailure || memberImageId.IsFailure)
            {
                return Results.NotFound();
            }

            var member = await members.FindByIdAsync(memberId.Value, cancellationToken).ConfigureAwait(false);
            if (member is null)
            {
                return Results.NotFound();
            }

            // Serve ONLY a file whose record belongs to this member.
            var stored = await images.ListByMemberAsync(memberId.Value, cancellationToken).ConfigureAwait(false);
            var image = stored.FirstOrDefault(candidate => candidate.Id.Value == memberImageId.Value.Value);
            if (image is null)
            {
                return Results.NotFound();
            }

            if (image.FileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                return Results.NotFound();
            }

            var memberDirectory = Path.GetFullPath(Path.Combine(options.Value.MemberImagesRoot, memberId.Value.Value.ToString()));
            var filePath = Path.GetFullPath(Path.Combine(memberDirectory, image.FileName));
            if (!filePath.StartsWith(memberDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            if (!File.Exists(filePath))
            {
                return Results.NotFound();
            }

            return Results.File(filePath, ContentTypeFor(image.FileName));
        });
    }

    internal static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
}
