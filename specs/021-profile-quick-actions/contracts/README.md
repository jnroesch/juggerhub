# Interface Contracts: Profile Quick-Actions

**No new API contracts.** This feature is frontend-only and consumes existing,
already-shipped endpoints unchanged. It adds nothing to any request/response shape.

## Endpoints consumed (read/write, all existing)

| Endpoint | Verb | Auth | Used for |
|----------|------|------|----------|
| `/api/v1/chat/search?q={handle}` | GET | Authenticated | Resolve `handle → userId` + `existingConversationId` for **Message** (block-aware; excludes blocked users) |
| `/api/v1/chat/conversations` | POST `{participantUserIds:[userId], name:null}` | Authenticated | Start a DM when none exists; returns the conversation to navigate to |
| `/api/v1/teams/{slug}/members/search?q={handle}` (via `TeamService.searchUsers`) | GET | Team admin (403/404 otherwise) | Resolve the target's `userId` + `relation` per administered team (eligibility) |
| `/api/v1/teams/{slug}/invitations` (via `TeamService.createTargetedInvite`) | POST `{userId}` | Team admin | Send the targeted team invitation |
| `/api/v1/profiles/me` (via `ProfileService.getMine`) | GET | Authenticated | Viewer handle (self-detection) + administered teams |
| `/api/v1/profiles/{handle}` (public profile) | GET | Anonymous | The profile being viewed — **unchanged; still carries no account id** |

## Authorization notes (server is the boundary — FR-010)

- Messaging honors block and rate-limit rules server-side; the client only presents
  the outcome.
- `searchUsers` / `createTargetedInvite` return **403/404** for a caller who is not an
  admin of the team; the client only ever calls them for the viewer's own admin
  teams, so the UX gate and the server rule agree.
- The public profile endpoint is untouched: this feature adds **no** field to it and
  exposes **no** account identifier (FR-009 / specs/003 privacy invariant).

*(Exact team search/invite route strings mirror `TeamService`; the frontend calls the
service methods, not the URLs directly.)*
