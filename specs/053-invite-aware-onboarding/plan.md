# Implementation Plan: Invite-Aware Onboarding

**Branch**: `053-invite-aware-onboarding` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/053-invite-aware-onboarding/spec.md` — GH #324

## Summary

A person who opens a team's shared invite link with no account registers, verifies, signs in and
walks the onboarding wizard — and the product forgets they were invited. The trail is carried
carefully everywhere except across the **verification email**: `RegisterComponent` only decorates
its own sign-in links with the `returnUrl`, `RegisterRequest` has no field that could carry it,
`AuthEmailService.BuildLink` builds `verify-email?userId&token` and nothing else, and
`VerifyEmailComponent`'s success state links to a bare `/sign-in`. From sign-in onward the chain
*already works* (`sign-in.component.ts:58-63` carries `returnUrl` into `/onboarding`;
`onboarding.component.ts:381` honours it on exit) — the issue's claim that onboarding drops it is
wrong, and was corrected by reading the code.

The fix has two halves, in the shape the owner chose:

1. **Carry the invitation through the server, never store it.** Registration (and the two
   resend-verification paths) accept an optional **invite reference** — the invite link's two
   path segments, `slug` + `token`, validated for **shape only** by a pure helper — and
   `BuildLink` appends them to the verification link as `inviteSlug`/`inviteToken`. The verify
   page rebuilds `returnUrl=/join/{slug}/{token}?action=accept` from those two validated parts
   and puts it on its sign-in button; from there the existing chain carries it into the wizard.
   Nothing is written to any table. **No entity, no column, no migration, no new endpoint** — two
   optional request fields, one optional link suffix.
2. **The team step leads with the invitation.** `OnboardingComponent` parses the carried
   reference out of its `returnUrl`, previews it through the existing anonymous
   `GET /invitations/{token}`, lists the account's addressed invitations through the existing
   `GET /profiles/me/invitations`, and offers Accept (the existing `POST /invitations/{token}/accept`).
   Accepting keeps the player in the wizard; the exit lands on the joined team. The 029 search
   stays underneath, unchanged. 029's load-bearing rule — **Continue is pure navigation with no
   network call** — is preserved: Accept is its own press, exactly like "Ask to join".

Everything reused already exists and is already called by other screens. The only backend change
is *carrying* — and the carried thing is an identity, never a destination, which is what makes
"an emailed link that redirects" a non-issue: the link contains no *where*.

## Technical Context

**Language/Version**: C# / .NET 10, EF Core (backend — DTO + service + email link only, no data
access change); TypeScript / Angular 22 (Nx workspace), **zoneless** change detection (frontend)

**Primary Dependencies**: Existing — ASP.NET Identity (`GenerateEmailConfirmationTokenAsync`),
`AuthEmailService` + `EmailTemplateService` (the verification template already prints the URL it is
given), `TeamSlugPolicy` (slug format rule, reused for the reference), Angular signals + rxjs,
Transloco, `jh-loading`/`jh-alert`/`jhButton`/`jhCard`, `safeReturnUrl`. **No new package.**

**Storage**: None. `TeamInvitation`, `PlayerProfile`, `User` are read as today. **If a task ever
produces an `Add-Migration`, something is wrong.**

**Testing**: Backend — xUnit integration tests (`RegisterVerifyTests`, `_factory.EmailSender.LatestFor`
already asserts on the verification link body). Frontend — Jest component specs (zoneless: no
`fakeAsync`; `HttpTestingController`), plus `catalog-parity.spec.ts` / `catalog-punctuation.spec.ts`
for the new copy. E2E — Playwright (`registerVerify` already reads the verification link from
Mailpit via `linkFromEmail(request, to, 'verify-email')`, which is exactly the hop under test).

**Target Platform**: Web, mobile-first; the wizard is a `max-w-sm` column that must read at 375px in
German.

**Project Type**: Web application (backend + frontend, both touched)

**Performance Goals**: When an invitation is known, the team step costs at most three extra `GET`s
(preview, addressed list, memberships) issued on wizard entry, none on advance. When none is known,
**zero** extra requests — the plain flow stays byte-identical in requests as well as pixels.

**Constraints**: Continue/Skip/"I'm not on a team yet"/Back never issue a request (029 FR-017/018);
the reference is validated on every hop that turns it into a destination; no invite reference in
logs; one coral CTA per view (Continue) so Accept is `variant="secondary"` like "Ask to join";
`catalog-parity.spec.ts` goes red unless all three catalogues change together.

**Scale/Scope**: ~4 backend files (DTO ×1, service ×1, email service ×1, new pure helper ×1) + tests;
~7 frontend files (new util ×1, register/sign-in/verify-email/onboarding edits, models) + specs;
~12 i18n keys × 3; one e2e scenario.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS, with three deliberate choices:
  - The carried reference is **never a URL**. The server accepts `inviteSlug` + `inviteToken`,
    validates their *shape* (`TeamSlugPolicy` format + length bounds; base64url charset with length
    bounds for the token), and places the two parts — separately URL-encoded — on the link it
    builds itself. A hand-edited link can only ever yield "no invite". The verify page validates
    again before it composes any path, and only ever composes `/join/{slug}/{token}?action=accept`
    — the same route any holder of the link reaches. Nothing here can redirect outside the app.
  - The reference **grants nothing**: preview is already anonymous (005/026 decision, `[AllowAnonymous]`
    at `InvitationsController.cs:28`), accept still requires the JWT and re-checks usability and
    membership server-side (`TeamInvitationService.AcceptAsync`). Registration does not touch the
    invitation at all — not even a read — so the register endpoint does not become an oracle for
    invite tokens (the preview endpoint already is one, by design; this adds no second surface).
  - A malformed reference is **dropped silently**; the registration proceeds and the neutral
    `NeutralCheckEmail` response is unchanged in every branch (FR-003/FR-004). The reference is not
    logged: the only new log line is none — the existing "Failed to send verification email" stays
    as is.
- **II. Thin Controllers, Service-Centric Backend** — PASS. `AuthController` is untouched (the
  record gains two optional positional parameters; model binding does the rest). Parsing lives in
  a pure static helper (`InviteReference.TryParse`) next to `TeamSlugPolicy`; `AuthService` passes
  the parsed value down; `AuthEmailService` renders it. No new interface, no new DI registration.
- **III. Disciplined Data Access** — N/A. No query changes; no `ExecuteUpdate`; no list surface.
- **IV. Secure Authentication & Session Management** — PASS. The verification token, its lifetime
  and `ConfirmEmailAsync` are untouched; the invite reference rides *next to* the token in the
  query, never inside it. Cookies/JWT unchanged.
- **V. Environment Parity** — PASS. Nothing environment-specific; the link base is the existing
  `FrontendBaseUrl`.
- **VI. Conventions & Tooling** — PASS. Separate `.ts`/`.html`/`.css`; no scripts.
- **VII. Resilient by Default, Never Amplifying** — **NOT engaged for the backend** (no outbound
  call is added; the verification email goes through the existing `IEmailSender` with its existing
  028 policy). On the browser hop, PASS by inheritance: the preview, addressed list and membership
  reads are `GET`s (time-limited + transiently retried by `retryInterceptor`); **accept and decline
  are `POST`s and are never retried** — a replayed accept is harmless here (the service is
  idempotent) but the rule does not bend for endpoints that look safe (029 precedent). FR-022's
  "retry" is a *press*. No hand-rolled retry, timeout or backoff anywhere in this diff.
- **Quality Gate 7 (UI/Design compliance)** — **APPLIES.** New markup on the team step and new copy
  in three catalogues. Instantiate `checklists/ui-review.md` from the template. The binding case is
  the **invitation card + notes at 375px in German** (the longest locale), per the owner's standing
  browser-walk rule.
- **Quality Gate 8 (Resilience)** — APPLIES to the browser hop only, satisfied by inheritance as
  under VII. Nothing to configure.

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/053-invite-aware-onboarding/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1–R9
├── data-model.md        # Phase 1 — the transient reference + client view-model (nothing persisted)
├── quickstart.md        # Phase 1 — manual + automated validation walk
├── contracts/
│   ├── auth-api.md          # The two optional request fields + the verification-link suffix
│   └── consumed-endpoints.md # Existing invitation/membership endpoints the wizard now calls
├── checklists/
│   ├── requirements.md  # spec quality (done)
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Dtos/Auth/AuthRequests.cs                 # EDIT — RegisterRequest + ResendVerificationRequest gain
│                                              #        optional InviteSlug / InviteToken
├── Services/Teams/InviteReference.cs         # NEW  — pure shape validation (slug via TeamSlugPolicy,
│                                              #        token via base64url regex); no DB
├── Services/Auth/AuthService.cs              # EDIT — parse once, pass to SendVerificationSafelyAsync
│                                              #        in all three send paths (new user, existing
│                                              #        unverified email, resend)
├── Services/Email/AuthEmailService.cs        # EDIT — SendVerificationEmailAsync(…, InviteReference?);
│                                              #        BuildLink appends &inviteSlug=&inviteToken=
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Auth/RegisterVerifyTests.cs           # EDIT — link carries / drops / omits the reference
    └── Auth/AuthTestHelpers.cs               # EDIT — RegisterAsync gains optional invite args

frontend/apps/web/src/app/
├── core/utils/invite-ref.ts                  # NEW  — InviteRef; parse from a /join returnUrl, parse
│   └── invite-ref.spec.ts                    #        from query params, compose the join path
├── core/models/auth.models.ts                # EDIT — optional inviteSlug/inviteToken on the two requests
├── features/auth/register/                   # EDIT — send the reference parsed from returnUrl
├── features/auth/sign-in/                    # EDIT — resend carries the reference parsed from returnUrl
├── features/auth/verify-email/               # EDIT — read inviteSlug/inviteToken; success button carries
│                                              #        returnUrl=/join/…?action=accept; resend carries it
├── features/onboarding/
│   ├── onboarding.component.ts               # EDIT — carried invite, preview, addressed list, accept,
│   ├── onboarding.component.html             #        joined state, exit destination
│   └── onboarding.component.spec.ts          # EDIT — the scenarios in quickstart §B
└── public/i18n/{en,de,es}.json               # EDIT — onboarding.team.invite.* (×3, together)

frontend/apps/web-e2e/src/
└── onboarding.spec.ts                        # EDIT — invite link → register → verify → sign in → accept
```

