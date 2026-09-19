---

description: "Task list for feature 053 — Invite-Aware Onboarding"
---

# Tasks: Invite-Aware Onboarding

**Input**: Design documents from `specs/053-invite-aware-onboarding/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Included. The repo's convention is an integration test per backend behaviour and a
component spec per frontend surface, and this feature's guarantees are mostly negative ones — "a
malformed reference is dropped and registration still succeeds", "Continue sends nothing", "a
tampered link can only produce the plain step" — that only a test can hold in place.

**Organization**: Grouped by user story. US1 is the carrying (backend + auth-page chain); US2 the
wizard's invitation card; US3 the addressed-invitations list; US4 the stale/failed states. US2 and
US4 share the wizard files and are ordered, not parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: `[US1]`…`[US4]` — maps to the user stories in [spec.md](spec.md)

## Path Conventions

Web app: backend at `backend/`, frontend at `frontend/apps/web/src/app/`, e2e at
`frontend/apps/web-e2e/src/`. Paths below are relative to the repository root.

**Guardrails that apply to every task**:

- **No entity, no column, no migration, no new endpoint.** If a task produces an `Add-Migration`
  or a new controller action, something is wrong.
- The carried thing is **two validated segments** (`slug`, `token`), never a path or URL.
- `next()` / `back()` / Skip / "I'm not on a team yet" in the wizard are **not touched**.
- All new wizard state is **signals** (zoneless).
- **All three catalogues change together** (`catalog-parity.spec.ts`); German uses `–`, never `—`.
- **No retry / timeout / backoff code** anywhere; the interceptors already do the right thing.

---

## Phase 1: Setup

**Purpose**: Nothing to scaffold. Confirm the ground is where the plan says it is.

- [X] T001 Confirm the four break/carry points read as plan.md describes: `frontend/apps/web/src/app/features/auth/register/register.component.ts` L77-80 + L226-234 (returnUrl decorates links, never sent), `frontend/apps/web/src/app/features/auth/verify-email/verify-email.component.html` L13-19 (bare `/sign-in`), `backend/Services/Email/AuthEmailService.cs` L82-87 (`BuildLink` has no third parameter), and `frontend/apps/web/src/app/features/auth/sign-in/sign-in.component.ts` L58-63 (already carries `returnUrl` into `/onboarding` — must stay untouched)
- [X] T002 [P] Confirm the token/slug shapes the validation will encode: `backend/Services/Teams/TeamInvitationService.cs` `NewToken()` (32 bytes → base64url, 43 chars) and `backend/Services/Teams/TeamSlugPolicy.cs` `SlugRegex` + `Validate(min,max)` with `backend/Common/TeamOptions.cs` `SlugMinLength`/`SlugMaxLength`

---

## Phase 2: Foundational

**Purpose**: The one shared piece both the backend and the four frontend components need — the
reference's shape — defined once per side.

- [X] T003 Create `backend/Services/Teams/InviteReference.cs`: `public readonly record struct InviteReference(string Slug, string Token)` with `public static InviteReference? TryParse(string? slug, string? token, TeamOptions options)` — returns null unless BOTH are non-blank, `TeamSlugPolicy.Validate(TeamSlugPolicy.Normalize(slug), options.SlugMinLength, options.SlugMaxLength) == SlugRejection.None`, and the token matches a `[GeneratedRegex("^[A-Za-z0-9_-]{16,128}$")]`. Pure — no DB, no logging. XML doc must say why bounds not `{43}` (research R2) and why registration never reads the invitation (R3)
- [X] T004 [P] Create `frontend/apps/web/src/app/core/utils/invite-ref.ts` exporting `interface InviteRef { slug: string; token: string }`, `inviteRef(slug, token): InviteRef | null` (same two regexes as T003; slug length 3–30), `inviteFromReturnUrl(url: string | null | undefined): InviteRef | null` (matches `^/join/([^/?]+)/([^/?]+)(?:\?action=(?:accept|decline))?$` then validates both parts; anything else → null), `inviteFromQuery(params: ParamMap): InviteRef | null` (reads `inviteSlug` + `inviteToken`), `inviteReturnUrl(ref): string` → `/join/{slug}/{token}?action=accept`, `invitePagePath(ref): string` → `/join/{slug}/{token}`. Doc comment mirrors `return-url.ts`'s: one util so four components can't drift
- [X] T005 [P] Create `frontend/apps/web/src/app/core/utils/invite-ref.spec.ts`: parses with/without `?action=`, rejects bad slug (`Bad_Slug`, `-lead`, `ab`), bad token (`spaces here`, `<script>`, 10 chars, 200 chars), one part missing, `//join/…`, `https://evil/join/…`, `/join/a/b/c`; `inviteFromQuery` with both / one / neither; both composers produce exactly the documented strings

