using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// People search for starting a chat (feature 019, User Story 6; narrowed by feature 046). Matching
/// uses <c>ILike</c> + <c>Unaccent</c>, the convention feature 007 established and every other search
/// surface in this codebase follows.
/// </summary>
/// <remarks>
/// <para>
/// <b>This service reads no message text.</b> Feature 046 removed message-body search from the
/// product: the inbox — its only consumer — now finds conversations by name through
/// <see cref="IChatConversationService.GetInboxAsync"/>, and the removal is from the API rather than
/// just the interface (046 FR-010), because a dormant half that reads message bodies is exactly the
/// surface the security-first principle exists to shrink.
/// </para>
/// <para>
/// What remains is the people half, which the new-chat picker, compose-by-handle (feature 022) and
/// the profile Message action (feature 021) depend on: open reach (019 FR-049) minus the caller and
/// anyone blocked in either direction (FR-033), each hit carrying an existing DM id so a duplicate is
/// never started (FR-008).
/// </para>
/// </remarks>
public sealed class ChatSearchService : IChatSearchService
{
    private readonly AppDbContext _db;

    public ChatSearchService(AppDbContext db) => _db = db;

    public async Task<ChatSearchResultDto> SearchAsync(
        Guid callerId,
        string term,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        var trimmed = term?.Trim() ?? string.Empty;

        if (trimmed.Length < ChatConstants.MinSearchTermLength)
        {
            return Empty(pagination);
        }

        var pattern = $"%{trimmed}%";

        return new ChatSearchResultDto(await SearchPeopleAsync(callerId, pattern, pagination, ct));
    }

    private async Task<PagedResult<PersonHitDto>> SearchPeopleAsync(
        Guid callerId,
        string pattern,
        PaginationRequest pagination,
        CancellationToken ct)
    {
        // Reach is open (FR-049): people search is not restricted to teammates. Two exclusions only —
        // yourself, and anyone either of you has blocked (FR-033), since offering a chat that the send
        // would refuse is a dead end.
        var query = _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId != callerId)
            .Where(p => !_db.UserBlocks.Any(b =>
                (b.BlockerUserId == callerId && b.BlockedUserId == p.UserId)
                || (b.BlockerUserId == p.UserId && b.BlockedUserId == callerId)))
            .Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.DisplayName), AppDbContext.Unaccent(pattern))
                || EF.Functions.ILike(p.Handle, pattern));

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new { p.UserId, p.DisplayName, p.Handle, HasAvatar = p.Avatar != null })
            .ToListAsync(ct);

        var items = new List<PersonHitDto>(rows.Count);
        foreach (var r in rows)
        {
            // Surface an existing DM so the client opens it rather than starting a duplicate (FR-008).
            var pairKey = Conversation.BuildDirectPairKey(callerId, r.UserId);
            var existing = await _db.Conversations.AsNoTracking()
                .Where(c => c.DirectPairKey == pairKey)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            items.Add(new PersonHitDto(r.UserId, r.DisplayName, r.Handle, ChatAvatarUrl.ForPlayer(r.Handle, r.HasAvatar), existing));
        }

        return new PagedResult<PersonHitDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    private static ChatSearchResultDto Empty(PaginationRequest pagination) =>
        new(new PagedResult<PersonHitDto>(Array.Empty<PersonHitDto>(), 0, pagination.NormalizedSkip, pagination.NormalizedTake));
}
