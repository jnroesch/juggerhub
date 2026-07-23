# Implementation Plan: Authenticated-Only Access with Opt-In Public Profiles

**Branch**: `main` (feature dir `026-authenticated-only-access`) | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/026-authenticated-only-access/spec.md`

## Summary

Reverse the "anonymous by design" model (features 006/007/009): gate all profile,
team, event, and search/browse access behind authentication, with a single
exception — a profile whose owner has explicitly opted it **public** is viewable
anonymously via its direct `/u/{handle}` link (card + team memberships + activity).
Default is **private**; existing profiles are migrated to private.

Technical approach, two mechanisms:

1. **Secure-by-default authorization.** Add a global `FallbackPolicy` requiring an
   authenticated user (JwtBearer-in-cookie scheme) so any endpoint lacking an
   explicit `[AllowAnonymous]` is protected. Then remove the `[AllowAnonymous]`
   attributes from team/event/browse read endpoints, and audit the remaining
   `[AllowAnonymous]` endpoints so only the intentionally-anonymous set (auth,
   health, recognition icons, invite previews, public profile) keeps it. This
   closes the whole "forgot to authorize" class (OWASP A01) rather than fixing
   endpoints one at a time.
2. **Owner-controlled profile visibility.** Add an `IsPublic` flag to
   `PlayerProfile` (default `false`, EF migration backfills existing rows to
   `false`). The three anonymous public-profile endpoints become **optional-auth**:
   they resolve the caller's id if a cookie is present and pass it to the service,
   which returns the profile only when `IsPublic` **or** the caller is
   authenticated — otherwise the same `null`→404 as a missing handle (no existence
   oracle). The owner toggles the flag from profile/account settings; the server is
   the boundary.

Frontend: add `authGuard` to `t/:slug`, `events/:id`, and the three `browse/*`
routes; keep `u/:handle` anonymous (the backend 404 drives its not-found state);
add a "Make my profile public" toggle to the owner profile/account settings.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular 21 (zoneless), Nx (frontend)

**Primary Dependencies**: ASP.NET Core MVC + `Microsoft.AspNetCore.Authorization`, EF Core (PostgreSQL 18), Mapster; Angular Router, Tailwind CSS

**Storage**: PostgreSQL — one new non-null `boolean` column `IsPublic` on `PlayerProfiles`, default `false`

**Testing**: xUnit integration tests (`JuggerHub.Api.IntegrationTests`, WebApplicationFactory + Testcontainers-style Postgres); Jest (frontend, zoneless — no `fakeAsync`); Playwright e2e (`web-e2e`)

**Target Platform**: Linux containers on AKS (Dev/Prod); docker-compose (local)

**Project Type**: Web application (ASP.NET Core API + Angular SPA)

**Performance Goals**: No new hot paths; the `IsPublic` check is a column already loaded in the profile projection. No added round-trips.

**Constraints**: Never-trust-the-client (Principle I); no existence oracle for private profiles; DTOs must stay free of email/account/security data; `.html`/`.css`/`.ts` kept separate; `.ps1`-only scripts.

**Scale/Scope**: ~5 backend controllers touched (Teams, Events, Profiles, + audit of the rest), 1 entity + 1 migration, 2 DTOs, 1 service interface change (3 methods gain a viewer param); ~5 frontend routes + 1 settings toggle. Substantial **existing-test churn** (anonymous-access assertions must flip to 401 + authenticated 200).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Security-First, Never Trust the Client | ✅ Strengthened | This feature tightens access control and adds secure-by-default authorization. Visibility enforced server-side; private-vs-missing indistinguishable; no secrets in public DTO. **No constitution amendment needed** — Principle I never mandated anonymous browse; the "anonymous by design" language lives in feature specs 006/007/009 and code comments, which this feature supersedes. |
| II. Thin Controllers, Service-Centric | ✅ | Visibility decision lives in `IProfileService` (viewer-aware); controllers only pass the optional caller id and shape responses. No new controller logic beyond the existing optional-auth helper. |
| III. Disciplined Data Access | ✅ | New column via EF migration; profile reads already use projections + `AsNoTracking`. No unbounded lists added. |
| IV. Secure Auth & Session | ✅ | Reuses the JwtBearer-in-cookie scheme and existing `authGuard`; no token storage change. Fallback policy uses the same scheme. |
| V. Environment Parity | ✅ | Behavior identical across local/Dev/Prod; migration ships with the image. |
| VI. Conventions & Tooling | ✅ | Angular files stay separate; any scripts are `.ps1`. |
| Gate 7. UI/Design compliance | ⚠️ Required | The settings toggle and any anonymous not-found/sign-in states must pass the UI review checklist against DESIGN.md before verification. Instantiate `specs/026-authenticated-only-access/checklists/ui-review.md`. |

**Gate result: PASS.** No violations; Complexity Tracking not required.

**Spec-drift reconciliation (documentation task, not a code gate):** annotate
feature specs **006** (anonymous event reads), **007** (anonymous search/browse),
and **009** (anonymous team public page) as superseded by 026, and update the
code comments that cite "anonymous by design" (e.g. `ProfilesController`,
`TeamsController`, `EventsController` XML docs, and `RecognitionIconsController`'s
"shown on public profile and team pages" rationale). No constitution version bump.

## Project Structure

### Documentation (this feature)

```text
specs/026-authenticated-only-access/
├── plan.md              # This file
├── spec.md              # Feature spec
├── research.md          # Phase 0 — decisions (fallback policy, optional-auth, migration)
├── data-model.md        # Phase 1 — PlayerProfile.IsPublic + DTO deltas
├── quickstart.md        # Phase 1 — how to validate end-to-end
├── contracts/           # Phase 1 — endpoint authorization + visibility contract
│   └── access-and-visibility.md
├── checklists/
│   ├── requirements.md   # (from /speckit-specify)
│   └── ui-review.md      # (instantiate during UI work)
└── tasks.md             # (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Program.cs                              # + FallbackPolicy (RequireAuthenticatedUser, JwtBearer)
├── Entities/PlayerProfile.cs               # + IsPublic (bool, default false)
├── Data/
│   ├── AppDbContext.cs                     # column config (default false); global ban filter unchanged
│   └── Migrations/*_AddProfileVisibility.cs # new migration (backfill existing → false)
├── Dtos/Profile/ProfileDtos.cs             # OwnerProfileDto + IsPublic; UpdateProfileRequest + IsPublic
├── Services/Profile/
│   ├── IProfileService.cs                  # GetPublicAsync/GetProfileIdAsync/GetAvatarAsync gain Guid? viewerUserId
│   └── ProfileService.cs                   # visibility gate + persist IsPublic on update
├── Controllers/
│   ├── ProfilesController.cs               # class-level [Authorize]; Browse loses [AllowAnonymous];
│   │                                       #   {handle}* pass optional caller id; add GetOptionalUserId()
│   ├── TeamsController.cs                  # remove [AllowAnonymous] on Browse + {slug}/public
│   ├── EventsController.cs                 # remove [AllowAnonymous] on Browse/detail/participants/news/contacts
│   └── (audit remaining controllers)       # confirm intentional [AllowAnonymous] set only
└── tests/JuggerHub.Api.IntegrationTests/   # flip anonymous-access assertions; add visibility + fallback tests

frontend/apps/web/src/app/
├── app.routes.ts                           # authGuard on t/:slug, events/:id, browse/{teams,events,players}
├── features/profile/profile-owner/ (or account/) # "Make my profile public" toggle (.html/.css/.ts)
├── core/services/profile.service.ts        # carry IsPublic on owner DTO + update payload
└── features/profile/profile-public/         # relies on 404 for private → existing not-found UX
```

**Structure Decision**: Existing ASP.NET Core API + Angular SPA layout (constitution
Technology Stack). No new projects or modules; changes are edits to existing files
plus one EF migration and one Angular settings control.

## Phase 0 — Research

See [research.md](./research.md). Key decisions:

- **Global `FallbackPolicy` (secure-by-default) vs. per-endpoint `[Authorize]`** →
  chose fallback policy + explicit `[AllowAnonymous]` allowlist. Rationale: Principle I
  (NON-NEGOTIABLE), closes A01 for future endpoints too.
- **Optional-auth on public-profile endpoints** → mirror the existing
  `TeamsController.GetPublic` / `EventsController.GetDetail` pattern
  (`GetOptionalUserId()` + service-side decision), keeping `[AllowAnonymous]`.
- **No existence oracle** → private-to-anonymous returns the exact same
  `null`→404 ProblemDetails as a missing handle.
- **Migration backfill** → non-null `IsPublic` default `false`; existing rows → `false`.
- **Visibility surface** → single `IsPublic` field in the owner update (no separate
  endpoint) unless UI review prefers a dedicated toggle call.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — `PlayerProfile.IsPublic`, DTO deltas, migration note.
- [contracts/access-and-visibility.md](./contracts/access-and-visibility.md) —
  per-endpoint authorization matrix (before/after) and the visibility decision table.
- [quickstart.md](./quickstart.md) — end-to-end validation (anonymous denied,
  authenticated allowed, public-profile opt-in round-trip, no-oracle check).

## Complexity Tracking

No constitution violations — section intentionally empty.
