# Data Model: Invite-Aware Onboarding (053)

**Nothing is persisted by this feature.** No entity, no column, no migration. This file records the
one transient value that is carried and the client view-model the wizard holds.

## Existing entities (read only, unchanged)

- **`TeamInvitation`** (`backend/Entities/TeamInvitation.cs`) — `Kind` (`Link` multi-use /
  `Targeted` single-use), raw `Token`, `Status` (`Pending`/`Accepted`/`Declined`/`Revoked`;
  `Expired` is derived from `ExpiresDate`), `TeamId`, `CreatedByUserId`, `TargetUserId?`.
  Read by preview / accept / decline / list-mine exactly as today.
- **`TeamMembership`** — written by `AcceptAsync` exactly as today.
- **`PlayerProfile.OnboardingCompletedAt`** — written by `completeOnboarding` exactly as today.

## Transient: the invite reference

Carried, never stored.

| Part | Shape | Where it comes from | Where it goes |
|---|---|---|---|
| `slug` | `^[a-z0-9]+(?:-[a-z0-9]+)*$`, 3–30 chars (`TeamOptions.SlugMinLength/MaxLength`, via `TeamSlugPolicy`) | first segment of `/join/{slug}/{token}` | `RegisterRequest.InviteSlug`, `ResendVerificationRequest.InviteSlug`, verification-link `inviteSlug` |
| `token` | `^[A-Za-z0-9_-]{16,128}$` (today's tokens are 43 base64url chars) | second segment | `…InviteToken`, verification-link `inviteToken` |

**Rules**

- Both parts or neither. One without the other is "no invite".
- Validated on **every** hop that reads it: server (`InviteReference.TryParse`), verify page
  (`inviteFromQuery`), register / sign-in / onboarding (`inviteFromReturnUrl`). The regexes are the
  same on both sides.
- A failed validation is silent everywhere: registration proceeds, the link is built without the
  suffix, the button carries no `returnUrl`, the wizard shows the plain step.
- It is turned into a path in exactly two places, both of which compose the path themselves:
  `inviteReturnUrl(ref)` → `/join/{slug}/{token}?action=accept` (verify page → sign-in) and
  `invitePagePath(ref)` → `/join/{slug}/{token}` (wizard exit, R5).

## Client view-model (OnboardingComponent, all signals)

```text
carriedInvite : InviteRef | null                  -- from returnUrl, field initialiser
invitePreview : 'none' | 'loading'
              | { kind: 'usable'; preview: InvitePreview }
              | 'expired' | 'invalid'             -- from GET /invitations/{token}
addressedInvites : MyInvitation[]                 -- from GET /profiles/me/invitations, minus
                                                  --   any with teamSlug === carried team (R8)
memberSlugs   : Set<string>                       -- from MembershipService, loaded only when an
                                                  --   invitation is known (R6)
joinedSlugs   : string[]                          -- teams joined through the step, in order;
                                                  --   [0] is the exit destination (FR-016)
acceptingToken: string | null                     -- in-flight guard; never gates Continue
inviteError   : string | null                     -- quiet failure line (FR-022)
```

### State transitions (carried invite)

```text
none ──(carriedInvite set)──▶ loading ──preview.state=Usable──▶ usable
                                      ──preview.state=Expired─▶ expired
                                      ──Invalid | 404 | error─▶ invalid
usable ──accept 2xx──▶ usable + slug ∈ joinedSlugs   (card shows "You're on {team} now")
usable ──accept err──▶ usable + inviteError          (Accept stays pressable)
usable ──slug ∈ memberSlugs──▶ card shows "already on {team}", no Accept
```

`next()`, `back()`, Skip and "I'm not on a team yet" change none of the above and issue no request.

### Exit destination (R5)

```text
joinedSlugs[0]                                            → /t/{slug}
carried && usable && step !== 'welcome'                   → /join/{slug}/{token}      (no action)
carried && (expired | invalid)                            → /
otherwise                                                 → safeReturnUrl(returnUrl) ?? '/'
```
