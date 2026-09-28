# Data Model: Team News Posts Can Be Edited and Deleted

**Feature**: 057-team-news-edit-delete · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

One new column, one migration. No new entity, no new table, no index change.

## TeamNewsPost (existing, `backend/Entities/TeamNewsPost.cs`)

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `Id` | `Guid` (UUIDv7) | — | Now exposed to the client (FR-018) |
| `TeamId` | `Guid` | — | Cascade from `Team` (unchanged): deleting a team deletes its posts |
| `AuthorUserId` | `Guid` | — | `Restrict` (unchanged). An edit never changes the author (FR-004) |
| `Body` | `string`, max 1000, required | — | Replaced by an edit; always stored trimmed |
| `CreatedDate` | `DateTime` | — | The posting time. Orders the feed; never changes (FR-004, FR-018) |
| `ModifiedDate` | `DateTime` | — | Audit field. Moves on every write, including edits (set explicitly on the `ExecuteUpdate` path, Gate 2) |
| **`EditedDate`** | **`DateTime?`** | **NEW** | When a person last changed the text. `null` = never edited. Set only by the edit path, and only when the trimmed text actually changed (FR-003, FR-004, FR-017). Deliberately **not** derived from `ModifiedDate` (research R4) |

**Validation** (the same rules as posting, FR-002): `Body` is non-empty after trimming and at
most 1,000 characters. It is enforced by the request DTO's attributes under
`[ApiController]` (400), and the service trims before comparing and storing.

**Lifecycle**:

```text
            post (010)                  edit (text changed)           edit (same text)
   ∅ ────────────────────▶ Live ─────────────────────────────▶ Live ─────────────────▶ Live
                     EditedDate = null            EditedDate = now,             (no write)
                                                  Body = new text
                            │
                            │ delete (any admin, FR-013)  /  team deleted (005, cascade)
                            ▼
                            ∅   (hard delete: no tombstone, no history)
```

**Migration** `AddTeamNewsEditedDate`: `ALTER TABLE "TeamNewsPosts" ADD "EditedDate" timestamp
with time zone NULL`. There is no backfill: null on every existing row is FR-017. The down
migration drops the column.

## Notification (existing, `backend/Entities/Notification.cs`), no schema change

This feature adds two new *operations* on rows the 010 engine already writes, and no field.

| Rows affected | Selected by | On edit (FR-006) | On delete (FR-009) |
|---------------|-------------|------------------|--------------------|
| Every `TeamNews` row one post produced, for every recipient, current or former member | `Type == TeamNews` and `DedupeKey` starts with `news:{postId}:` (research R1) | `Payload` rewritten to the corrected `TeamNewsPayload`; `ModifiedDate` = now. `IsRead`, `ReadDate`, `CreatedDate` (inbox order) and `DedupeKey` are untouched | Deleted. Recipients who lost an **unread** row get their badge refreshed after commit |

**`TeamNewsPayload`** (unchanged shape: `teamSlug`, `teamName`, `newsPostId`, `excerpt`). The
rewrite carries the team's current slug and name and the excerpt of the corrected text:
the same 140-character rule as posting (`TeamNewsService.Excerpt`). The slug is immutable,
and the name can't be edited today (#359 would change that, and refreshing it then is
correct).

**Identifying rows by key, not by roster.** The dedupe key is written once per recipient at
fan-out time (`news:{postId}:{recipientId}`) and never changes, so it identifies a former
member's row as reliably as a current member's.

## TeamNewsDto (existing, `backend/Dtos/Teams/TeamDtos.cs`)

```text
TeamNewsDto(
  Guid      Id,                 // NEW, FR-018
  string    AuthorDisplayName,
  string?   AuthorHandle,       // null when the author's profile is gone (banned 013 / erased 037)
  TeamRole  AuthorRole,         // the author's CURRENT role (Member once they have left)
  DateTime  CreatedDate,
  DateTime? EditedDate,         // NEW, null = never edited
  string    Body)
```

No per-viewer permission flags: under FR-013 every admin may act on every post, which the
client already knows from the viewer's relation (research R10).

## HomeNewsDto (existing, `backend/Dtos/Home/HomeDtos.cs`)

```text
HomeNewsDto(
  string    Source,             // "team" | "event" | "party"
  string    SourceName,
  string    SourceSlugOrId,
  string    Body,
  DateTime  CreatedDate,
  DateTime? EditedDate)         // NEW: team posts only; event and party always null (FR-016)
```

`HomeProjections.NewsRaw` gains the same field. The team source projects `n.EditedDate`, and
the event and party sources project `(DateTime?)null`, so their items **cannot** carry the
marker until those sources gain editing.

## Frontend models

- `TeamNews` (`core/models/team.models.ts`): add `id: string`, `editedDate: string | null`.
- `HomeNews` (`core/models/home.models.ts`): add `editedDate: string | null`.
