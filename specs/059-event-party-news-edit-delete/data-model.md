# Data Model: Event and Party News Posts Can Be Edited and Deleted

**Feature**: 059-event-party-news-edit-delete · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

Two new nullable columns, one migration. No new entity, no new table, no index change, no
change to the `Notifications` schema.

## EventNewsPost (existing, `backend/Entities/EventNewsPost.cs`)

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `Id` | `Guid` (UUIDv7) | — | Already on `EventNewsDto` |
| `EventId` | `Guid` | — | Cascade from `Event` (unchanged) |
| `AuthorUserId` | `Guid` | — | Never changed by an edit (FR-004) |
| `Body` | `string`, max 2000, required | — | Replaced by an edit; stored trimmed |
| `CreatedDate` | `DateTime` | — | Posting time; orders the feed; never changes |
| `ModifiedDate` | `DateTime` | — | Audit field. **Set explicitly** on the edit's `ExecuteUpdate` (Gate 2) |
| **`EditedDate`** | **`DateTime?`** | **NEW** | When a person last changed the text; `null` = never edited. Set only by the edit path, only when the trimmed text changed (FR-003, FR-004, FR-018) |

## PartyNewsPost (existing, `backend/Entities/PartyNewsPost.cs`)

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `Id` | `Guid` (UUIDv7) | — | Already on `PartyNewsDto` |
| `PartyId` | `Guid` | — | Cascade from `Party` (unchanged: disbanding deletes the posts) |
| `AuthorUserId` | `Guid` | — | Never changed by an edit |
| `Body` | `string`, max 1000, required | — | Replaced by an edit; stored trimmed |
| `CreatedDate` | `DateTime` | — | Posting time; orders the feed |
| `ModifiedDate` | `DateTime` | — | **Set explicitly** on the edit's `ExecuteUpdate` (Gate 2) |
| **`EditedDate`** | **`DateTime?`** | **NEW** | As on `EventNewsPost` |

**Validation** (FR-002): the posting rules of each kind — non-empty after trimming; ≤ 2,000
characters (event) / ≤ 1,000 (party). Enforced by the request DTO's attributes under
`[ApiController]` (400); the service trims before comparing and storing.

**Lifecycle** (both kinds):

```text
          post (unchanged)            edit (text changed)            edit (same text)
   ∅ ─────────────────────▶ Live ───────────────────────────▶ Live ─────────────────▶ Live
                    EditedDate = null          EditedDate = now,              (no write)
                                               Body = new text
                          │
                          │ delete (any current admin)  /  event deleted · party disbanded (cascade)
                          ▼
                          ∅   (hard delete)
```

**Migration** `AddEventAndPartyNewsEditedDate`:
`ALTER TABLE "EventNewsPosts" ADD "EditedDate" timestamp with time zone NULL;`
`ALTER TABLE "PartyNewsPosts" ADD "EditedDate" timestamp with time zone NULL;`
No backfill (null on every existing row *is* FR-018). The down migration drops both columns.

## Notification (existing), no schema change

| Rows | Selected by | On party edit | On party delete |
|------|-------------|---------------|-----------------|
| Every `PartyNews` row one post produced, for every recipient, current or former crew | `Type == PartyNews` ∧ `DedupeKey` starts with `party-news:{postId}:` (research R2) | **Untouched** — no write at all; the payload `{partyId, eventId, teamSlug, eventName, teamName}` quotes no text (R1, FR-006) | Deleted, in the same transaction as the post; recipients who lost an **unread** row get their badge refreshed after commit (FR-009) |

Event news produces no rows, so neither event operation touches this table.

## DTOs

```text
EventNewsDto(                       // GET /events/{id}/news items, POST (201), PATCH (200)
  Guid      Id,
  string    AuthorDisplayName,      // MemberPlaceholder when the author is banned/erased
  string    Body,
  DateTime  CreatedDate,
  DateTime? EditedDate)             // NEW — null = never edited; POST returns null

PartyNewsDto(                       // GET /parties/{id}/news items, POST (201), PATCH (200)
  Guid            Id,
  string          AuthorDisplayName,
  PartyMemberRole AuthorRole,       // the author's CURRENT party role
  string          Body,
  DateTime        CreatedDate,
  DateTime?       EditedDate)       // NEW

HomeNewsDto                         // unchanged shape (057 added EditedDate); the event and party
                                    // projections now write n.EditedDate instead of null (R7)
```

## Frontend models

- `EventNews` (`core/models/event.models.ts`): add `editedDate: string | null`.
- `PartyNews` (`core/models/party.models.ts`): add `editedDate: string | null`.
- `HomeNews`: unchanged.
