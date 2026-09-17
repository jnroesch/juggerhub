# Phase 1 Data Model: Team Creation Wizard

## Stored data: none

This feature adds **no entity, no column, no migration and no DTO field**. It writes only what the
product already writes, through calls other screens already make:

| Written | Entity | Written by today | Written by the wizard |
|---|---|---|---|
| The team | `Team` | the create form | the review step — same payload |
| The logo | `TeamLogo` | team settings | the logo step — same call |
| An invitation | `TeamInvitation` | team invitations | the invite step — same call |

`CreateTeamRequest(Name, Slug, Type, Location)` is unchanged. FR-027 requires a team created by the
wizard to be indistinguishable from one created by the form it replaces, and sending the identical
payload is what makes that structural rather than a thing to remember.

## Client-side state: the step machine

The only new "model" is the component's own state, and it is worth writing down because one field
in it governs the whole second half of the flow.

```ts
type Step = 'basics' | 'type' | 'review' | 'logo' | 'invite';
const STEPS: readonly Step[] = ['basics', 'type', 'review', 'logo', 'invite'];
```

| State | Shape | Notes |
|---|---|---|
| `step` | `signal<Step>` | starts at `'basics'` |
| `createdSlug` | `signal<string \| null>` | **the latch** — see below |
| `form` | `FormGroup` | `name`, `slug` — as today |
| `type` | `signal<TeamType>` | `'CityTeam'` default, as today |
| `selectedCity` | `signal<CityOption \| null>` | as today |
| `slugStatus` / `checkingSlug` / `slugCheckFailed` | signals | as today, pipeline unchanged (R7) |
| `submitting` / `error` | signals | as today |
| `uploadingLogo` / `hasLogo` | signals | logo step only |

### `createdSlug` is a latch, not a step marker

Set exactly once, by a successful `POST /teams`. Never cleared. Three things derive from it, and
all three are the spec's requirements rather than conveniences:

- **FR-011** — once set, the Back affordance is *not rendered*. The team handle is `init`-only on
  the entity and the other answers are settings now, so there is nothing behind Back to return to.
- **The optional steps cannot be entered without it.** They address the team by slug; a step that
  could be reached with `createdSlug === null` would have nothing to call.
- **FR-014** — both optional steps finish by navigating to `/t/{createdSlug()}`.

Deriving the post-create half from the *existence of the team* rather than from `stepIndex >= 3`
is what makes "the team is real from here on" a single fact rather than a convention three places
have to agree about.

### What is deliberately not in the state

- **No draft.** Nothing is written to `sessionStorage` (D6 — recorded scope decision).
- **No held `File`.** The logo uploads on selection (R4); a `File` kept in memory would be the
  onboarding model, which is wrong here because the team exists.
- **No list of pending invitations.** The invite step's extracted component tracks the
  `Invitable → Invited` flip on its own rows and nothing else. The team's real pending list lives
  on the team's invitations screen, and re-deriving it here would be a second source of truth for
  something the wizard never displays.
- **No `submitting`/`error` persisted anywhere** — 045's rule, and it has no storage here anyway.

## Validation

All validation is the server's; the wizard's is for UX only (Principle I). Client-side rules are
unchanged from the current form:

| Field | Rule | Enforced where |
|---|---|---|
| name | required, 2-50 | server `[Required, MinLength(2), MaxLength(50)]`, mirrored in the form |
| slug | required, lowercase pattern, ≤ 30 | server `[Required, MaxLength(30)]` + uniqueness, mirrored in the form |
| slug availability | must have a *positive, completed* check | advisory only — the server is the uniqueness boundary and answers 409 (R3) |
| city | required iff `type === 'CityTeam'` | server rejects a `CityTeam` without a location |

The one gate that is new is a step gate, not a validation rule: **step 2 cannot be left without a
city when the type is a city team** (FR-005). That is the same condition today's `canSubmit`
already carries, moved from one submit button to one step boundary.