**Structure Decision**: The wizard work stays inside `OnboardingComponent`, as 029 decided and for
the same reasons (one screen in one wizard with wizard-specific rules; the reusable part — fetch
state — is already `BrowseList`). The invitation row markup is **copied** from the "My team" home's
invite row (`my-team.component.html:48-56`), not extracted: it is eight lines of presentation with no
rule inside it, which is the 029 precedent ("12 lines of markup, not a component") rather than the
052 one (an `@switch` encoding a rule about whether an invite may be offered). The "My team" home is
**not touched**. The invite page (`InviteAcceptComponent`) keeps its own hero layout and is not
touched either — only its route is composed by the new util.

The reference parsing is **one util** (`core/utils/invite-ref.ts`) shared by four components, for the
same reason `safeReturnUrl` is one util: four hand-written regexes for the same two segments would be
four places for the shape to drift.

## Implementation Shape

Sketch only — `/speckit-tasks` turns this into ordered tasks.

### Backend: carry the reference

```csharp
public sealed record RegisterRequest(
    …existing six…,
    [MaxLength(64)] string? InviteSlug = null,
    [MaxLength(128)] string? InviteToken = null);

public sealed record ResendVerificationRequest(
    [Required, EmailAddress] string Email,
    [MaxLength(64)] string? InviteSlug = null,
    [MaxLength(128)] string? InviteToken = null);
```

