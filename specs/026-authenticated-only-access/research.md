# Research: Authenticated-Only Access with Opt-In Public Profiles

Phase 0 decisions for feature 026. Grounded in the current codebase (see file
references) and constitution Principle I (Security-First, NON-NEGOTIABLE).

## Current state (verified)

- **Auth scheme**: JwtBearer is the default authenticate/challenge scheme; the token
  is read from the `httpOnly` access-token cookie via `OnMessageReceived`
  (`backend/Program.cs:100-154`). 401s emit a generic ProblemDetails.
- **No global fallback policy today**: `AddAuthorization` only registers the
  `PlatformAdmin` policy (`backend/Program.cs:160-166`). Endpoints are anonymous
  unless a controller/action carries `[Authorize]`.
- **Controller-level `[Authorize]`**: `EventsController` and `TeamsController` have
  it (with per-action `[AllowAnonymous]` overrides). `ProfilesController` does
  **not** — its owner actions declare `[Authorize]` individually; `Browse` and the
  `{handle}*` reads are `[AllowAnonymous]`.
- **Anonymous read endpoints to close**: Teams `Browse`, `{slug}/public`; Events
  `Browse`, `{id}`, `{id}/participants`, `{id}/news`, `{id}/contacts`; Profiles
  `Browse`. (Anonymous **and staying so**: Auth flows, Health, RecognitionIcons
  icons, invite-preview endpoints, and the public-profile `{handle}*` reads.)
- **Optional-auth pattern already exists**: `TeamsController.GetOptionalUserId()`
  + `EventsController.GetOptionalUserId()` resolve the caller id when a cookie is
  present and pass it to the service for viewer-relative shaping. We reuse it.
- **Profile storage**: profile fields live on `PlayerProfile : BaseEntity`
  (`backend/Entities/PlayerProfile.cs`), 1:1 with `User`. Global query filter hides
  banned owners everywhere (`AppDbContext.cs:128/154/169/303`).

## Decision 1 — Secure-by-default authorization (global FallbackPolicy)

**Decision**: Register a global `FallbackPolicy` that requires an authenticated
user on the JwtBearer scheme, then remove `[AllowAnonymous]` from the team/event/
browse reads and keep it only on the intentionally-anonymous allowlist.

```csharp
// Program.cs, in AddAuthorization(...)
options.FallbackPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
    .RequireAuthenticatedUser()
    .Build();
```

**Rationale**:
- Principle I is NON-NEGOTIABLE and the owner is security-paramount: default-deny
  is the correct posture. Any endpoint added later is protected unless someone
  *explicitly* opts it anonymous — this kills the "forgot `[Authorize]`" bug class
  (OWASP A01, Broken Access Control) rather than patching endpoints individually.
- The fallback applies only where no other policy/attribute is present, so existing
  `[Authorize]` and `[AllowAnonymous]` endpoints are unaffected in behavior.

**Alternatives considered**:
- *Per-endpoint edits only* (add class-level `[Authorize]` to `ProfilesController`,
  strip the specific `[AllowAnonymous]`s): smaller blast radius, but leaves the
  codebase one forgotten attribute away from a leak. Rejected as the primary
  mechanism; the attribute edits are still done, but underwritten by the fallback.
- *Middleware that blanket-blocks anonymous*: duplicates what the authorization
  framework already does and fights the `[AllowAnonymous]` allowlist. Rejected.

**Required allowlist audit (must stay `[AllowAnonymous]`)**: Auth
(login/register/refresh/forgot/reset/verify/password-policy), Health,
RecognitionIcons (icon bytes only), invite previews
(`InvitationsController`, `EventInvitationsController`, `PartyInvitationsController`,
`MarketController` preview reads), and the public-profile `{handle}`, `{handle}/avatar`,
`{handle}/activity`. A test enumerates routes to prove nothing intended-anonymous
regressed and nothing intended-private stayed open.

## Decision 2 — Optional-auth + visibility gate on public-profile endpoints

**Decision**: Keep `[AllowAnonymous]` on `GET {handle}`, `{handle}/avatar`,
`{handle}/activity`; add `GetOptionalUserId()` to `ProfilesController` (same helper
as Teams/Events); pass `Guid? viewerUserId` into
`IProfileService.GetPublicAsync/GetProfileIdAsync/GetAvatarAsync`. The service
returns the profile iff `IsPublic || viewerUserId is not null`, else `null`.

**Rationale**: Mirrors the established viewer-aware public-read pattern, keeps the
decision in the service (Principle II), and lets an authenticated caller view any
profile while an anonymous caller sees only public ones.

**Alternatives considered**:
- *Two separate endpoints (anonymous public vs. authed any)*: more surface, and the
  frontend already hits one route. Rejected.
- *Authorize the whole controller and drop anonymous profile access*: would break
  the shareable-link requirement (FR-010). Rejected.

## Decision 3 — No existence oracle for private profiles

**Decision**: When the gate denies an anonymous caller, return the identical
`404 ProblemDetails` ("Profile not found") already used for an unknown handle — same
status, title, detail, and code path. Avatar returns the same `NotFound()`; activity
the same 404.

**Rationale**: FR-011 / SC-004 — a private profile must be indistinguishable from a
missing one. Reusing the exact existing not-found branch prevents wording/timing
tells.

## Decision 4 — `IsPublic` column + migration backfill

**Decision**: Add `bool IsPublic { get; set; } = false;` to `PlayerProfile`;
configure the column non-null with default `false`; generate an EF migration. New
rows default false at the app level; the migration's column default backfills all
existing rows to `false` (satisfying FR-017/FR-018).

**Rationale**: Simplest correct model — a boolean attribute of the existing profile
(no new entity). Aligns with data-model.md. Non-null avoids tri-state ambiguity.

**Alternatives considered**:
- *Enum `ProfileVisibility { Private, Public }`*: future-proofs for more states
  (e.g. "members-only") but none are required now; a bool is clearer and cheaper.
  Revisit only if a third state is specced.

## Decision 5 — Where the owner sets visibility

**Decision**: Carry `IsPublic` on `OwnerProfileDto` and accept it on
`UpdateProfileRequest`, persisted through the existing `UpdateAsync`. The frontend
renders a "Make my profile public" toggle in the owner profile/account settings.

**Rationale**: Reuses the existing owner-update path (one round-trip, one code path,
one authorization check — owner acts only on their own subject). A dedicated
`PUT /me/visibility` endpoint is unnecessary unless the UI review wants an instant
toggle separate from the edit form — noted as an option, not a requirement.

## Decision 6 — Test-churn scope

**Decision**: Update existing integration tests that assert anonymous access
succeeds (Events, Teams, Profile, Home, Search/browse) to assert `401` for the
anonymous caller and `200` for an authenticated one; add new tests for the
visibility gate (private→404 anonymous, public→200 anonymous, authed→200 either
way, no-oracle equality), the migration default, and a route-enumeration
allowlist test. Update frontend guard specs + e2e for the redirected routes.

**Rationale**: The behavior reversal is exactly what current tests pin; leaving them
green would mean they still assert the old anonymous exposure. Flipping them is part
of the feature, not incidental.

## Open items for planning/tasks

- Enumerate the precise existing tests to flip (done at `/speckit-tasks` granularity).
- Confirm during UI review whether the toggle lives on the profile-owner page or the
  account settings page (DESIGN.md is the tiebreaker).
