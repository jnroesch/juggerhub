# Data Model: Removing a Member Is Confirmed, and the People It Concerns Are Told

**No new entity, no new column, no migration.** `NotificationType` is stored as an integer and the
two new members are appended; preferences are sparse; the rate limiter's counters are Redis keys
(in-memory in Development and tests).

## NotificationType (appended)

| Value | Name | Recipient | Actor | Category |
|-------|------|-----------|-------|----------|
| 12 | `TeamMemberRemoved` | the player an admin removed | **none** (never the admin) | `InvitesAndRoster` |
| 13 | `TeamMemberDeparted` | each current admin but the acting one | **the departing player** | `InvitesAndRoster` |

Appended, never inserted: the column holds integers, so renumbering would re-point stored rows.
`NotificationCategories.For` gets an explicit arm for each (its default arm files an unmapped type
under *Team news* silently — feature 039's warning).

## Payloads

```text
TeamMemberRemovedPayload  { teamSlug: string, teamName: string }
TeamMemberDepartedPayload { teamSlug: string, teamName: string, removed: bool }
```

- **No person in either payload.** The departing player is the `Departed` row's actor (037 FR-023);
  the removing admin appears nowhere (spec FR-011, FR-015).
- `removed` is a **bool**, never an enum (#370 — payload enums are stored as numbers).
- `teamSlug` is the key feature 061's rename rewrite matches on, so both follow a rename.

## Dedupe keys

| Type | Key | Written by |
|------|-----|------------|
| `TeamMemberRemoved` | `team-removed:{membershipId}` | `CreateAsync` |
| `TeamMemberDeparted` | `team-departure:{membershipId}:{recipientId}` | `CreateManyAsync` with prefix `team-departure:{membershipId}` |

`membershipId` is the `Id` of the `TeamMembership` row the departure deleted. A rejoin inserts a new
row with a new id, so a later departure is a new notice (FR-018).

## Existing entities touched (read only)

- **TeamMembership** — its `Id` is captured inside the removal transaction before the row is
  removed; `Role` selects the admins after commit.
- **User** — `Email`, `PreferredLanguage`, `Status` (banned admins are not told).
- **PlayerProfile** — `DisplayName` for the email and push, read through `_db.PlayerProfiles`.

## Rate-limit partition

`team-invite-accept:{userId}` — fixed window, 10 permits per clock hour, on
`POST /api/v1/invitations/{token}/accept`.

## State transitions

```text
TeamMembership (exists) ──leave──────▶ (deleted) ─▶ Departed{removed:false} → admins (not the leaver)
TeamMembership (exists) ──admin removes▶ (deleted) ─▶ Removed → the player
                                                  └▶ Departed{removed:true} → admins (not the actor)
TeamMembership (exists) ──team deleted / account erased / ban──▶ nothing sent (FR-019)
PartyMember (In|Declined) ──admin removes──▶ (deleted) ─▶ nothing sent (FR-009)
```