`InviteReference.TryParse(slug, token, TeamOptions)` — both present, slug passes
`TeamSlugPolicy.Normalize` + `Validate(min,max) == None`, token matches `^[A-Za-z0-9_-]{16,128}$`
(`NewToken()` yields 43 base64url chars from 32 random bytes; the bounds leave room without
accepting arbitrary text). Anything else → `null`, and registration proceeds exactly as today.

`AuthService.RegisterAsync` parses once at the top (after the terms check — the reference never
changes a refusal) and hands the result to `SendVerificationSafelyAsync(user, invite, ct)` in **both**
send branches: the new-account branch and the existing-unverified-email branch (that branch resends
verification; a person who registers twice from the same invite link must not lose it on the second
try). `ResendVerificationAsync` does the same.

`AuthEmailService.BuildLink` gains an optional suffix:
`…/verify-email?userId=…&token=…&inviteSlug={enc}&inviteToken={enc}`. The template already prints
whatever URL it is handed (`email-verification.html:8,13`); no template change.

### Frontend: the chain

`core/utils/invite-ref.ts`:

| Function | Purpose |
|---|---|
| `inviteFromReturnUrl(url)` | `/join/{slug}/{token}` with optional `?action=accept\|decline` → `InviteRef \| null` |
| `inviteFromQuery(params)` | `inviteSlug` + `inviteToken` query params → `InviteRef \| null` |
| `inviteReturnUrl(ref)` | `/join/{slug}/{token}?action=accept` (what sign-in should carry) |
| `invitePagePath(ref)` | `/join/{slug}/{token}` (the page, no automatic action) |

Both parsers apply the same two regexes the server applies; a value that fails is `null`, and every
caller treats `null` as "no invite" (FR-020).

- **Register**: `submit()` adds `inviteSlug`/`inviteToken` when `inviteFromReturnUrl(returnUrl)`
  is non-null. `signInParams` is unchanged.
- **Sign-in**: `resendVerification()` adds the same two fields from its `returnUrl()`.
  The post-login branch is **unchanged** — it already carries `returnUrl` into `/onboarding`.
