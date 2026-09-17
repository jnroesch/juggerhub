# Phase 0 Research: Team Creation Wizard

Everything below was settled by reading the code, not by assumption. Each finding names the file it
came from so it can be re-checked rather than trusted.

## R1 — A team has no description, so none can be "moved"

**Finding**: `Team` (backend/Entities/Team.cs) carries `Slug`, `Name`, `Type`, `CityId`,
`BeginnersWelcome`, `Logo`, and three collections. There is no description column. `grep -i
description` over `backend/Dtos/Teams/` returns nothing, over the Teams migrations returns nothing,
and `team.models.ts` has no such field on `TeamDetail`, `TeamPublic` or `TeamPublicDetail`. Team
settings has no such input.

**Consequence**: the request's "move the description to that screen" describes an operation that
cannot be performed — there is no source to move from. Adding one is additive work: a column, a
migration, a DTO field, an editing surface and a display surface.

**Decision**: out of scope, by owner decision. Filed as **GH #321**. The wizard collects nothing a
team does not already hold (FR-027).

## R2 — Both optional steps require a team that already exists

**Finding**: the logo endpoint is `PUT /teams/{slug}/logo` and the invite endpoints are
`/teams/{slug}/invitations/*` (backend/Controllers/TeamsController.cs). Both are addressed by slug
and both are admin-gated — `CreateTargetedInvite` returns `Forbidden("Only admins can invite
people.")` for anyone else, and `SearchUsersAsync` is behind the same check.

**Consequence**: neither can run before `POST /teams` has returned a slug. The only two shapes
available are (a) create at the review step and let the optional steps act on the real team, or
(b) collect intentions and replay them all at the end.

**Decision**: (a), by owner decision. Beyond it being the only shape in which the invite search can
report a *true* `Invitable`/`Invited`/`Member` relation, (b) would need a different user-search
surface that does not exist, and would make the final press a compound action whose partial failure
("team created, three of five invitations sent, logo rejected") has no good recovery.

**Rejected**: (b). Recorded so the question is not reopened without the reason.

## R3 — A taken address is a 409, and that is the only safe discriminator

**Finding**: `TeamsController.Create` maps `CreateTeamStatus.SlugTaken` to **409 Conflict** and
every other failure to 400. The `detail` string is English-only server prose.

**Supporting finding**: the availability check deliberately ships a machine-readable `reason` code,
which `team-create.component.ts` turns into a catalogue key —
`` `teams.create.slugReason.${reason[0].toLowerCase()}${reason.slice(1)}` `` — with the in-code
justification *"The server sends a code rather than a sentence because its own prose is
English-only."*

**Decision**: FR-009's recovery branches on `err.status === 409` and nothing else. Matching on the
message would be a localisation defect that fails silently for exactly the players the rest of the
codebase went out of its way to protect.

## R4 — 051 applies a logo immediately; there is no preview anywhere

**Finding**: `team-settings.component.ts` `onLogoSelected()` calls `teams.uploadLogo(...)` directly
from the file `change` handler. There is no intermediate preview state. The applied result appears
because `TeamService.logoUrl(slug)` appends a per-slug revision counter that `bumpLogoRevision()`
increments on upload — the fix for GH #283, since a logo's URL does not change when its image does.

**Contrast**: onboarding *does* hold the avatar `File` in a signal with an `avatarPreview` and
uploads at `finish()`. That is correct there because no profile avatar resource exists to write to
until the flow completes.

**Decision**: the wizard follows 051, not onboarding, because by the logo step the team exists.
FR-016 was amended in the spec accordingly rather than left as drift. The revision-counter
mechanism is reused, not reimplemented — without it the newly uploaded logo would not render.

## R5 — The invite search is ~30 lines of template carrying one behaviour rule

**Finding**: `team-invitations.component.html` lines 47-78 hold the search input, a searching line,
and a result list whose `@switch (u.relation)` decides between a static "member" label, a static
"invited" chip, and an actual invite button. `invite()` refuses non-`Invitable` users and
optimistically flips the row to `Invited` on success.

**Also found**: there is **no empty state** — a search matching nobody renders an empty `<ul>`.

**Decision**: extract to `features/teams/components/invite-search/` and use it from both screens
rather than copying it. The relation switch is a rule about whether an invite may be offered, and
two copies of such a rule is the duplication class 046 recorded as having already drifted once in
this codebase. The extraction adds the missing empty state (FR-025), which the existing screen
inherits — called out in the plan so it is not read as an unrelated regression.

**Rejected**: putting it in `shared/`. It takes a team slug and reports a relation to that team; it
is team-domain, not an app-wide primitive. `features/profile/components/` is the precedent for a
feature-local components directory.

## R6 — The wizard shape is already settled by two existing flows

**Finding**: `EventCreateComponent` uses a `STEPS` constant, a `step` signal and a `stepIndex`
computed, rendering round knobs with
`i === stepIndex() ? 'w-6 bg-brand' : i < stepIndex() ? 'w-2 bg-brand/60' : 'w-2 bg-surface-sunken'`
and one `@if` per step in a single template. `OnboardingComponent` uses the same idea with a
`CORE_STEPS`/`FLOW` split so welcome/done sit outside the progress.

**Decision**: copy the event wizard's shape. This flow has no welcome or done screen, so it needs
no `FLOW`/`CORE_STEPS` split — all five steps carry a knob.

**Note**: the app is **zoneless**. Anything the template reads for an enable/disable decision must
be a signal or a computed, not a plain property. This is the hazard 045 documented at length when
it had to convert ten plain fields in the training wizard.

## R7 — What the availability pipeline already gets right

**Finding**: `team-create.component.ts`'s slug pipeline is
`distinctUntilChanged()` → `tap(clear verdict)` → `debounceTime(300)` → `switchMap(check, catchError **inside**)`,
and carries three comments explaining the ordering: the verdict is cleared on the keystroke rather
than after the debounce (or submit rides a verdict about a handle nobody is asking for);
`distinctUntilChanged` sits *ahead* of the debounce so typing and deleting a character still
re-checks; and the error is caught inside the `switchMap` so a failure cannot tear down the
subscription and leave every later keystroke unchecked.

**Decision**: this moves to the new component **unchanged, comments included**. It is three
separate bug fixes encoded as operator order, and re-deriving it while restructuring the component
is the most likely way to silently undo them.

## R8 — Principle VII has nothing to add and one thing to forbid

**Finding**: no outbound integration is added, and no new kind of browser→backend call. All four
calls the wizard makes are already made from other screens.

**Decision**: no `AddJuggerHubResilience`, no retry, no backoff, no breaker. Consistent with
044/045/046/047/048, all of which recorded the same finding.

**The prohibition that does apply**: create, upload and invite are all mutations, and Principle VII
forbids automatically retrying a mutation on the browser hop — a `POST /teams` retried after a
timeout would create a second team. The player-visible retry FR-010 and FR-018 require is a *press*,
not an automatic one.
