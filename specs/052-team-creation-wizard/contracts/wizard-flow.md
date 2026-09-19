# Contract: Team Creation Wizard

## No API contract changes

**This feature introduces no endpoint, changes no request or response shape, and adds no field to
any DTO.** Every call below already exists and is already made from another screen. This document
exists to record *which* existing contracts the wizard depends on, so a future change to one of them
knows this flow is a consumer.

| # | Call | Method | Used on step | Also used by |
|---|---|---|---|---|
| 1 | `/api/v1/teams/slug-available?slug=` | GET | basics | today's create form |
| 2 | `/api/v1/teams` | POST | review | today's create form |
| 3 | `/api/v1/teams/{slug}/logo` | PUT | logo | team settings (051) |
| 4 | `/api/v1/teams/{slug}/invitations/user-search?q=` | GET | invite | team invitations |
| 5 | `/api/v1/teams/{slug}/invitations` | POST | invite | team invitations |

Frontend access is through the existing `TeamService` methods — `checkSlug`, `createTeam`,
`uploadLogo`, `logoUrl`, `searchUsers`, `createTargetedInvite` — none of which change.

## Responses the wizard must handle

Only #2 needs behaviour that is not already implemented somewhere else, and only because the wizard
has a step to send the player back to:

| Call | Status | Meaning | Wizard behaviour |
|---|---|---|---|
| 2 | `201` | created | latch `createdSlug`, advance to `logo` |
| 2 | **`409`** | **team address taken** | **return to `basics`**, show the reason on the address field, clear the stale positive verdict so Continue is held until a fresh check lands. All other answers retained. (FR-009) |
| 2 | `400` | invalid | stay on `review`, show the problem detail, allow another press (FR-010) |
| 2 | other / network | failed | stay on `review`, show the problem detail, allow another press (FR-010) |
| 3 | `204` | logo set | bump the slug's logo revision, render the applied logo |
| 3 | any failure | refused | report on the step; team unchanged; retry or skip stays available (FR-018) |
| 5 | `201` / `200` | invited / already invited | flip that row to `Invited` |
| 5 | any failure | not sent | report on the step; other invitations unaffected (FR-026) |

**The 409 is the only discriminator.** `TeamsController.Create` maps `CreateTeamStatus.SlugTaken` to
409 and everything else to 400, so the branch is on `err.status` alone. The server's `detail` prose
is English-only and must never be matched on — the availability check ships a machine-readable
`reason` code precisely so the client can render its own localized sentence. See research R3.

## Mutations are never retried automatically

Calls 2, 3 and 5 are mutations on the browser hop. Principle VII forbids retrying them
automatically: a `POST /teams` replayed after a timeout creates a second team. The retries the spec
requires (FR-010, FR-018) are the player pressing again, on a failure they can see.

## The step machine

```
  basics ──► type ──► review ──┐
     ▲         ▲         ▲     │ POST /teams  (201)
     └─────────┴─────────┘     │
        back, answers kept     ▼
           ▲             createdSlug latched
           │                   │
           │ 409               ▼
           └──────────────  logo ──skip/continue──► invite ──skip/finish──► /t/{slug}
                              │                       │
                    PUT .../logo              POST .../invitations (xN)
```

- Left of the latch: free movement in both directions, every answer preserved (FR-004).
- The latch: crossed exactly once, by a `201`.
- Right of the latch: forward only. No Back is rendered (FR-011). Each step is independently
  skippable (FR-012), and leaving at any point here leaves a complete team (FR-013).
- The 409 edge is the one arrow that crosses the latch backwards, and it exists precisely because
  the team was **not** created.

## Extracted component contract

`<jh-invite-search>` — `features/teams/components/invite-search/`. Internal to the frontend; listed
here because it is a contract two screens now depend on.

| | |
|---|---|
| **Input** `slug` | `string`, required — the team whose members and invitations the search is relative to |
| **Output** `invited` | emits after an invitation is successfully created |
| **Owns** | the search control and its 300ms debounce, the results, the `Member`/`Invited`/`Invitable` switch, the optimistic flip to `Invited`, the empty state, its own error line |
| **Does not own** | the pending-invitation list, the shared invite link, navigation |

Consumers: `team-invitations` (reloads its pending list and link on `invited`) and the wizard's
invite step (ignores it). Existing `data-testid`s `user-search` and `invite-<handle>` move with the
markup unchanged, so the existing team-invitations tests keep addressing the same elements.