**Checkpoint**: Both sides agree on what an invite reference is.

---

## Phase 3: User Story 1 — The invitation survives registration and verification (Priority: P1) 🎯 MVP

**Goal**: Register from an invite link → the verification email's link carries the reference → the
verify page's sign-in button carries `returnUrl=/join/…?action=accept` → sign-in carries it into
the wizard (already works). No storage; the plain flow byte-identical.

**Independent Test**: quickstart §A steps 1–5 (cross-device via a second browser profile), plus
the "register without an invite" and "edit the link" negatives; `RegisterVerifyTests` green.

### Backend

- [X] T006 [US1] Edit `backend/Dtos/Auth/AuthRequests.cs`: append `[MaxLength(64)] string? InviteSlug = null, [MaxLength(128)] string? InviteToken = null` to `RegisterRequest` and `ResendVerificationRequest` (attributes on the constructor *parameters* — the file's header comment explains why). XML-doc both: opaque reference to the invite link the person came from; validated for shape only; never stored; only effect is the verification link
- [X] T007 [US1] Edit `backend/Services/Email/AuthEmailService.cs`: `SendVerificationEmailAsync(User user, string token, InviteReference? invite, CancellationToken ct)`; `BuildLink(path, userId, token, InviteReference? invite = null)` appends `&inviteSlug={Uri.EscapeDataString(slug)}&inviteToken={Uri.EscapeDataString(token)}` only when `invite` is non-null. Update the class doc: the reference rides *next to* the verification token, never inside it; the template prints whatever URL it is given (`email-verification.html` L8/L13) so no template change
- [X] T008 [US1] Edit `backend/Services/Auth/AuthService.cs`: `SendVerificationSafelyAsync(User user, InviteReference? invite, CancellationToken ct)`; in `RegisterAsync` parse ONCE after the terms check (`var invite = InviteReference.TryParse(request.InviteSlug, request.InviteToken, _teams)` — inject `IOptions<TeamOptions>` if not already present) and pass it in BOTH send branches (existing-unverified-email at L101-114 and the new-account send at L164); in `ResendVerificationAsync` parse the same way and pass it. Add a comment at the parse site: shape only, no DB read, malformed ⇒ null ⇒ today's link; the neutral response is untouched on every path (research R3/R4)
- [X] T009 [US1] Edit `backend/tests/JuggerHub.Api.IntegrationTests/Auth/AuthTestHelpers.cs`: `RegisterAsync(client, email, password = null, handle = null, inviteSlug = null, inviteToken = null)` and a `ResendVerificationAsync(client, email, inviteSlug = null, inviteToken = null)` helper, both posting the optional fields only when non-null
- [X] T010 [US1] Add to `backend/tests/JuggerHub.Api.IntegrationTests/Auth/RegisterVerifyTests.cs`: (a) register with a well-formed pair → link contains `&inviteSlug=berlin-jugger&inviteToken=<enc>`; (b) register with a malformed pair (slug `Bad_Slug`, or token with spaces, or slug only) → 200 with the identical neutral body AND the link has no `inviteSlug`; (c) register without the fields → link unchanged from today (no `invite` substring); (d) register the same unverified email twice with the pair → the SECOND mail's link carries it too (existing-email branch); (e) `resend-verification` with the pair → the new link carries it; without → not; (f) the pair is never persisted: after (a) verify + login and assert nothing — document in the test name that there is no column to assert on and the Restrict is structural
- [X] T011 [US1] Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~RegisterVerifyTests"` and the full Auth collection; commit `feat(053): carry the invite reference through the verification email (#324)`

### Frontend chain

- [X] T012 [P] [US1] Edit `frontend/apps/web/src/app/core/models/auth.models.ts`: add optional `inviteSlug?: string; inviteToken?: string;` to `RegisterRequest` and `ResendVerificationRequest` with doc comments matching T006
- [X] T013 [US1] Edit `frontend/apps/web/src/app/features/auth/register/register.component.ts`: derive `private readonly invite = inviteFromReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))` next to `signInParams`; in `submit()` spread `...(this.invite ? { inviteSlug: this.invite.slug, inviteToken: this.invite.token } : {})` into the register payload. Fix the stranded doc comment at L65-69 (it currently documents `appHost`) so it sits on `signInParams` and mentions the new server-side carry. `signInParams` itself unchanged
- [X] T014 [US1] Extend `frontend/apps/web/src/app/features/auth/register/register.component.spec.ts`: with `returnUrl=/join/berlin-jugger/<43-char-token>?action=accept` the POST body carries both fields; with `returnUrl=/players/x` or none it carries neither; with `returnUrl=/join/Bad_Slug/tok` it carries neither
- [X] T015 [US1] Edit `frontend/apps/web/src/app/features/auth/sign-in/sign-in.component.ts`: `resendVerification()` adds the pair from `inviteFromReturnUrl(this.returnUrl())` when present. Post-login branch (L58-63) untouched — add a one-line comment there pointing at 053 so nobody "fixes" it
- [X] T016 [US1] Create `frontend/apps/web/src/app/features/auth/sign-in/sign-in.component.spec.ts` (none exists): resend carries the pair when returnUrl is a join URL, not otherwise; a successful login with `onboardingCompleted:false` + returnUrl navigates to `/onboarding` with `queryParams.returnUrl` (pins the already-working hop)
- [X] T017 [US1] Edit `frontend/apps/web/src/app/features/auth/verify-email/verify-email.component.ts` + `.html`: read `inviteFromQuery(this.route.snapshot.queryParamMap)` in `ngOnInit`; expose `signInParams: Record<string,string>` = `{ returnUrl: inviteReturnUrl(ref) }` or `{}`; success button gets `[queryParams]="signInParams"`; `resend()` sends the pair too. Success copy unchanged
- [X] T018 [US1] Create `frontend/apps/web/src/app/features/auth/verify-email/verify-email.component.spec.ts` (none exists): both params well-formed → success link `href` contains `returnUrl=%2Fjoin%2F…%3Faction%3Daccept`; one missing or malformed → no `returnUrl`; resend forwards the pair; verification failure path unchanged
- [X] T019 [US1] Run `npx nx test web --testPathPattern="invite-ref|register.component|sign-in.component|verify-email.component"`, `npx nx lint web`; commit `feat(053): the verify page and register form carry the invite (#324)`

**Checkpoint**: A new account that follows every link arrives at `/onboarding?returnUrl=/join/…`.
The wizard still shows the plain step — that is US2.

---

## Phase 4: User Story 2 — The team step leads with the invitation (Priority: P1)

**Goal**: With a usable carried invite, the team step opens on an invitation card with a secondary
Accept; accepting joins immediately and confirms in place; finishing lands on `/t/{slug}`; the 029
search stays beneath; Continue is untouched.

**Independent Test**: quickstart §A steps 6–7 and the two exit negatives (dismiss-at-Welcome keeps
auto-accept; finish-without-accept strips it); onboarding spec green.

- [X] T020 [US2] Edit `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` — state: `carriedInvite` (field initialiser via `inviteFromReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))`), `invitePreview = signal<InvitePreviewState>('none')` where `type InvitePreviewState = 'none' | 'loading' | { kind: 'usable'; preview: InvitePreview } | 'expired' | 'invalid'`, `joinedSlugs = signal<string[]>([])`, `acceptingToken = signal<string | null>(null)`, `inviteError = signal<string | null>(null)`, `memberSlugs = computed(() => new Set(this.membership.teams().map(t => t.slug)))` (inject `MembershipService`). Update the class doc's team-step paragraph: Accept is its own press exactly like askToJoin; `next()`/`back()` still carry no team logic
- [X] T021 [US2] Same file — `ngOnInit`: when `carriedInvite` is set, `invitePreview.set('loading')` and `teamApi.getInvitePreview(token)` → `Usable` ⇒ `{kind:'usable', preview}`, `Expired` ⇒ `'expired'`, `Invalid`/error/404 ⇒ `'invalid'`; then `membership.load()` (comment: `/onboarding` is off-shell so the shell's load never ran; needed so "already on that team" can be told before a press — research R6). No carried invite ⇒ none of this runs (FR-017: zero extra requests)
- [X] T022 [US2] Same file — `acceptInvite(token: string, teamSlug: string)`: return if `acceptingToken()` set or `joinedSlugs().includes(teamSlug)`; `acceptingToken.set(token)`; `teamApi.acceptInvite(token)` → next: `joinedSlugs.update(l => [...l, r.teamSlug])`, `membership.load()`, clear guard; error: clear guard, `inviteError.set(transloco 'onboarding.team.invite.acceptError')` — inject `TranslocoService`; no status-code branching (a 409/404 both read as "couldn't join just now"; the card's state on reload tells the truth). Doc comment: no retry/timeout/backoff, `retryInterceptor` never repeats this POST (constitution VII), same reasoning as `askToJoin`
- [X] T023 [US2] Same file — `exitTarget(): string` per data-model.md: `joinedSlugs()[0]` → `/t/{slug}`; else carried && preview usable && `step() !== 'welcome'` → `invitePagePath(carried)`; else carried && (expired|invalid) → `'/'`; else `safeReturnUrl(returnUrl) ?? '/'`. Both `enterApp()` and `dismiss()` navigate to it. Doc comment carries research R5's reasoning verbatim in short: the wizard offered it, the player didn't press, auto-accepting on exit would make Continue an accept by another route; Welcome-dismiss never showed it, so today's behaviour stands
- [X] T024 [US2] Edit `frontend/apps/web/src/app/features/onboarding/onboarding.component.html` — inside `@case ('team')`, between the intro `<p>` and the search `<label>`: `@if (carriedInvite)` block with `@switch (invitePreview())`-style rendering: `'loading'` → `<jh-loading [label]="'onboarding.team.invite.loading' | transloco" data-testid="onboarding-invite-loading" />`; usable → a `jhCard padding="dense"` row copied from `my-team.component.html` L48-56 (letter tile, name, `myTeam.cityTeam`/`mixteam` · location · `<span class="font-mono">count</span>` members, `onboarding.team.invite.invitedBy`), `data-testid="onboarding-invite"`, eyebrow `onboarding.team.invite.eyebrow` above it; then EITHER `@if (joinedSlugs().includes(p.teamSlug))` → `<p data-testid="onboarding-invite-joined">` `invite.joined`; `@else if (memberSlugs().has(p.teamSlug))` → `<p data-testid="onboarding-invite-member">` `invite.alreadyMember`; `@else` → `<button jhButton variant="secondary" class="w-full" data-testid="onboarding-invite-accept" [disabled]="acceptingToken() !== null" (click)="acceptInvite(p.token…)">` `invite.accept`/`invite.accepting`; `@if (inviteError())` → `<jh-alert data-testid="onboarding-invite-error">`. Below the whole block, when any invitation is shown: `<p class="mt-lg text-body-sm text-muted">` `invite.orAnother`. Keep the HTML comment at L128-131 and extend it: the Accept button above is the only new network press on this step
- [X] T025 [US2] Add to `frontend/apps/web/public/i18n/en.json`, `de.json`, `es.json` under `onboarding.team.invite`: `eyebrow`, `loading`, `invitedBy` ("{{name}} invited you to join"), `accept` ("Accept & join {{team}}"), `accepting`, `joined` ("You're on {{team}} now. Welcome aboard." — must NOT mention approval), `alreadyMember`, `acceptError`, `orAnother` ("Or look for another team"), `expired`, `invalid`, `yourInvites`. German: sentence case, `–` for any dash; Spanish: raya rules per DESIGN.md. Run `npx nx test web --testPathPattern="catalog-"`
- [X] T026 [US2] Extend `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts` (new `describe('team step invitation')`): (a) a join returnUrl → preview GET fired on init, card renders inviter/team/count, Accept is `variant="secondary"`; (b) Accept posts once to `/invitations/{token}/accept`, confirmation text does NOT contain the 029 pending wording, Accept disappears, `membership.load` called; (c) Back then forward keeps the joined state; (d) `enterApp()` navigates to `/t/{slug}` after an accept — also after `finish()` path; (e) no accept + walked past Welcome → `enterApp()` navigates to `/join/{slug}/{token}` with NO `action`; (f) `dismiss()` at Welcome → navigates to the full `/join/…?action=accept`; (g) advancing past the step with a usable invite issues no request (extend the existing "never blocks" block); (h) no carried invite → no preview GET, no membership GET, template identical to before (no `onboarding-invite*` testids)
- [X] T027 [US2] Run `npx nx test web --testPathPattern="onboarding.component|catalog-"`, `npx nx lint web`, `npx nx build web`; commit `feat(053): the onboarding team step leads with the invitation (#324)`

**Checkpoint**: The whole MVP chain works end to end for a usable link invite.

---

## Phase 5: User Story 4 — Nothing about the invitation can trap the player (Priority: P1)

**Goal**: Expired / invalid / malformed / already-member / failed-accept all leave the search and
every exit working; notes are short and plain.

**Independent Test**: quickstart §A negatives (revoke, edit token, edit slug); spec cases below.

- [X] T028 [US4] Edit `frontend/apps/web/src/app/features/onboarding/onboarding.component.html`: in the `@if (carriedInvite)` block add `'expired'` → `<p class="… text-muted" data-testid="onboarding-invite-expired">` `invite.expired`; `'invalid'` → `data-testid="onboarding-invite-invalid"` `invite.invalid`; both followed by the normal search (no `orAnother` line — the search IS the path). `'none'` renders nothing. Verify no note surfaces status codes or the reference (FR-024)
- [X] T029 [US4] Extend the onboarding spec: (a) preview `Expired` → expired note, search still renders, Continue/skip/back all navigate, no request on advance; (b) preview `Invalid` and preview 404 → invalid note, same guarantees; (c) malformed reference in returnUrl (`/join/Bad_Slug/tok`) → no preview GET, no note, plain step; (d) accept fails (500) → `onboarding-invite-error` shown, Accept still enabled and pressable again, exactly one more POST on the second press, Continue still works; (e) carried invite for a team already in `membership.teams()` → `onboarding-invite-member`, no Accept button; (f) stale carried invite → `enterApp()` goes to `/`
- [X] T030 [US4] Run `npx nx test web --testPathPattern="onboarding.component"`; commit `feat(053): stale and failed invites never block the wizard (#324)`

---

## Phase 6: User Story 3 — Invitations already addressed to the account (Priority: P2)

**Goal**: Targeted invitations to the signed-in account are listed on the team step with Accept and
Decline, deduplicated by team against the carried invite; a failed list is an empty list.

**Independent Test**: quickstart §A "targeted invite" walk; spec cases below.

- [X] T031 [US3] Edit `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`: inject `InvitationService`; `addressedInvites = signal<MyInvitation[]>([])`; in `ngOnInit` always call `listMine()` → set items filtered by `teamSlug !== carriedInvite-team` (the carried team's slug is only known after the preview — filter with a `computed` `visibleAddressed` that reads `invitePreview()` so the dedupe follows the preview, research R8); error → `[]`. If the resulting list is non-empty and `membership.loaded()` is false, `membership.load()`. Add `declineInvite(token)`: `teamApi.declineInvite(token)` → remove from the list on next AND error (023 precedent); `acceptInvite` already handles targeted tokens — on success also remove the row from `addressedInvites`
- [X] T032 [US3] Edit `frontend/apps/web/src/app/features/onboarding/onboarding.component.html`: after the carried block, `@if (visibleAddressed().length > 0)` → eyebrow `invite.yourInvites` + `<ul>` of the same dense-card rows (`[attr.data-testid]="'onboarding-invite-' + inv.teamSlug"`), each with joined / already-member / Accept (`onboarding-invite-accept-{slug}`, secondary) + Decline (`onboarding-invite-decline-{slug}`, secondary, `common.decline`); the `orAnother` line shows when either block rendered
- [X] T033 [US3] Extend the onboarding spec (new `describe('team step addressed invitations')`): (a) `listMine` called on init regardless of returnUrl; two items render with names/inviters; (b) an item whose `teamSlug` equals the carried preview's team is hidden; (c) Decline posts to `/invitations/{token}/decline` and removes the row (also on 404); (d) Accept on an addressed item joins, confirms, removes; `/t/{slug}` becomes the exit; (e) `listMine` failing → no list, no error shown, plain/carried step intact; (f) no items + no carried → template identical to before (no new testids)
- [X] T034 [US3] Run `npx nx test web --testPathPattern="onboarding.component"`, lint; commit `feat(053): the team step lists invitations addressed to the account (#324)`

---

## Phase 7: Polish & Cross-Cutting

- [X] T035 Add an e2e scenario to `frontend/apps/web-e2e/src/onboarding.spec.ts` ("an invite link survives registration and the team step offers it"): as user A (`registerAndEnter`) create a team via `/teams/new` (pattern: `trainings.spec.ts` L20-27), open the team's invitations screen (route per `app.routes.ts`, component `team-invitations`), press `create-link`, read `invite-link` text; sign out; in a fresh context open the link → `accept-join` → land on `/sign-in?returnUrl=…` → `createAccount` link → `register()` helper (adapt: it navigates to `/register` directly — either pass the returnUrl or navigate via the link) → `verifyLinkPath` (assert it contains `inviteSlug=`) → verify page → `verify-success-signin` → sign in → `onboarding-start` → walk to the team step → `onboarding-invite` visible with the team name → `onboarding-invite-accept` → `onboarding-invite-joined` → continue → finish → `onboarding-enter` → URL matches `/t/{slug}`. Runs at desktop + mobile projects like the existing test
- [X] T036 [P] Copy `.specify/templates/ui-review-checklist-template.md` to `specs/053-invite-aware-onboarding/checklists/ui-review.md`; fill the header; add feature items: one coral CTA per view on the team step (Continue) with Accept/Decline secondary; invitation rows are `jhCard padding="dense"` (a card drawn one way); loading is `jh-loading` (no spinner); the joined line is not the 029 pending line; German at 375px — no truncation of `invite.accept` with a long team name (test with a 30-char name)
- [X] T037 Browser walk per the owner's standing rule (`/run` skill; compose stack + Mailpit): quickstart §A in full, in **German**, screenshots at 375px and desktop of the team step in each state (usable, joined, already member, expired, invalid, addressed list) and of the verify page's sign-in button carrying the returnUrl; record results in `checklists/ui-review.md`; fix anything found
- [X] T038 [P] Run the full verification set: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, `npx nx test web`, `npx nx lint web`, `npx nx build web`, `npx nx e2e web-e2e --grep "invite|onboarding"`; record counts
- [X] T039 [P] File a follow-up GitHub issue (`gh issue create --label security,frontend`): "`/join/{slug}/{token}` is a path, so 033's tracker records invite tokens verbatim" — pre-existing, outside 053; cite 033 FR-008a and `infra/modules/app/analytics.tf` L53; suggest excluding `/join/*` (and the event/party invite routes) from tracking or moving the token to the query. Reference #324 and the 053 plan's residuals section
- [X] T040 Amend `specs/029-onboarding-team-search/spec.md`: add an "Amended by feature 053" callout after the Context section and mark the out-of-scope line "team invitations or invite links in the step" as superseded (pattern: 022/046/048 amendments); amend `specs/005-team-space/spec.md` FR-031 with a note that 053 makes the register path hold
- [X] T041 Final report per CLAUDE.md (summary, files changed, verification run, failures/skips, risks, spec/design drift, GH #324 status); commit `docs(053): amend 029 and 005 for the invite-aware team step (#324)`; push the branch and open a PR with `Closes #324`

---

## Dependencies

```text
Phase 1 (T001–T002)  →  Phase 2 (T003 ∥ T004 → T005)
                         ├─ T003 → US1 backend (T006 → T007 → T008 → T009 → T010 → T011)
                         └─ T004 → US1 frontend (T012 ∥ T013 → T014; T015 → T016; T017 → T018) → T019
US1 (T019)           →  US2 (T020 → T021 → T022 → T023 → T024 → T025 → T026 → T027)
US2 (T027)           →  US4 (T028 → T029 → T030)   [same two wizard files — sequential]
US4 (T030)           →  US3 (T031 → T032 → T033 → T034)
US3 (T034)           →  Polish (T035; T036 ∥ T038 ∥ T039; T037 after T036; T040 → T041)
```

US1's backend and frontend halves are independent of each other and can run in parallel after
Phase 2. US2/US4/US3 all edit `onboarding.component.{ts,html,spec.ts}` and are strictly ordered.

## Parallel Execution Examples

- After T003/T004: T006–T011 (backend) alongside T012–T019 (frontend chain).
- Within the frontend chain: T013/T014 (register), T015/T016 (sign-in), T017/T018 (verify) touch
  disjoint files and can proceed together once T004 exists.
- Polish: T036, T038, T039 in parallel; T037 needs T036's checklist to record into.

## Implementation Strategy

**MVP = US1 + US2** (T001–T027): the invite survives the email and the wizard offers it. That is
the whole of #324's user-visible promise. **US4** is next, not optional — it is P1 and guards the
first screen after registration — but it builds on US2's markup so it follows it. **US3** completes
the picture cheaply on top. Polish closes the Gate 7 and browser-walk obligations and files the
analytics follow-up.

Small commits, one per checkpoint, each with `#324` in the message.
