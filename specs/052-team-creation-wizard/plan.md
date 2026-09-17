# Implementation Plan: Team Creation Wizard

**Branch**: `052-team-creation-wizard` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/052-team-creation-wizard/spec.md`

## Summary

Reshape the one-screen team create form into a five-step wizard in the house style, and put two
capabilities that already exist — the team logo (051) and the targeted invite — inside it as
skippable steps.

**Frontend only. No entity, no migration, no new endpoint, no new DTO, no new dependency.** If a
task in this feature produces an `Add-Migration`, or touches `backend/`, something is wrong. Every
call the wizard makes is one the product already makes from another screen:

| Step | Call | Already used by |
|---|---|---|
| basics | `GET /teams/slug-available` | today's create form |
| review | `POST /teams` | today's create form |
| logo | `PUT /teams/{slug}/logo` | team settings (051) |
| invite | `GET /teams/{slug}/invitations/user-search`, `POST /teams/{slug}/invitations` | team invitations |

The whole feature is one component rewritten, one small component extracted, one route untouched,
and copy in three catalogues.

## Technical Context

**Language/Version**: TypeScript 5.x, Angular (standalone, **zoneless**, signals)

**Primary Dependencies**: none added. `@jsverse/transloco` for copy, existing `jh-city-picker`,
existing `TeamService`.

**Storage**: none. Nothing is persisted client-side — see [D6](#d6-no-draft-persistence).

**Testing**: Jest component specs (`*.spec.ts`), alongside the existing
`team-create.component.spec.ts` (186 lines) which is rewritten against the new flow.

**Target Platform**: browser, mobile-first. 375px is the binding width.

**Project Type**: web application (`frontend/apps/web`), backend untouched.

**Performance Goals**: unchanged. The wizard makes strictly fewer calls per screen than the form it
replaces (the availability check now runs on a step that shows nothing else).

**Constraints**: the existing 300ms debounce on the address check and the existing hold-progress-
while-checking rule are preserved exactly (FR-006). The logo and invite steps must not be reachable
before the team exists.

**Scale/Scope**: one wizard, five steps, ~20 new copy keys × 3 languages, two components touched
plus one extracted.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1 design.*

| Gate | Engaged? | Finding |
|---|---|---|
| 1. Architecture (thin controllers, DI'd services, EF projections) | **No** | No backend change. |
| 2. Data access (pagination, projections, `ModifiedDate`, `BaseEntity`) | **No** | No backend change, no new entity, no `ExecuteUpdateAsync`. The user search the invite step consumes already paginates. |
| 3. Security review (OWASP, never trust the client) | **Yes, satisfied** | Every gate the wizard appears to apply is applied server-side and is not moved: the logo and invite endpoints are admin-gated on the server, team creation authorises server-side, and the address uniqueness check is advisory in both the old form and the new one — the server is still the boundary (see [D5](#d5-the-409-is-the-discriminator)). The wizard adds **no** client-side authorisation decision. |
| 4. Auth (httpOnly cookies, backend password policy) | **No** | Untouched. |
| 5. Conventions (`.html`/`.css`/`.ts` separate; only `.ps1` scripts) | **Yes, satisfied** | The wizard keeps three separate files, as the component does today. No inline template, no script added. |
| 6. Environment parity | **No** | No configuration, no secrets, no environment-dependent behaviour. |
| 7. **UI/Design compliance** | **YES — ENGAGED** | The feature is almost entirely new markup and new copy. `checklists/ui-review.md` is instantiated from the template and verified against the diff before verification. See [Gate 7 risk](#gate-7-the-binding-risk). |
| 8. **Resilience (Principle VII)** | **Engaged as a prohibition** | See below. |

### Principle VII — engaged only to forbid something

This feature adds **no outbound integration and no new kind of browser→backend call**. There is
nothing here to wrap in `AddJuggerHubResilience`; reaching for retry, backoff or a circuit breaker
in this diff is review-rejectable, exactly as it was in 044/045/046/047/048.

What Principle VII *does* say about this diff is a prohibition, and it is load-bearing:

> **Never retry a mutation on the browser hop.**

All three of the wizard's writes — create team, upload logo, send invitation — are mutations. None
may be retried automatically, on a timeout or on anything else. FR-010 and FR-018 give the player a
way to *press again*; that is a user-initiated retry of a request they can see failed, which is the
opposite of the automatic retry the principle forbids. **A duplicate `POST /teams` would create a
second team**, which is precisely the harm the rule exists to prevent.

The existing shared HTTP layer already bounds these calls in time. Nothing per-call-site is added.

**Post-Phase-1 re-check**: no gate moved. Design introduces no backend surface, no network call
that did not already exist, and no client-side security decision.

## Project Structure

### Documentation (this feature)

```text
specs/052-team-creation-wizard/
├── plan.md              # This file
├── research.md          # Phase 0 — the decisions and what was rejected
├── data-model.md        # Phase 1 — no entities; the client-side step machine instead
├── quickstart.md        # Phase 1 — how to verify the feature by hand
├── contracts/
│   └── wizard-flow.md   # Phase 1 — the step machine + the endpoints consumed (none new)
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # Gate 7 (instantiated at implementation time)
└── tasks.md             # Phase 2 — /speckit-tasks
```

### Source Code (repository root)

```text
frontend/apps/web/
├── public/i18n/
│   ├── en.json                      # + teams.create.* wizard keys
│   ├── de.json                      # same keys — all three at once, or parity goes red
│   └── es.json                      # same keys
└── src/app/features/teams/
    ├── team-create/
    │   ├── team-create.component.ts      # REWRITTEN — five-step machine
    │   ├── team-create.component.html    # REWRITTEN — progress + five step blocks
    │   ├── team-create.component.css     # unchanged (empty-ish, as today)
    │   └── team-create.component.spec.ts # REWRITTEN against the new flow
    ├── components/
    │   └── invite-search/                # NEW — extracted, two call sites
    │       ├── invite-search.component.ts
    │       ├── invite-search.component.html
    │       └── invite-search.component.spec.ts
    └── team-invitations/
        ├── team-invitations.component.ts   # search state moves out to the extracted component
        └── team-invitations.component.html # inline search block replaced by <jh-invite-search>
