using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Teams;

/// <summary>
/// EF-Core-direct implementation of <see cref="ITeamLogoService"/> (feature 051 / #305).
/// </summary>
/// <remarks>
/// The write path is the one feature 035 established for avatars and catalogue icons, and is
/// copied rather than re-derived: mint the key, write the object, commit the descriptor, and only
/// then delete the object this one replaces. A row and a blob cannot share a transaction, so some
/// failure window is unavoidable; this ordering picks the harmless one. Failing after the write
/// leaves an unreferenced object the reconciliation sweep reclaims, whereas deleting first would
/// leave a team with no logo at all.
/// </remarks>
public sealed class TeamLogoService : ITeamLogoService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly IImageProcessor _imageProcessor;
    private readonly IMediaStore _mediaStore;
    private readonly ImageProcessingOptions _imageOptions;

    public TeamLogoService(
        AppDbContext db,
        TeamMembershipGuard guard,
        IImageProcessor imageProcessor,
        IMediaStore mediaStore,
        IOptions<ImageProcessingOptions> imageOptions)
    {
        _db = db;
        _guard = guard;
        _imageProcessor = imageProcessor;
        _mediaStore = mediaStore;
        _imageOptions = imageOptions.Value;
    }

    public async Task<TeamLogoResult> SetAsync(
        string slug, Guid actorUserId, byte[] content, CancellationToken ct = default)
    {
        // Normalize BEFORE authorizing? No — authorize first. Processing a 8 MB upload from
        // somebody with no right to change this team would let an outsider spend our CPU, and
        // the refusal has to be indistinguishable from an unknown team either way.
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamLogoResult.Fail(TeamLogoStatus.NotFoundOrNotMember);
        }

        if (!a.IsAdmin)
        {
            return TeamLogoResult.Fail(TeamLogoStatus.Forbidden, "Only admins can change the team logo.");
        }

        // Feature 034 / #98: validate + decompression-bomb guard + strip metadata + centre-crop +
        // re-encode to WebP. The declared content type is never trusted. On any rejection we
        // return without touching the stored logo (spec FR-007).
        var processed = _imageProcessor.Process(content, _imageOptions.TeamLogo);
        if (processed.Status != ImageProcessingStatus.Success)
        {
            return TeamLogoResult.Fail(MapProcessingStatus(processed.Status), processed.Reason!);
        }

        // Operate on the DbSet directly (not via the 1:1 navigation) so EF issues a clean INSERT
        // for a new logo and an UPDATE for an existing one.
        var logo = await _db.TeamLogos.FirstOrDefaultAsync(l => l.TeamId == a.TeamId, ct);

        var objectKey = MediaObjectKey.Create(MediaKind.TeamLogo);
        var supersededKey = logo?.ObjectKey;

        using (var normalized = new MemoryStream(processed.Bytes!))
        {
            await _mediaStore.PutAsync(objectKey, normalized, processed.ContentType!, ct);
        }

        if (logo is null)
        {
            _db.TeamLogos.Add(new TeamLogo
            {
                TeamId = a.TeamId,
                ObjectKey = objectKey,
                SizeBytes = processed.Bytes!.Length,
                ContentType = processed.ContentType!,
            });
        }
        else
        {
            logo.ObjectKey = objectKey;
            logo.SizeBytes = processed.Bytes!.Length;
            logo.ContentType = processed.ContentType!;
        }

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(supersededKey))
        {
            await _mediaStore.DeleteAsync(supersededKey, ct);
        }

        return TeamLogoResult.Ok();
    }

    public async Task<TeamLogoResult> RemoveAsync(string slug, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamLogoResult.Fail(TeamLogoStatus.NotFoundOrNotMember);
        }

        if (!a.IsAdmin)
        {
            return TeamLogoResult.Fail(TeamLogoStatus.Forbidden, "Only admins can change the team logo.");
        }

        var logo = await _db.TeamLogos.FirstOrDefaultAsync(l => l.TeamId == a.TeamId, ct);
        if (logo is null)
        {
            // Idempotent (spec FR-017): the requested state is the state we are in.
            return TeamLogoResult.Ok();
        }

        var objectKey = logo.ObjectKey;
        _db.TeamLogos.Remove(logo);
        await _db.SaveChangesAsync(ct);

        // Row first, object second — the mirror of the write ordering, and harmless in the same
        // direction: a failure here strands an object for the sweep rather than leaving a
        // descriptor pointing at bytes that are gone.
        await _mediaStore.DeleteAsync(objectKey, ct);

        return TeamLogoResult.Ok();
    }

    public async Task<MediaContent?> GetAsync(string slug, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);

        // Step 1 — read the DESCRIPTOR only.
        var data = await _db.TeamLogos
            .AsNoTracking()
            .Where(l => l.Team.Slug == normalized)
            .Select(l => new { l.ObjectKey, l.ContentType })
            .FirstOrDefaultAsync(ct);

        if (data is null)
        {
            return null;
        }

        // Step 2 — and ONLY now touch the media store. There is no visibility decision to make
        // here (spec FR-012), but the ordering is kept so this method reads like its siblings and
        // so a future gate has an obvious place to go.
        var stream = await _mediaStore.OpenReadAsync(data.ObjectKey, ct);

        // A descriptor whose object is missing degrades to the ordinary "no logo" outcome rather
        // than an error — every surface already renders a placeholder for that (spec FR-014).
        return stream is null ? null : new MediaContent(stream, data.ContentType, data.ObjectKey);
    }

    /// <summary>Map a processing rejection onto the logo-upload status, keeping the reasons distinct.</summary>
    private static TeamLogoStatus MapProcessingStatus(ImageProcessingStatus status) => status switch
    {
        ImageProcessingStatus.Empty => TeamLogoStatus.Empty,
        ImageProcessingStatus.UnsupportedType => TeamLogoStatus.InvalidType,
        ImageProcessingStatus.InputTooLarge => TeamLogoStatus.TooLarge,
        ImageProcessingStatus.OutputTooLarge => TeamLogoStatus.TooLarge,
        ImageProcessingStatus.DimensionsTooLarge => TeamLogoStatus.DimensionsTooLarge,
        ImageProcessingStatus.Unreadable => TeamLogoStatus.Unreadable,
        _ => TeamLogoStatus.InvalidType,
    };
}
