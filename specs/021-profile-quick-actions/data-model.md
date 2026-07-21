# Phase 1 Data Model: Profile Quick-Actions

This feature adds **no persisted entities and no DTO changes**. It introduces only
client-side view-models used by the new component. Existing API shapes are consumed
as-is.

## Client view-models (component-local)

### `ViewerContext` (derived from `getMine()`, only when authenticated)

| Field | Source | Use |
|-------|--------|-----|
| `handle` | `OwnerProfile.handle` | Self-detection: hide actions when it equals the target handle |
| `adminTeams: { slug, name }[]` | `OwnerProfile.teams` filtered to `role === 'Admin'` | Basis for the Invite action + eligibility resolution |

Anonymous viewers have no `ViewerContext`; the actions are not rendered.

### `InviteEligibility` (per administered team, from `searchUsers`)

| Field | Source | Use |
|-------|--------|-----|
| `slug`, `name` | admin team | Picker label / invite target |
| `relation` | `InvitableUser.relation` for the target handle | `Invitable` = eligible; `Member`/`Invited` = excluded |
| `userId` | `InvitableUser.userId` | Passed to `createTargetedInvite(slug, userId)` |

Derived state: `eligibleTeams = teams where relation === 'Invitable'`.
- `adminTeams.length === 0` → Invite hidden.
- `adminTeams.length ≥ 1 && eligibleTeams.length === 0` → Invite disabled + reason.
- `eligibleTeams.length === 1` → invite directly.
- `eligibleTeams.length > 1` → picker.

### `MessageTarget` (from `chat.search`, resolved on click)

| Field | Source | Use |
|-------|--------|-----|
| `userId` | `PersonHit.userId` (exact handle match) | `chat.start([userId], null)` when no existing DM |
| `existingConversationId` | `PersonHit.existingConversationId` | Navigate straight to `/chat/:id` when set |

Unresolved (no exact match, or blocked → excluded from results) → friendly failure,
no conversation opened.

## Consumed API shapes (unchanged)

- `PublicProfileDto` (target profile) — read for `handle`/`displayName`; **no account id** (unchanged).
- `OwnerProfileDto` (viewer) — `handle` + `teams[].role`.
- `PersonHit` (chat search) — `userId`, `handle`, `existingConversationId`.
- `InvitableUser` (team user search) — `userId`, `handle`, `relation`.
- `Conversation` (chat start) — `id` for navigation.
- `TeamInvitation` (targeted invite result) — drives the "sent" confirmation.

No migrations, no schema, no contract changes.