```

**Structure Decision**: the existing `features/teams/` layout is kept as-is. The extracted search
goes to `features/teams/components/`, **not** `shared/` — it is team-domain (it takes a team slug
and reports a relation to *that team*), and `shared/` holds app-wide primitives
(`city-picker`, `address-fields`, `ui`). The precedent for a feature-local `components/` directory
is `features/profile/components/`.

The route is untouched: `teams/new` stays one route with one component behind `authGuard`. Steps
are **not** routed — 045 recorded the owner's rejection of step-in-route for the other two wizards,
and a different answer here would make this the odd one out.

## Design Decisions

### D1. One component, five steps, one template

Mirror `EventCreateComponent` exactly: a `STEPS` constant, a `step` signal, a `stepIndex` computed,
and one `@if` block per step in a single template, with the round-knob progress rendered from
`steps`/`stepIndex`. The knob markup is lifted from the event wizard verbatim so the two flows
cannot drift apart visually.

```ts
type Step = 'basics' | 'type' | 'review' | 'logo' | 'invite';
const STEPS: readonly Step[] = ['basics', 'type', 'review', 'logo', 'invite'];
```

All five carry a knob. The optional steps are shown in the progress from the first screen, because
a step that appears only once you reach it reads as the flow having grown rather than as one you
were always going to be offered.

### D2. The create point is a latch, not a step boundary

`createdSlug = signal<string | null>(null)`, set once, never cleared. It is the single fact that the
whole post-create half of the wizard is derived from:

- the Back action is **not rendered at all** once it is set (FR-011) — not disabled, not hidden by
  a step comparison, but absent, because the answers behind it are no longer editable here;
- the logo and invite steps read the slug from it, so neither can be entered before the team exists;
- the "finish" action on both optional steps navigates to `/t/{createdSlug()}`.

The team handle is `init`-only on the entity — immutable once created. The latch is what makes the
irreversibility visible in the UI instead of being a surprise.

### D3. The logo step applies immediately, matching 051

Team settings — the only place a logo can be set today — uploads on file selection and renders the
applied result via `TeamService.logoUrl(slug)`, whose per-slug revision counter (051, the lesson of
GH #283) is what makes the new image actually appear at an unchanged URL. The wizard does the same
and reuses that exact mechanism.

This **amends FR-016**, which originally asked for a preview before applying. The amendment and its
reasoning are recorded in the spec itself rather than left as drift. Rejected alternative: hold the
`File` in memory like onboarding's avatar and upload at the end — that is right for onboarding,
where nothing exists yet to upload *to*, and wrong here, where the team already exists and a second
interaction model for one act would be the inconsistency this feature is meant to remove.

### D4. Extract `jh-invite-search`, do not copy it

The invite step needs the search box, the debounce, the result rows and — critically — the
three-way `Member` / `Invited` / `Invitable` switch that decides whether a row offers an invite at
all. That switch is behaviour, not decoration, and a second copy of it is the kind of duplication
046 recorded as having already drifted once elsewhere in this codebase.

So the block is **lifted out of `team-invitations.component.html`** into
`features/teams/components/invite-search/`, and both screens render `<jh-invite-search>`:

| | input | output | what the parent does with it |
|---|---|---|---|
| team invitations | `slug` | `invited` | reloads its pending list and link |
| create wizard | `slug` | `invited` | nothing — it has no pending list |

The component owns the search control, the 300ms debounce, the results signal, the optimistic
`Invitable → Invited` flip on success, and its own error line. The parents own everything else.

Two consequences, both deliberate:

1. **`team-invitations` changes behaviour slightly.** Today its result list has no empty state — a
   search matching nobody renders an empty `<ul>`. FR-025 requires one, and the extracted component
   is one component, so the existing screen gains the empty state too. This is an improvement, it
   is in DESIGN.md's voice for empty states, and it is called out here so it is not read as an
   accidental regression in an unrelated screen.
2. **The extraction is verbatim otherwise.** Markup, tokens, `data-testid`s (`user-search`,
   `invite-<handle>`) and copy keys (`teams.invitations.*`) all move unchanged, so the existing
   `team-invitations` tests keep passing and the diff on that screen stays readable.

### D5. The 409 is the discriminator

FR-009 — a handle that was available when checked but taken by the time Create is pressed — needs
the wizard to tell "your address is gone" apart from "something else went wrong", because the two
have different recoveries (back to step 1, versus stay on review and retry).

The server already makes this structural: `TeamsController.Create` returns **`409 Conflict`** for
`CreateTeamStatus.SlugTaken` and `400` for everything else. So the branch is on `err.status === 409`
and nothing else.

**Never branch on the message.** The server's `detail` prose is English-only — that is the
documented reason the availability check ships a machine-readable `reason` code that the client
turns into `teams.create.slugReason.*` rather than rendering the server's sentence. Matching on
prose would break the moment a German-speaking player hit it, and would break silently.

On a 409 the wizard sets the step back to `basics`, surfaces the reason on the address field using
the existing `slugStatus` display path (so the message a taken handle produces is the same one the
live check produces), and clears the stale positive verdict so Continue is held until a fresh check
comes back. Every other answer is untouched.

### D6. No draft persistence

The training and event wizards persist unfinished answers to `sessionStorage` (045). This one does
not, by explicit scope decision recorded in the spec's Out of Scope section: the exposure is two
short steps rather than 045's sixteen and twenty-one answers, and a third draft shape is its own
change with its own version discipline.

Recorded as the one place this wizard is deliberately unlike the other two. Worth a follow-up once
the flow exists — noted rather than silently skipped.

### D7. What is *not* being touched

- `CreateTeamRequest` and everything it carries. FR-027: the team created by the wizard must be
  byte-identical to one created by today's form, and the payload is how that is guaranteed.
- The type toggle, the city picker binding, the `jhLowercase` folding on the address input, the
  availability pipeline (`distinctUntilChanged` → `tap(clear)` → `debounceTime(300)` →
  `switchMap` with the error caught **inside**). That pipeline carries three separate comments
  explaining why each operator sits where it does; it moves from the old component to the new one
  **unchanged**, comments included.
- Team settings' own logo controls, and the invite link / pending / revoke halves of team
  invitations.

## Gate 7: the binding risk

The UI review checklist is instantiated at implementation time from
`.specify/templates/ui-review-checklist-template.md` into `checklists/ui-review.md`. DESIGN.md wins
on any conflict.

The three things most likely to fail it:

1. **Five knobs at 375px.** The event wizard renders six and fits, so the geometry is proven — but
   the review step's label/value list is new markup and German is the long language. The binding
   case to check is the review step in German at 375px.
2. **The review step is a new pattern.** Neither existing wizard has a review that lists answers
   with a route back to each. It must read as a summary, not as a form, and its "change this"
   affordances must not look like inputs.
3. **The two optional steps need a skip that reads as a real choice**, not as a dismissal. DESIGN.md
   §"Encouraging & low-pressure" governs: a skip is an offer declined, and the copy must not imply
   the team is incomplete without it (FR-013 says it is not).

## Complexity Tracking

No constitution violations. One recorded deviation from a project convention, with its reasoning:

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| This wizard persists no draft, unlike the other two (D6) | Scope, by owner decision | Adding a third draft shape is its own change with its own version discipline; the exposure here is two short steps, not twenty-one answers |
| An existing screen (`team-invitations`) is modified although the request did not ask for it (D4) | Extracting the search is what stops the relation switch existing twice | Copying the block leaves two copies of a behaviour rule that decides whether an invite may be sent at all — the duplication class 046 recorded as having already drifted once |
