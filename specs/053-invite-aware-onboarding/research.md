# Research: Invite-Aware Onboarding (053)

Every item below was settled by reading the code on `main` (2026-09-19), not by assumption. File
references are to the current tree.

## R1 — Where the chain actually breaks (and where it does not)

**Decision**: Fix exactly two links — registration input → verification link, and the verify page's
sign-in button. Leave sign-in and the wizard's returnUrl handling alone.

**Rationale**: The issue says the wizard "forgets" the invite. It does not:

- `invite-accept.component.ts:110` builds `returnUrl=/join/{slug}/{token}?action=accept` for a
  signed-out visitor.
- `sign-in.component.html:76` forwards `returnUrl` to `/register`; `register.component.ts:77-80`
  forwards it back to its sign-in links — **but never sends it to the server** (`submit()` at
  `:226-234` posts email/password/handle/terms only).
- `AuthEmailService.BuildLink` (`:82-87`) is `{base}/{path}?userId={id}&token={enc}` — no third
  parameter, and `RegisterRequest` (`AuthRequests.cs:30-36`) has no field that could feed one.
- `verify-email.component.html:13-19` — the success state's button is `routerLink="/sign-in"` with
  **no query params**. Even a register page that transmitted the invite would lose it here.
- `sign-in.component.ts:58-63` — **already** sends a not-yet-onboarded user to `/onboarding` with
  `queryParams: { returnUrl }`.
- `onboarding.component.ts:381` — `enterApp()` **already** honours `safeReturnUrl(returnUrl)`.

So the repair is the email hop, and the wizard's job is to *read* what already arrives.

**Alternatives considered**: (a) storing the reference on the account — declined by the owner (spec
Clarifications); (b) browser storage — declined (useless across devices, which is the common case
for a verification mail).

## R2 — What to carry: two validated path segments, not a URL

**Decision**: Carry `inviteSlug` + `inviteToken` as two separate optional request fields and two
separate query params on the emailed link. Never carry a path.

**Rationale**: The invite link is `/join/{slug}/{token}` (`TeamEmailService.BuildJoinLink`,
`:69-73`). Its two segments have tight, already-defined shapes:

- slug — `TeamSlugPolicy.SlugRegex` `^[a-z0-9]+(?:-[a-z0-9]+)*$`, length `TeamOptions.SlugMinLength`
  (3) … `SlugMaxLength` (30); `TeamSlugPolicy.Validate(normalized, min, max)` is the existing pure
  check (`TeamService.cs:69,88` call it the same way).
- token — `NewToken()` (`TeamInvitationService.cs:461-465`): 32 random bytes → base64 → `=` trimmed,
  `+`→`-`, `/`→`_` ⇒ **43 chars of `[A-Za-z0-9_-]`**. Validate as `^[A-Za-z0-9_-]{16,128}$` (bounds
  rather than `{43}` so a future token-length change does not silently drop every invite).

Two fields mean the server never parses a path and never composes one it did not build. The verify
page rebuilds the path from the two validated parts; a tampered value fails its regex and the button
degrades to a plain `/sign-in`. `safeReturnUrl` (`core/utils/return-url.ts:10-12`) stays the
open-redirect guard for the *composed* path, as everywhere else.

**Alternatives considered**: a single `invite=slug/token` param (needs a separator convention and a
split on both sides — nothing gained); carrying the full `returnUrl` server-side "validated like
`safeReturnUrl`" (the issue's first option — rejected because a validated *path* is still a path the
email could be pointed at any in-app route, whereas an identity cannot be pointed anywhere).

## R3 — Validate shape only; never touch the invitation at registration

**Decision**: `InviteReference.TryParse` is pure (no DB). Registration does not check that the
invitation exists, is usable, or belongs to the slug.

**Rationale**: FR-003/FR-004/FR-005. A DB read inside registration would (a) add latency to the
enumeration-sensitive path for no user-visible gain — the wizard previews the invite anyway, and
(b) make `POST /auth/register` a second oracle for invite tokens. The preview endpoint is already an
anonymous oracle by 005's design; adding another surface is what Principle I exists to prevent. A
stale invite is a first-class wizard state (FR-018/019), not a registration concern.

## R4 — Thread the reference through all three verification-send paths

**Decision**: `SendVerificationSafelyAsync(User, InviteReference?, ct)`; called with the parsed
reference from (1) the new-account branch, (2) the existing-unverified-email branch of
`RegisterAsync` (`AuthService.cs:101-114`), and (3) `ResendVerificationAsync` (`:231-239`).

**Rationale**: Branch (2) is reachable by the invited person themselves — they register, do not
find the mail, register again from the same link. Today that branch silently resends; without the
reference it would silently resend a link that drops the invite. The two client resend surfaces
(sign-in's `resendVerification()` at `sign-in.component.ts:85-96`, verify page's form at
`verify-email.component.ts:45-55`) both know the invite (sign-in from `returnUrl`, verify from its
own query) and pass it on (FR-007). The neutral responses are untouched in all three.

