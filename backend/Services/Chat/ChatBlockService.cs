using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Blocking (feature 019, User Story 5).
/// </summary>
/// <remarks>
/// The enforcement lives at the three call sites a block has to hold on — starting a DM, sending to
/// one, and people search (data-model R17) — because those are the paths that would otherwise let it
/// be walked around. This service only owns the block records themselves.
/// </remarks>
public sealed class ChatBlockService : IChatBlockService
{
    private readonly AppDbContext _db;

    public ChatBlockService(AppDbContext db) => _db = db;

    public async Task<PagedResult<BlockedUserDto>> ListAsync(
        Guid callerId,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        var query = _db.UserBlocks.AsNoTracking().Where(b => b.BlockerUserId == callerId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.Id)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(b => new BlockedUserDto(
                b.BlockedUserId,
                // A banned account's profile is filtered out globally (013) — show the placeholder
                // rather than dropping the row, or the blocker could not unblock them.
                b.Blocked.Profile!.DisplayName ?? ChatConversationService.PlaceholderName,
                b.Blocked.Profile!.Handle,
                b.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<BlockedUserDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<ChatResult> BlockAsync(Guid callerId, Guid targetUserId, CancellationToken ct = default)
    {
        if (callerId == targetUserId)
        {
            return ChatResult.Fail(ChatOutcome.Invalid, "You can't block yourself.");
        }

        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, ct);
        if (!exists)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        var already = await _db.UserBlocks.AsNoTracking()
            .AnyAsync(b => b.BlockerUserId == callerId && b.BlockedUserId == targetUserId, ct);

        if (already)
        {
            return ChatResult.Ok();
        }

        _db.UserBlocks.Add(new UserBlock { BlockerUserId = callerId, BlockedUserId = targetUserId });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Double-tap from two tabs; the unique index made it idempotent for us.
            _db.ChangeTracker.Clear();
        }

        return ChatResult.Ok();
    }

    public async Task<ChatResult> UnblockAsync(Guid callerId, Guid targetUserId, CancellationToken ct = default)
    {
        // The block record is dropped; the conversation and its history are untouched, so unblocking
        // restores the thread exactly as it was (spec FR-030).
        await _db.UserBlocks
            .Where(b => b.BlockerUserId == callerId && b.BlockedUserId == targetUserId)
            .ExecuteDeleteAsync(ct);

        return ChatResult.Ok();
    }
}
