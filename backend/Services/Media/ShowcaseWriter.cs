using System.Linq.Expressions;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Media;

/// <summary>Which owner a gallery belongs to — selects the row the writer locks.</summary>
public enum ShowcaseOwner
{
    Profile,
    Team,
}

/// <summary>
/// The write core both showcase galleries share (feature 046 / #99): add, remove, reorder — each
/// serialized per owner and applied all-or-nothing. Owner-agnostic on purpose, so the profile and
/// team surfaces cannot drift apart on the one thing that must hold identically for both: the cap.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a pessimistic lock rather than a count.</b> "Read the count, refuse at five, otherwise
/// insert" is a time-of-check/time-of-use race: two uploads against a four-picture gallery both read
/// four and both insert, and the cap the whole feature rests on (spec FR-001/FR-002) is gone. So
/// every mutation takes <c>SELECT … FOR UPDATE</c> on the <em>owner's</em> row first, which
/// serializes concurrent writers for that one gallery and leaves every other gallery unaffected.
/// This is not a new idiom here: <c>TeamService.MutateMembershipAsync</c> already guards the
/// last-admin rule exactly this way.
/// </para>
/// <para>
/// <b>Why not a unique index on (owner, position).</b> It looks like the cheaper guarantee and is
/// the wrong one twice over. Ten simultaneous adds against an empty gallery would all choose
/// position 0, so <em>one</em> would be admitted and nine refused — where the requirement is five
/// admitted (spec SC-002). And it would break reorder: EF issues one UPDATE per row, so a
/// permutation passes through a transient duplicate that a non-deferrable constraint rejects.
/// </para>
/// <para>
/// <b>Why the execution strategy wraps everything.</b> Constitution Principle VII: a retrying
/// execution strategy forbids user-initiated transactions unless the whole transaction is a single
/// retriable unit, with all state mutation inside the delegate. The change tracker is cleared first
/// because a rollback does not undo it, so a replay would re-apply what the previous attempt staged.
/// </para>
/// <para>
/// <b>What this class does not do.</b> It never touches the media store — a blob and a row cannot
/// share a transaction, so object writes and deletes are the caller's job, sequenced around this
/// call (see <c>ProfileShowcaseService.AddAsync</c>). And it makes no authorization decision: the
/// calling service has already established that the actor may write to this gallery.
/// </para>
/// </remarks>
public sealed class ShowcaseWriter
{
    /// <summary>
    /// The hard cap, per owner (spec FR-001). A platform constant rather than configuration: it is
    /// the same in every environment, and changing it is a product decision, not an operator one.
    /// </summary>
    public const int MaxImagesPerOwner = 5;

    private readonly AppDbContext _db;

    public ShowcaseWriter(AppDbContext db) => _db = db;

    /// <summary>
    /// Insert <paramref name="create"/>'s image at the end of the owner's gallery, or refuse with
    /// <see cref="ShowcaseAddStatus.GalleryFull"/>.
    /// </summary>
    /// <remarks>
    /// <c>create</c> builds the entity for a given position and is called <em>inside</em> the
    /// transaction, so a replay builds a fresh instance rather than re-adding a stale one. The object
    /// key it carries is minted — and its object stored — before this call; see the calling service.
    /// </remarks>
    public async Task<ShowcaseAddResult> AddAsync<T>(
        ShowcaseOwner owner,
        Guid ownerId,
        Expression<Func<T, bool>> ownedByOwner,
        Func<int, T> create,
        CancellationToken ct = default)
        where T : class, IShowcaseImage
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await LockOwnerAsync(owner, ownerId, ct);

            var count = await _db.Set<T>().CountAsync(ownedByOwner, ct);
            if (count >= MaxImagesPerOwner)
            {
                await tx.RollbackAsync(ct);
                return ShowcaseAddResult.Fail(ShowcaseAddStatus.GalleryFull);
            }

