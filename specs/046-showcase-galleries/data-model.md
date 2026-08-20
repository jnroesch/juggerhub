# Phase 1 Data Model — Showcase Image Galleries (046 / #99)

Two new entities, one migration, no changes to any existing table. Both mirror `ProfileAvatar`
(feature 035) in shape and in EF configuration; the differences are called out where they exist and
each has a reason.

---

## `ProfileShowcaseImage`

`backend/Entities/ProfileShowcaseImage.cs` — one picture in a player's showcase.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | `BaseEntity`, UUIDv7 (Principle III). Appears in the image URL. |
| `CreatedDate` / `ModifiedDate` | `DateTime` | `BaseEntity`, set by `AuditFieldsInterceptor`. |
| `ProfileId` | `Guid` | FK → `PlayerProfile`. **Cascade** delete, like `ProfileAvatar`. |
| `Profile` | `PlayerProfile` | Owner navigation. Load-bearing: the ban query filter is expressed through it (R1/R3). |
| `Position` | `int` | Dense, 0-based within the owner. Order is `(Position, Id)` so it is total and identical for every viewer (FR-006). |
| `Caption` | `string?` | Max 120 chars, nullable = no caption (FR-005). Plain text; never rendered as markup. |
| `ContentType` | `string` | Max 64. Always `image/webp` after processing. |
| `ObjectKey` | `string` | Max `MediaObjectKey.MaxLength` (200). **Never leaves the backend** — not in a DTO, header, or link (FR-022). |
| `SizeBytes` | `int` | Lets a descriptor describe a fetch without touching the store. |

**EF configuration**

```
HasQueryFilter(g => g.Profile.User.Status != AccountStatus.Banned)   // FR-019, matches ProfileAvatar
Property(g => g.Caption).HasMaxLength(120)
Property(g => g.ContentType).HasMaxLength(64).IsRequired()
Property(g => g.ObjectKey).HasMaxLength(MediaObjectKey.MaxLength).IsRequired()
HasIndex(g => new { g.ProfileId, g.Position })                        // NOT unique — see below
HasIndex(g => g.ObjectKey).IsUnique()                                 // two descriptors must never share an object
HasOne(g => g.Profile).WithMany(p => p.ShowcaseImages)
    .HasForeignKey(g => g.ProfileId).OnDelete(DeleteBehavior.Cascade)
```

**Why `(ProfileId, Position)` is not unique.** Uniqueness is maintained by the writer, not the
schema: every add and every reorder runs inside the per-owner `FOR UPDATE` transaction (R2), so no
two writers ever assign positions concurrently. A unique constraint would additionally have to be
`DEFERRABLE` — EF issues one `UPDATE` per row during a reorder, and a non-deferrable constraint
rejects the transient duplicate mid-permutation. The index still exists, non-uniquely, because every
read orders by it.

---

## `TeamShowcaseImage`

`backend/Entities/TeamShowcaseImage.cs` — identical, with two deliberate differences.

| Property | Type | Notes |
|----------|------|-------|
| `TeamId` | `Guid` | FK → `Team`. **Cascade** delete — consistent with memberships/invitations/news. |
| `Team` | `Team` | Owner navigation. |
| *(everything else)* | | Exactly as above. |

**Difference 1 — no query filter.** A team's gallery is not hidden because a member was banned
(R3). There is deliberately nothing to filter on.

**Difference 2 — no uploader column.** `TeamShowcaseImage` stores no `UploadedByUserId`: nothing
reads it, and a `UserId`-keyed column would enter account deletion's inventory for no benefit (R4).

---

## Owner-side navigations

```csharp
// PlayerProfile
public ICollection<ProfileShowcaseImage> ShowcaseImages { get; set; } = [];

// Team
public ICollection<TeamShowcaseImage> ShowcaseImages { get; set; } = [];
```

Added for the cascade configuration and for `Count()` inside the locked transaction. Read paths use
`_db.ProfileShowcaseImages` / `_db.TeamShowcaseImages` directly with `.Select` projections, never a
lazy navigation (Principle III).

---

## `DbSet`s

```csharp
public DbSet<ProfileShowcaseImage> ProfileShowcaseImages => Set<ProfileShowcaseImage>();
public DbSet<TeamShowcaseImage> TeamShowcaseImages => Set<TeamShowcaseImage>();
```

---

## Invariants

| # | Invariant | Enforced by |
|---|-----------|-------------|
| I1 | At most 5 rows per owner | `FOR UPDATE` on the owner row + re-count inside the transaction (R2) |
| I2 | Positions are `0..n-1`, contiguous, no gaps | The service rewrites positions on every add, delete, and reorder, inside the same transaction |
| I3 | Order is total and viewer-independent | `OrderBy(Position).ThenBy(Id)` in every projection |
| I4 | One object per descriptor, one descriptor per object | Unique index on `ObjectKey`; key minted per upload |
| I5 | A banned member's rows are invisible | Query filter on `ProfileShowcaseImage` |
| I6 | An object key never reaches a client | No DTO field carries it; the ETag is a hash (`MediaResponse.Fingerprint`) |
| I7 | No object outlives its owner | Explicit reclaim in `AccountDeletionService` and `TeamService.DeleteAsync` (R7); sweep as backstop |

---

## Migration

One migration, `AddShowcaseGalleries`:

- creates `ProfileShowcaseImages` and `TeamShowcaseImages` with the columns above,
- two cascade FKs, two `(OwnerId, Position)` indexes, two unique `ObjectKey` indexes,
- **no backfill, nothing dropped, no existing table touched**. Every gallery starts empty.

If a task in this feature produces a migration that alters an existing table, something is wrong.

---

## DTOs

`backend/Dtos/Profile/ShowcaseImageDto.cs` and `backend/Dtos/Teams/…` — or one shared shape, since
both surfaces return the same fields:

```csharp
public sealed record ShowcaseImageDto(Guid Id, string? Caption, int Position);
```

That is the **entire** DTO. No object key, no size, no content type, no URL — the client composes
the image address from the owner it already knows plus `Id`, exactly as it composes the avatar URL
from a handle today. `SizeBytes` and `ContentType` stay server-side; nothing in the UI needs them,
and every field not sent is a field that cannot leak (Principle I/II).

Write-side request records:

```csharp
public sealed record UpdateShowcaseCaptionRequest(string? Caption);
public sealed record ReorderShowcaseRequest(IReadOnlyList<Guid> ImageIds);
```

Uploads are `IFormFile`, matching `PUT /profiles/me/avatar`.

---

## Service result types

Mirroring `AvatarSetResult` / `AvatarSetStatus`:

```csharp
public enum ShowcaseAddStatus { Success, OwnerNotFound, Forbidden, GalleryFull, Empty, InvalidType, TooLarge, TooManyPixels, Unreadable, StoreUnavailable }
public enum ShowcaseMutateStatus { Success, NotFound, Forbidden, StaleOrder }
```

`GalleryFull` is distinct from every processing failure so the client can say "you already have
five" rather than "invalid image" (FR-016). `StaleOrder` is the reorder-is-not-a-permutation
refusal (FR-010) and is what tells the client to reload.
