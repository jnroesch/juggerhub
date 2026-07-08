# Data Model: In-App Notification System

## Entity: `Notification`

Derives from `BaseEntity` (`Id` UUIDv7, `CreatedDate`, `ModifiedDate` set by the audit
interceptor). One row = one item addressed to exactly one recipient.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | UUIDv7, from `BaseEntity`. |
| `RecipientUserId` | `Guid` (FK → `AspNetUsers`) | Owner. All reads/mutations scoped to this. `OnDelete: Cascade` (a deleted user's notifications go with them). Required. |
| `Type` | `NotificationType` (enum, stored as `int`) | Discriminator → icon, rendering, target, inline actions. |
| `Payload` | `string` (`jsonb`) | Serialized, type-specific structured data (see schemas). Never trusted for authorization; render-only + action-target hints. |
| `IsRead` | `bool` | Default `false`. |
| `ReadDate` | `DateTime?` | Set when first marked read; null while unread. |
| `ActorUserId` | `Guid?` (FK → `AspNetUsers`, `OnDelete: SetNull`) | Who caused it (for "so-and-so did X"); nullable/system. Kept out of payload so it survives actor rename and can be joined for display. |
| `DedupeKey` | `string?` (max 200) | Optional natural idempotency key (e.g. `invite:{invitationId}`) to suppress duplicate unread notifications for the same logical event + recipient. |

### Indexes

- `(RecipientUserId, CreatedDate DESC)` — the inbox list (newest-first, per recipient).
- `(RecipientUserId, IsRead)` partial `WHERE NOT "IsRead"` — the unread count / badge.
- Unique `(RecipientUserId, DedupeKey)` partial `WHERE "DedupeKey" IS NOT NULL` — idempotency;
  a concurrent duplicate loses the race and is treated as already-created.

### Relationships / lifecycle

- `Recipient` (User) 1—* `Notification` (cascade delete).
- `Actor` (User) 0..1 — set null on actor delete.
- Source rows (team, invite, news) are **not** FK'd from the notification — they are referenced by
  id **inside the payload** so that deleting a source degrades gracefully (US3 AC, edge cases): a
  dangling id just renders a non-navigating/soft row, never a broken join or cascade surprise.

## Enum: `NotificationType`

Serialized by name (global `JsonStringEnumConverter`). Extensible — new members append without
touching existing rows.

| Member | Meaning | Inline actions? | Target |
|--------|---------|-----------------|--------|
| `TeamInvite` | Targeted invite to join a team | Accept / Decline | invite (via `/join/{slug}/{token}` or inline) |
| `TeamRoleChanged` | Recipient's role in a team changed | none | team space `/t/{slug}` |
| `TeamNews` | New team news post | none | team news `/t/{slug}` (news) |

## Payload schemas (per type)

Stored as `jsonb`; deserialized to a typed record for DTO mapping. All fields are display/target
hints only.

- **TeamInvite**: `{ invitationId, token, teamSlug, teamName, inviterName }`
  - Inline actions call the existing invitation endpoints keyed by `token`. `InviteResolved`
    render state is derived at read time from the live invite status (not stored), so an
    out-of-band accept/decline/expiry reconciles correctly.
- **TeamRoleChanged**: `{ teamSlug, teamName, newRole }`
- **TeamNews**: `{ teamSlug, teamName, newsPostId, excerpt }` (`excerpt` = first N chars of body).

## DTOs (client-facing, mapped via Mapster)

- `NotificationDto`: `{ id, type, createdDate, isRead, actorDisplayName?, payload (typed union), resolved? }`
  where `resolved` applies to `TeamInvite` (true when the underlying invite is no longer usable).
- `UnreadCountDto`: `{ count }` (capped display handled client-side).
- Requests: `MarkReadRequest` is unnecessary — mark-read targets a route id; mark-all is a bare POST.

## New capability touching existing model: team news posting

No schema change — reuses `TeamNewsPost` (already mapped). Adds a write path:
`TeamNewsService.PostAsync(slug, actorUserId, body)` → admin-gated via `TeamMembershipGuard`,
inserts a `TeamNewsPost`, then fans out `TeamNews` notifications to every other current member.

## Migration

`AddNotifications` — creates `Notifications` table + the three indexes above. `Payload` column is
`jsonb`. FKs: `RecipientUserId` (cascade), `ActorUserId` (set null). No data backfill.