            var image = create(count);
            _db.Set<T>().Add(image);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ShowcaseAddResult.Ok(image.Id, count);
        });
    }

    /// <summary>
    /// Remove one image and close the gap behind it, returning the stored object key so the caller
    /// can delete the bytes. Removing a row does not remove the object.
    /// </summary>
    public async Task<ShowcaseRemoveResult> RemoveAsync<T>(
        ShowcaseOwner owner,
        Guid ownerId,
        Guid imageId,
        Expression<Func<T, bool>> ownedByOwner,
        CancellationToken ct = default)
        where T : class, IShowcaseImage
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await LockOwnerAsync(owner, ownerId, ct);

            var images = await OwnedOrderedAsync(ownedByOwner, ct);
            var target = images.FirstOrDefault(i => i.Id == imageId);
            if (target is null)
            {
                await tx.RollbackAsync(ct);
                return ShowcaseRemoveResult.Fail(ShowcaseMutateStatus.NotFound);
            }

            var objectKey = target.ObjectKey;
            _db.Set<T>().Remove(target);

            // Close the gap so positions stay dense (0..n-1). Done here rather than lazily on read
            // because "the third picture" must mean the same thing to the next writer as it does to
            // a viewer.
            Compact(images.Where(i => i.Id != imageId));

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ShowcaseRemoveResult.Ok(objectKey);
        });
    }

    /// <summary>
    /// Apply a complete new order, or refuse the whole thing.
    /// </summary>
    /// <remarks>
    /// <paramref name="orderedIds"/> must be an exact permutation of the owner's current images.
    /// Anything else — wrong length, a duplicate, an id belonging to someone else, or one a
    /// co-admin removed while the caller's page was open — is
    /// <see cref="ShowcaseMutateStatus.StaleOrder"/> with nothing written, which is what tells the
    /// client its view is out of date (spec FR-010).
    /// </remarks>
    public async Task<ShowcaseMutateStatus> ReorderAsync<T>(
        ShowcaseOwner owner,
        Guid ownerId,
        IReadOnlyList<Guid> orderedIds,
        Expression<Func<T, bool>> ownedByOwner,
        CancellationToken ct = default)
        where T : class, IShowcaseImage
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await LockOwnerAsync(owner, ownerId, ct);

            var images = await OwnedOrderedAsync(ownedByOwner, ct);
            var byId = images.ToDictionary(i => i.Id);

            var distinct = new HashSet<Guid>(orderedIds);
            if (orderedIds.Count != images.Count
                || distinct.Count != orderedIds.Count
                || !distinct.All(byId.ContainsKey))
            {
                await tx.RollbackAsync(ct);
                return ShowcaseMutateStatus.StaleOrder;
            }

            Compact(orderedIds.Select(id => byId[id]));

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ShowcaseMutateStatus.Success;
        });
    }

    /// <summary>
    /// Pessimistic lock on the owner's row, which is what serializes concurrent writers for one
    /// gallery. The table name is chosen from a closed enum and never interpolated from input.
    /// </summary>
    private Task LockOwnerAsync(ShowcaseOwner owner, Guid ownerId, CancellationToken ct) => owner switch
    {
        ShowcaseOwner.Profile => _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"PlayerProfiles\" WHERE \"Id\" = {ownerId} FOR UPDATE", ct),
        ShowcaseOwner.Team => _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Teams\" WHERE \"Id\" = {ownerId} FOR UPDATE", ct),
        _ => throw new ArgumentOutOfRangeException(nameof(owner), owner, "Unknown showcase owner."),
    };

    /// <summary>
    /// The owner's images, tracked (they are about to be renumbered) and in the order viewers see.
    /// Bounded by <see cref="MaxImagesPerOwner"/>, so this is not an unbounded read.
    /// </summary>
    private async Task<List<T>> OwnedOrderedAsync<T>(Expression<Func<T, bool>> ownedByOwner, CancellationToken ct)
        where T : class, IShowcaseImage =>
        await _db.Set<T>()
            .Where(ownedByOwner)
            .OrderBy(i => i.Position)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

    /// <summary>Renumber a sequence to dense 0-based positions in the order given.</summary>
    private static void Compact<T>(IEnumerable<T> images) where T : class, IShowcaseImage
    {
        var position = 0;
        foreach (var image in images)
        {
            image.Position = position++;
        }
    }
}
