# Contract: Home *Needs you* (breaking — front and back ship together)

**Feature**: 058 · `GET /api/v1/home` → `HomeDto.needsYou` (the route and `HomeDto` are unchanged)

## Item shape

```jsonc
// BEFORE (removed): "title" and "context" were English sentences built on the server.
{ "kind": "TeamInvite", "id": "tok-…", "title": "Hamburg Hammers invited you",
  "context": "to join the team", "linkTarget": "hamburg-hammers", "occurredAt": "…" }

// AFTER: names only; the client composes the words in the viewer's language.
{ "kind": "TeamInvite", "id": "tok-…",
  "params": { "teamName": "Hamburg Hammers", "teamSlug": null, "eventName": null, "playerName": null },
  "linkTarget": "hamburg-hammers", "occurredAt": "…" }
```

## Per kind

| `kind` | `id` (action key) | `linkTarget` | `params` | Actions (client → existing endpoint) |
|--------|-------------------|--------------|----------|---------------------------------------|
| `TeamInvite` | invitation token | team slug | teamName | accept / decline → `/invitations/{token}/…` |
| `PartyCoAdminInvite` | invitation token | event id | eventName, teamName | accept / decline → party invitation endpoints |
| `PartyRequest` | party id | event id | teamName, eventName | I'm in / Can't → `/parties/{id}/join` · `/decline` |
| `MarketInvite` | market request id | event id | teamName, eventName | accept / decline → market endpoints |
| `MarketApplication` | market request id | event id | teamName, eventName | none (shown pending) |
| **`JoinRequest`** (NEW) | join request id | **player handle** (profile link) | **playerName, teamName, teamSlug** | Approve / Decline → `/teams/{teamSlug}/join-requests/{id}/approve` · `/decline` |

- Only viewers who are **currently admins** of the team receive `JoinRequest` items, one per
  **waiting** request (research R3), across every team they administer.
- A `JoinRequest` answer that returns `404` means *no longer waiting*: the client hides the item and
  shows a notice in the card; it does not retry.
- Ordering and the cap are unchanged: every kind merged newest-first by `occurredAt`, capped at
  `HomeOptions.NeedsYouCap`.
- `params` values are user data (names) and are rendered as text, never as markup.
