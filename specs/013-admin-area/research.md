# Research: Platform Admin Area (013)

All decisions below were made against the clarified spec (suspend = sign-in block
only; ban = soft-delete + re-registration block, reversible; admin designation purely
config-driven) and the constitution's security-first / never-trust-the-client rule.

## §1 Admin designation: seeded Identity role, mirrored from config

**Decision**: Use an ASP.NET Identity role named `PlatformAdmin`
(`AspNetRoles`/`AspNetUserRoles` via the already-wired `IdentityRole<Guid>` +
`AddEntityFrameworkStores`). A startup step (`PlatformAdminRoleSync`, run right after
migrations in `Program.cs`) mirrors role membership to `AdminOptions.NormalizedEmails`:

- ensure the role exists (create once),
- for each configured email whose account exists and lacks the role → add (log),
- for each configured email with no account → skip + log info (picked up next start),
- for each current role member whose email is *not* configured → remove (log),
- empty config → remove all members + log a prominent warning ("no platform
  administrators exist"), never throw.

Sync failure logs an error and does **not** crash the app: the authorization side
fails closed regardless (no role membership ⇒ no access), which is the safe direction.

**Rationale**: Issue #21 named a seeded Identity role as the primary candidate; the
store is already registered so this adds no new infrastructure; `RoleManager`/
`UserManager` give idempotent membership operations; and mirror semantics implement
the owner's "purely config-driven" decision exactly. Restart-to-apply matches the
spec's assumption.

**Alternatives considered**:
- *Custom `IsPlatformAdmin` bool on `User`* — equivalent power, but reinvents what
  Identity roles already provide and makes a future runtime grant/revoke surface
  (#21's someday-scope) harder.
- *Claims-based approach* — no advantage over a role for a single untired designation;
  roles are the conventional Identity idiom.
- *Keep allowlist as live check* — explicitly what #21 retires.

## §2 Enforcement: same policy, handler swaps to role membership (DB check)

**Decision**: Keep `PlatformAdminPolicy` / `PlatformAdminRequirement` and every
existing `[Authorize(Policy = PlatformAdminPolicy.Name)]` attribute untouched. Rewrite
only `PlatformAdminHandler` to succeed iff the authenticated user is currently in the
`PlatformAdmin` role, checked against the store per request
(`UserManager.IsInRoleAsync` on the user resolved from the JWT `sub`).

**Rationale**: This is precisely the "replace only the policy handler, leaving
controllers and behaviour untouched" migration that `AdminOptions`' docs and issue #21
promised. A per-request DB check means a role removal (config change + restart) takes
effect immediately for admin endpoints and can never be defeated by a stale token.
Admin traffic is tiny; one indexed lookup per admin request is negligible.

**Alternatives considered**:
- *Embed a role claim in the JWT and use `policy.RequireRole`* — faster (no DB hit)
  but grants ride on the token for up to `AccessTokenLifetimeMinutes` after removal,
  and privilege must never outlive its source. Rejected on the security-first
  principle; can be revisited if admin traffic ever matters.
- *Keep `IAuthorizationHandler` reading config directly* — that *is* the interim gate.

## §3 Account state: enum on the Identity user

**Decision**: Add `AccountStatus Status` (`Active = 0`, `Suspended`, `Banned`) and
`DateTime? StatusChangedAt` to `User` (`AspNetUsers`). Account state is
authentication/authorization data (like `EmailConfirmed`, `LockoutEnd`), not profile
content, so the feature-003 "keep Identity clean, profile data on `PlayerProfile`"
rule does not apply. Enforcement points:

- `AuthService.LoginAsync`: after a correct password (same enumeration-safe ordering
  as the verify gate) — `Suspended` → a distinct "account suspended" result/message
  (spec: clear, non-technical); `Banned` → generic `LoginResult.Failed()` (a banned
  account is "gone"; nothing is revealed).
- `AuthService.RefreshAsync`: after the user lookup — any non-`Active` status rejects,
  so surviving refresh tokens die at the next rotation.
- Suspend/ban service actions call `IRefreshTokenService.RevokeAllForUserAsync`
  immediately; live access tokens then expire within `Jwt.AccessTokenLifetimeMinutes`
  — exactly the spec's "normal session-refresh window".

**Rationale**: One source of truth on the row every auth flow already loads; no join
needed at the hottest checkpoints.

**Alternatives considered**: separate `AccountState` table (a join everywhere for
three states — no benefit); reusing Identity `LockoutEnd` (semantically wrong — it's
brute-force lockout with its own expiry, and conflating them breaks both features).

## §4 Ban invisibility: EF global query filter on PlayerProfile

**Decision**: Add a global query filter
`HasQueryFilter(p => p.User.Status != AccountStatus.Banned)` on `PlayerProfile`.
Player-facing reads (public profile `/u/{handle}`, player browse, team rosters,
participant lists, recognition display) all reach players through `PlayerProfile`, so
banned players drop out of every such surface **by default** — hidden-unless-opted-in,
which fails closed. Admin services opt out explicitly with `IgnoreQueryFilters()`
(scoped to the admin services only; the admin users list is the one place banned
players remain findable, per spec FR-012).

Implementation notes for the tasks phase:
- Audit every query that projects profile data through a navigation (e.g. roster →
  membership → profile): where a filtered required navigation would silently drop
  rows, that dropping is the *desired* behavior for banned users; add integration
  tests per surface (SC-005) rather than trusting the filter blindly.
- Suspended players are **not** filtered anywhere (spec: fully visible).
- The filter references `p.User`, adding a join to profile queries; profile reads are
  already keyed/indexed and low-volume, acceptable.

**Rationale**: Enumerating every public read site by hand fails open the day someone
adds a new surface and forgets the `Where`. A global filter inverts the default.

**Alternatives considered**: per-query `.Where(...)` on all public reads (fails open,
unmaintainable); a `Deleted` flag on `PlayerProfile` duplicated from `User` (two
sources of truth to keep in sync).

## §5 Re-registration block: the soft-deleted row is the denylist

**Decision**: No separate denylist entity. The banned `User` row is retained
(soft-delete) and `RequireUniqueEmail` + the existing enumeration-neutral registration
flow already refuse a second account for that email with a neutral "Accepted" response
(no oracle). One adjustment: the register path's "existing but unverified → resend
verification" courtesy branch must not fire for non-`Active` accounts (it would email
a banned/suspended user a verification link).

Unban = set `Status` back to `Active` (+ record) — profile, memberships, awards, and
content were never touched, satisfying FR-015/FR-018 restoration for free.

**Rationale**: The spec's denylist ("populated by bans, cleared by unbans") is exactly
the lifetime of the banned row; a second table would have to be kept in sync with it.

**Alternatives considered**: explicit `RegistrationDenylist` table — needed only if
ban-with-hard-delete ever arrives; noted as the extension point.

## §6 Action log: append-only AdminActionRecord

**Decision**: New entity `AdminActionRecord : BaseEntity` with `ActorUserId`,
`TargetUserId`, `Action` (enum `AdminAccountAction`: `Suspend`, `Reinstate`, `Ban`,
`Unban`, `PasswordResetSent`), and optional `Note` (unused by UI this pass). Written
in the same `SaveChanges` as the state change it records (atomicity). Read by no UI
this pass (spec: records kept, browsing UI later); badge/achievement grant attribution
continues to live on 012's `BadgeAward`/`AchievementAward` rows — not duplicated.

**Rationale**: FR-017 wants durable attribution; `BaseEntity` gives UUIDv7 +
`CreatedDate` via the audit interceptor for free. FKs point at `AspNetUsers` with
`DeleteBehavior.Restrict` so history cannot vanish.

## §7 Admin API + services

**Decision**: Two new thin controllers under the existing `PlatformAdmin` policy:

- `AdminOverviewController` → `GET api/v1/admin/overview` (four counts + new-players
  list + recent-grants list, one round trip).
- `AdminUsersController` → `GET api/v1/admin/users` (search `q` over display name /
  handle / team name, `status` filter, `PaginationRequest` → `PagedResult`),
  `GET api/v1/admin/users/{handle}` (identity, teams, pompfen, last-active +
  recent activity via the feature-003 participation data, status, admin marker),
  `POST api/v1/admin/users/{handle}/suspend | reinstate | ban | unban |
  reset-password` (idempotence + FR-019 admin-shield rules in the service).

Services `IAdminUserService` / `IAdminOverviewService` own all logic (queries use
`IgnoreQueryFilters` + `AsNoTracking` + projections; actions are tracked saves).
"Last active" derives from the newest event-participation activity (no new tracking,
per spec assumption); absent data renders as "—".

The reset-password action reuses the internals of `ForgotPasswordAsync` (generate
token → `SendPasswordResetEmailAsync`) for the *target* user; response is only
"sent". 012's `RecognitionAdminController` (`/admin/access` probe, subject awards),
badge/achievement grant/revoke endpoints, and DTOs are reused as-is by the frontend.

**Rationale**: Matches constitution II/III and the existing Controllers/Admin +
AdminControllerBase pattern from 012.

## §8 Frontend: admin shell with routed children; probe-based gating stays

**Decision**:
- Keep the existing `adminGuard` + `GET admin/access` probe (still the UX gate); move
  the probe call into a small `AdminService` (the recognition-admin service keeps its
  award/grant methods). The shell's top-nav and avatar-menu consult the same cached
  probe result to render the lock-marked Admin entry (desktop: after normal links;
  mobile: account-menu row, no fifth tab) — per wireframe 1a.
- Restructure `/admin` into a lazy-loaded `AdminShellComponent` (shield header,
  Overview/Users sidebar, "Back to app") with children: `''` → overview, `users` →
  list, `users/:handle` → detail, `catalogue` → the existing 012
  `AdminRecognitionComponent` (preserved, linked from the shell nav as a third item).
- The Assign picker on the user detail reuses 012's grant flow (catalogues +
  already-held marking via `players/{handle}/awards` + grant with note + revoke),
  rendered as a dialog/bottom-sheet per wireframe 1e.
- DESIGN.md drives all styling; the ui-review checklist is instantiated and run as
  quality gate 7.

**Rationale**: Smallest delta that matches the wireframes; no duplicated grant logic;
012 surface stays reachable (spec assumption).

## §9 Testing strategy

**Decision**: Integration tests (existing `JuggerHubApiFactory` + Testcontainers
pattern) cover: role sync (grant/revoke/skip/empty-config), authz swap (allowlisted
email without role is refused — SC-001; existing `AdminAuthorizationTests` updated),
account actions (suspend/ban/reinstate/unban + shield rules + records), login/refresh
refusal per status, registration block for banned email (neutral response), ban
invisibility per public surface (profile, browse, roster, participants), overview
stats, and users search/filter/pagination. Frontend: Playwright e2e for the gated
entry, overview→users→detail→suspend flow, and the Assign picker (pattern:
`recognition.spec.ts`); dev seed extended with an admin + suspended/banned samples so
e2e has stable fixtures.