- **Verify email**: reads `inviteFromQuery`; the success button gets
  `[queryParams]="signInParams"` = `{ returnUrl: inviteReturnUrl(ref) }` when present; the resend
  form sends the two fields too.

### Frontend: the wizard

New state in `OnboardingComponent` (all signals — zoneless, 045's lesson):

| Signal | Purpose |
|---|---|
| `carriedInvite` | `inviteFromReturnUrl(returnUrl)`, set in the field initialiser |
| `invitePreview` | `'none' \| 'loading' \| { kind:'usable', p } \| 'expired' \| 'invalid'` |
| `addressedInvites` | `MyInvitation[]` from `listMine()`, minus any whose `teamSlug` equals the carried team |
| `joinedSlugs` | Ordered list of teams joined through the step; `[0]` is the exit destination |
| `acceptingToken` | In-flight guard; never gates Continue |
| `inviteError` | Quiet failure line for a failed accept |
| `memberSlugs` | From `MembershipService.teams()`, loaded only when an invitation is known |

`ngOnInit` adds, **only when `carriedInvite` is set**: `getInvitePreview(token)` → `usable` /
`expired` (`state === 'Expired'`) / `invalid` (`state === 'Invalid'` or a 404). And, always,
`listMine()` → `addressedInvites` (a failed list is an empty list — FR US3-4). If either yields an
invitation, `membership.load()` so "already on that team" can be told before a press (FR-021). No
invitation known → no extra request → the plain 029 step, byte-for-byte (FR-017).

`acceptInvite(token, teamSlug)`: guard on `acceptingToken`; `POST accept`; on success push the slug
onto `joinedSlugs`, remove any addressed entry for that team, `membership.load()`; on failure set
`inviteError` — **no automatic retry**. `declineInvite(token)`: `POST decline`; remove from the list
either way (023 precedent). Both are their own press; `next()`/`back()` are not touched.

**Exit destination** (`enterApp()` and `dismiss()` share one `exitTarget()`):

1. `joinedSlugs()[0]` → `/t/{slug}` (FR-016).
2. Carried invite, still usable, and the wizard was walked past Welcome → `invitePagePath(ref)`:
   the invite page **without** the automatic accept. The wizard offered it and the player chose
   not to press; resuming the pre-sign-in `action=accept` behind their back would make Continue
   accept by another route (FR-011). Dismissing at Welcome never showed the invite, so the
   pre-sign-in intent is honoured exactly as today (spec edge case).
3. Carried invite that turned out stale → `/` (the step already said so; the invite page would
   only say it again).
4. Otherwise `safeReturnUrl(returnUrl) ?? '/'`, unchanged.

**Template**: above the search, when `invitePreview()` is `usable`, an invitation card in the "My
team" row treatment (letter tile, name, type · location · members, "Invited by {name}") with a
secondary `jhButton` "Accept & join {team}" — or the joined confirmation, or the already-a-member
line. Addressed invitations render as the same rows with Accept + Decline. Expired/invalid carried
invites render as one `text-muted` sentence. Then a short "Or look for another team" line and the
unchanged 029 search block. Loading is `jh-loading` (one line, never a spinner).

### i18n

`onboarding.team.invite.*` ≈ 12 keys in **all three catalogues at once** (`catalog-parity.spec.ts`);
German uses `–` never `—` (`catalog-punctuation.spec.ts`). The joined confirmation must not share
words with `askedConfirmation` ("An admin still has to say yes") — membership by invitation is
immediate (FR-010).

## Complexity Tracking

No constitution violations; section intentionally empty.

## Deviations and residuals (recorded)

- **Cross-device limit** (owner-accepted): verify on one device, then sign in cold on another
  without following the verify page's button → plain step. Storing the reference on the account
  was declined; revisitable without undoing anything here.
- **`/join/{slug}/{token}` is a *path*, so 033's analytics records the invite token verbatim
  today** (033 FR-008a excludes only the query string). This feature adds nothing to that — the
  new `verify-email` and `onboarding` carriers are query strings and are discarded before send —
  but the pre-existing exposure is worth its own issue (filed at implementation).
- **An addressed (targeted) invitation to the same team as the carried link invite** is hidden
  behind the carried one and stays `Pending` server-side after the link is accepted. It expires in
  ≤7 days and is never shown anywhere the player already has that team. Accepted, not fixed.
- **No draft persistence** for the invite state (045 covers the two big wizards): the carried
  reference lives in the URL and survives a reload by construction; `joinedSlugs` does not, but
  a reload after an accept simply shows "already on that team" via `MembershipService`.
