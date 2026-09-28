# Contract: The two new notification kinds

**Feature**: 058 · Existing endpoints only: `GET /notifications`, the unread count, mark-read, and
the notifications SignalR hub. No route changes.

---

## `NotificationDto` (existing shape, widened meaning)

```jsonc
{
  "id": "…",
  "type": "TeamJoinRequest" | "TeamJoinRequestAnswered" | …,
  "createdDate": "…",
  "isRead": false,
  "actorDisplayName": "Jonas Weber" | null,  // TeamJoinRequest: the player's CURRENT name; null once banned/erased
  "resolved": false,                          // TeamJoinRequest: true once the request no longer waits (was: TeamInvite only)
  "payload": { … }
}
```

### `TeamJoinRequest` — to each admin

```jsonc
"payload": { "requestId": "0199…", "teamSlug": "hamburg-hammers", "teamName": "Hamburg Hammers" }
```

- **No name or handle in the payload** (research R1). The client renders
  `actorDisplayName ?? <translated "a former player">`.
- Link-only, opens `/t/{teamSlug}`. No inline actions.
- `resolved: true` ⇒ the row reads as no longer waiting.
- Removed from every inbox when the request is withdrawn or ended by the player joining another way.

### `TeamJoinRequestAnswered` — to the player

```jsonc
"payload": { "teamSlug": "hamburg-hammers", "teamName": "Hamburg Hammers", "accepted": true }
```

- `accepted` is a **boolean**, never an enum (research R8).
- `actorDisplayName` is always `null` — the answer never identifies an admin (FR-011).
- Opens `/t/{teamSlug}` when accepted, `/browse/teams` when declined.

Both kinds belong to the **Invites & roster** preference category; each channel (in-app, email,
push) is decided independently per recipient (feature 055).

---

## Push (`PushContent`)

| Type | Title | Body (en) | URL |
|------|-------|-----------|-----|
| `TeamJoinRequest` | team name | "{player} wants to join the team" — or "Someone wants to join the team" when the name cannot be resolved | `/t/{slug}` |
| `TeamJoinRequestAnswered` (accepted) | team name | "Your request to join was accepted" | `/t/{slug}` |
| `TeamJoinRequestAnswered` (declined) | team name | "Your request to join was declined" | `/browse/teams` |

German and Spanish in `PushLocalizer`, in the **recipient's** language. The collapse tag is the
dedupe key (prefix): `join-request:{requestId}` / `join-answer:{requestId}`. Neither body ever falls
back to the generic "You have a new notification".

---

## Email

| When | To | Subject (en) | Button |
|------|----|--------------|--------|
| a request is created | each admin with *Invites & roster → Email* on | "{player} wants to join {team} — JuggerHub" | Open the team page → `/t/{slug}` |
| a request is approved | the player, if on | "You're in: {team} — JuggerHub" | Go to your team → `/t/{slug}` |
| a request is declined | the player, if on | "Your request to join {team} — JuggerHub" | Browse teams → `/browse/teams` |

Each in the recipient's stored language (en/de/es), with the shared header, footer and
notification-settings link. A failed send is logged and never fails the request or the answer.
Final wording is set in the catalogues during implementation, under DESIGN.md's voice rules
(German `–`, never `—`).
