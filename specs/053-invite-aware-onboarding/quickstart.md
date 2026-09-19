# Quickstart: Invite-Aware Onboarding (053)

## Prerequisites

- Local stack up: `docker compose up -d` (Postgres, Redis, Mailpit at http://localhost:8025,
  backend, frontend).
- One existing, onboarded account that is admin of a team (create one via `/teams/new` if needed).
- A second, fresh email address for the invited player (Mailpit accepts anything).

## A. Manual walk (the whole chain, cross-device)

1. As the team admin: open the team's invitations screen, create the shared invite link, copy it.
2. In a **private window** (signed out): open the link → `/join/{slug}/{token}` → press
   **Accept & join** → you land on `/sign-in?returnUrl=…`.
3. Choose **Create account** → register with the fresh email → "Check your email".
4. **Open Mailpit in a different browser profile** (this is the cross-device case). Open the
   verification mail; confirm the link ends in `&inviteSlug=…&inviteToken=…`. Click it.
5. On "Email verified", press **Sign in**; confirm the address bar carries
   `returnUrl=/join/{slug}/{token}?action=accept`. Sign in.
6. The wizard opens. Walk to the team step: it leads with **"{inviter} invited you to join
   {team}"** and a secondary **Accept & join {team}**; the 029 search is beneath "Or look for
   another team".
7. Press Accept: the card says you are on the team now; Accept is gone; Continue was never
   disabled. Continue → photo → Finish → Done → **Enter** lands on `/t/{slug}`.
8. Repeat 2–6 in **one** browser (same-device case) — same result.

**Negative walks** (each ends with the search working and Continue / "I'm not on a team yet" / Back
all working):

- Revoke the link as admin between steps 3 and 6 → the step shows "no longer valid" note + search.
- Edit `inviteToken` in the emailed link to garbage → verify page's Sign in has no `returnUrl`;
  the plain step.
- Edit only `inviteSlug` to `x` → same (both or neither).
- Register **without** an invite → the verification link has no suffix, the step is the 029 step.
- Dismiss at Welcome with a carried invite → you land on the invite page and it auto-accepts, as
  today.
- Walk the wizard, do **not** press Accept, finish → you land on the invite page **without** an
  automatic accept.
- Have the admin send a **targeted** invite to a verified-but-not-onboarded account, then sign that
  account in → the step lists it with Accept and Decline; Decline removes it.
- Switch to **German** at 375px and re-check step 6: nothing truncated, one coral CTA (Continue).

## B. Automated checks

```powershell
# Backend — the link carries / drops / omits the reference; neutral responses unchanged
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~RegisterVerifyTests"

# Frontend — util, register, sign-in, verify-email, onboarding, catalogue guards
npx nx test web --testPathPattern="invite-ref|register.component|sign-in.component|verify-email.component|onboarding.component|catalog-"

# Lint + build
npx nx lint web ; npx nx build web

# E2E (needs the compose stack incl. Mailpit)
npx nx e2e web-e2e --grep "invite"
```

Expected frontend scenarios (each an `it(...)`):

- `invite-ref`: parses `/join/{slug}/{token}` with and without `?action=`, rejects bad slug, bad
  token, one-without-the-other, `//`, absolute URLs; composes both paths.
- `register`: sends `inviteSlug`/`inviteToken` when `returnUrl` is a join URL; sends neither
  otherwise; `signInParams` unchanged.
- `sign-in`: resend carries the pair when the returnUrl is a join URL; post-login branch unchanged.
- `verify-email`: success button carries `returnUrl=/join/…?action=accept` when both params parse;
  no `returnUrl` when either is missing or malformed; resend forwards the pair.
- `onboarding`: usable carried invite renders the card and no request is made on advance; Accept
  posts once, confirms membership wording (not the 029 pending wording), and makes `/t/{slug}` the
  exit; a failed Accept is reported and pressable again; expired / invalid / 404 render the two notes;
  a malformed reference is the plain step with zero extra requests; addressed invites are listed,
  deduplicated by team, and Decline removes; already-member shows no Accept; dismiss-at-Welcome keeps
  `?action=accept`, finishing without accepting strips it.

## C. Gate 7

Copy `.specify/templates/ui-review-checklist-template.md` to `checklists/ui-review.md`; verify each
item against the diff; screenshot the team step at 375px and desktop, **in German**, in each state
(usable, joined, already member, expired, invalid, addressed list).
