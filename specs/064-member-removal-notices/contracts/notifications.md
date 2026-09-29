# Contract: The two new notification kinds

**Feature**: 064 · Existing endpoints only: `GET /notifications`, the unread count, mark-read and the
notifications SignalR hub. No route changes.

## `TeamMemberRemoved` — to the player an admin removed

```jsonc
{
  "type": "TeamMemberRemoved",
  "actorDisplayName": null,            // ALWAYS null: the row has no actor (spec FR-011)
  "resolved": false,
  "payload": { "teamSlug": "rheinfeuer", "teamName": "Rheinfeuer" }
}
```

- Link-only, opens `/t/{teamSlug}`. No inline actions.
- Title (en): "You're no longer a member of {team}". No supporting line.

## `TeamMemberDeparted` — to each current admin but the one who acted

```jsonc
{
  "type": "TeamMemberDeparted",
  "actorDisplayName": "Jonas Weber" | null,   // the departing player's CURRENT name; null once banned/erased
  "resolved": false,
  "payload": { "teamSlug": "rheinfeuer", "teamName": "Rheinfeuer", "removed": true }
}
```

- **No name or handle in the payload.** The client renders
  `actorDisplayName ?? <translated placeholder>` (the same placeholder 058's join-request row uses).
- `removed` is a **boolean**: `false` = left on their own, `true` = an admin removed them. Which admin
  is never stated.
- Link-only, opens `/t/{teamSlug}` (the roster).
- Titles (en): "{player} left {team}" / "{player} was removed from {team}".

Both kinds belong to **Invites & roster**; in-app, email and push are decided independently per
recipient (feature 055). Both follow a team rename (feature 061 — matched by `teamSlug`).

## Push (`PushContent`)

| Type | Title | Body (en) | URL |
|------|-------|-----------|-----|
| `TeamMemberRemoved` | team name | "You're no longer a member of this team" | `/t/{slug}` |
| `TeamMemberDeparted` (removed=false) | team name | "{player} left the team" — "A player left the team" without a name | `/t/{slug}` |
| `TeamMemberDeparted` (removed=true) | team name | "{player} was removed from the team" — "A player was removed from the team" without a name | `/t/{slug}` |

German and Spanish in `PushLocalizer`, in the **recipient's** language. Collapse tags are the dedupe
keys: `team-removed:{membershipId}` / `team-departure:{membershipId}`. No body falls back to the
generic sentence.

## Email

| Template | Subject (en) | To |
|----------|--------------|----|
| `removed-from-team` | "You're no longer a member of {team} — JuggerHub" | removed player |
| `member-left` | "{player} left {team} — JuggerHub" | admins |
| `member-removed` | "{player} was removed from {team} — JuggerHub" | admins |

Each in en/de/es, in the recipient's saved language, button to the team page. Sent only if the
recipient's *Invites & roster → Email* is on. No email names the removing admin.