## R5 — Exit destination after the wizard

**Decision**: `exitTarget()` = joined team → `/t/{slug}`; else carried-and-usable-and-walked →
`/join/{slug}/{token}` **without** `action=accept`; else carried-and-stale → `/`; else the existing
`safeReturnUrl(returnUrl) ?? '/'`.

**Rationale**: Today `enterApp()` navigates to the carried `/join/…?action=accept`, and
`InviteAcceptComponent.maybeResume()` (`:72-91`) then **accepts automatically** if signed in and
usable. That was right when the wizard could not offer the invite. Once it does, a player who saw
"Accept & join" and pressed Continue instead has made a choice; auto-accepting on the way out would
turn Continue into an accept by another route (FR-011). Dismissing from Welcome never showed the
card, so there the pre-sign-in intent still stands, exactly as today — the spec's edge case says so
explicitly. `dismiss()` fires only from Welcome (`onboarding.component.html:29-37`), so
"`step() !== 'welcome'`" is the whole discriminator.

**Alternatives considered**: always strip the action (loses the Welcome-dismiss behaviour for no
reason); always keep it (Continue becomes an accept).

## R6 — Already-a-member without a press: `MembershipService`, loaded only when needed

**Decision**: When the wizard knows of any invitation (carried or addressed), call
`membership.load()` on entry and compare `teamSlug` against `membership.teams()`; render "You're
already on {team}" with no Accept (FR-021). Do **not** load it otherwise.

**Rationale**: `/onboarding` is outside the shell (`app.routes.ts:271`), so the shell's
`membership.load()` (`shell.component.ts:45`) has not run. The accept endpoint answers **200** for
both `Joined` and `AlreadyMember` (`InvitationsController.cs:50`), so the press cannot tell them
apart — the list can. One extra `GET`, only in the invite case, keeps the plain flow's request
count identical (FR-017, SC-003).

## R7 — Copy the invite-row markup; do not extract a component; do not touch "My team"

**Decision**: Reproduce the 8-line invite row from `my-team.component.html:48-56` (letter tile,
name, type · location · members, "Invited by") inside the wizard template.

**Rationale**: 029 copied the browse row rather than extracting `BrowseShellComponent` ("12 lines
of markup, not a component"). 052 extracted the invite *search* because its `@switch` encoded a rule
(may this person be invited?) that had already drifted once. The invite row encodes no rule — it is
presentation — and the wizard's Accept is a **secondary** button (one coral CTA per view: Continue)
while "My team"'s is primary, so the two rows differ in their actions anyway. Extracting would touch
an unrelated screen for a component with two consumers and a variant prop. Not worth it.

## R8 — Deduplicate by team, carried invite first

**Decision**: An addressed invitation whose `teamSlug` equals the carried invite's team is not
listed. Order: carried card, then addressed rows (server order, newest first).

**Rationale**: Spec FR-012 "no invitation appears twice". Two invitations to one team (a link plus a
targeted one) are one offer to the player. Residual: the targeted one stays `Pending` server-side
after the link is accepted (`AcceptAsync` consumes only the token it was given, `:389-412`); it
expires in ≤7 days and no surface shows it once the player has the team. Accepted.

## R9 — Analytics and logs

**Decision**: No new exposure; note the pre-existing one.

**Rationale**: 033 FR-008a discards the query string in the tracker (`data-exclude-search`,
`infra/modules/app/analytics.tf:53`), so `verify-email?…&inviteSlug&inviteToken` and
`onboarding?returnUrl=…` are never recorded. `/join/{slug}/{token}` is a **path** and is recorded
verbatim today — pre-existing, outside this feature, worth a follow-up issue. Nothing in this diff
logs the reference; the only log line on the send path is the existing failure message with no
values.

## R10 — Test surfaces already in place

- Backend: `RegisterVerifyTests` reads the sent verification HTML through
  `_factory.EmailSender.LatestFor(email).HtmlBody` (`:23-26`) — the link suffix is directly
  assertable. `AuthTestHelpers.RegisterAsync` posts an anonymous object; add two optional args.
- Frontend: `onboarding.component.spec.ts` already has a "team step never blocks onboarding" block
  that asserts zero requests on advance (`:551`) and a returnUrl block (`:286-317`); extend both.
- E2E: `support/auth.ts` `linkFromEmail(request, to, 'verify-email')` reads the emailed link from
  Mailpit — the very hop under test. No e2e helper creates a team invite link yet; the scenario
  will create a team via `/teams/new` (as `trainings.spec.ts:20` does) and take the link from the
  invitations screen.
