# Implementation Plan: Badges & Achievements

**Branch**: `012-badges-achievements` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-badges-achievements/spec.md`

## Summary

Add two **separate** admin-curated recognition systems — **Badges** (status / membership / milestone) and **Achievements** (competitive accomplishment, optionally carrying a year/competition context) — that a platform administrator can define and **manually** grant to a **player** or a **team**. Earned badges and achievements render on the player profile (filling the existing stub) and the team page, grouped and with empty states. All admin operations are gated **server-side** by a `PlatformAdmin` authorization policy whose v1 implementation is a **configuration-driven allowlist** (no platform admin role exists yet; real role tracked in GitHub #21). Automatic, criteria-based awarding is **out of scope for v1** but the model reserves an award `Source` so it can be added later without reworking earned history.

**Technical approach**: mirror established project patterns — `BaseEntity` (UUIDv7) entities, thin controllers over DI'd services behind interfaces, no repository layer, Mapster entity→DTO, mandatory pagination, per-blob icon storage like `ProfileAvatar`, and the polymorphic `PlayerProfileId?`/`TeamId?` + DB CHECK subject pattern like `EventSignup`. Badges and achievements are two parallel entity pairs (definition + award) rather than one abstraction, honoring the "two separate systems" decision.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (frontend, Nx workspace)

**Primary Dependencies**: Entity Framework Core + Npgsql, Microsoft Identity, Mapster, Asp.Versioning; Angular + Nx + Tailwind CSS

**Storage**: PostgreSQL 18. New tables for badge/achievement definitions, their icons (bytea blobs, separate tables), and awards. EF Core migrations auto-applied on startup.

**Testing**: xUnit + `WebApplicationFactory` + Testcontainers.PostgreSql (backend integration); Jest (frontend unit); Playwright desktop+mobile (e2e)

**Target Platform**: Containerized web app (backend API + Angular SPA behind nginx), local via docker-compose, deployed to Azure App Services

**Project Type**: Web application (backend + frontend)

**Performance Goals**: Standard web app; admin operations are low-volume; public read of a subject's awards is a small bounded list embedded in the existing profile/team page fetch (no extra round trip on page load)

**Constraints**: Constitution NON-NEGOTIABLE security-first — every authZ decision server-side; no secrets/stack traces to client; awards shown on public pages expose only name/description/icon/earned-date/context. Admin gate must be swappable for a real role (#21) without behavior change.

**Scale/Scope**: Small catalogs (tens–hundreds of definitions), a handful of awards per subject typically. Two admin resource families + read integration into two existing pages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance |
|---|---|
| **I. Security-first, never trust the client** | ✅ All define/grant/revoke gated by a server-side `PlatformAdmin` policy; public reads expose only public fields; no raw exceptions (existing `ExceptionHandlingMiddleware`). The temporary config gate is enforced server-side, not client-side. |
| **II. Thin controllers, service-centric** | ✅ New controllers delegate to `IBadgeService` / `IAchievementService` (interfaces, DI). No repository layer. DTOs via Mapster; services return entities. |
| **III. Disciplined data access** | ✅ New entities derive from `BaseEntity` (UUIDv7); audit fields via interceptor; reads use projections + `AsNoTracking`; **admin catalog + award lists paginated** (`PaginationRequest`/`PagedResult`); revoke uses targeted update setting `ModifiedDate` where it bypasses the tracker. |
| **IV. Auth & sessions** | ✅ Reuses existing JWT-cookie auth; adds an authorization **policy** only. No token storage changes. |
| **V. Environment parity & containers** | ✅ No new services; migrations auto-apply; admin allowlist flows through `.env` (local) and GitHub Environments (deployed), consistent with existing config/secrets handling. |
| **VI. Conventions & tooling** | ✅ Frontend keeps separate `.html`/`.css`/`.ts`; UI follows DESIGN.md; only `.ps1` scripts if any. |

**Result**: PASS — no violations. No entries required in Complexity Tracking.

**Design note on the temporary admin gate**: it is expressed as a named policy `PlatformAdmin`. v1 backs it with a config allowlist handler; issue #21 later swaps the handler/requirement for a real role **without** touching controllers or badge/achievement behavior. This keeps the interim measure isolated and honestly server-side.

## Project Structure

### Documentation (this feature)

```text
specs/012-badges-achievements/
├── plan.md              # This file
├── research.md          # Phase 0 output — key design decisions
├── data-model.md        # Phase 1 output — entities, relationships, indexes, migration
├── quickstart.md        # Phase 1 output — end-to-end validation guide
├── contracts/
│   └── openapi.yaml      # Phase 1 output — admin + read API surface
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Common/
│   ├── AdminOptions.cs                 # NEW — config allowlist (Section "Admin")
│   └── MappingConfig.cs                # extend — entity→DTO maps
├── Security/
│   └── PlatformAdmin/                  # NEW — authorization policy (temporary config gate)
│       ├── PlatformAdminRequirement.cs
│       └── PlatformAdminHandler.cs
├── Entities/
│   ├── RecognitionEnums.cs             # NEW — AwardSource, AwardStatus, SubjectType
│   ├── BadgeDefinition.cs              # NEW
│   ├── BadgeIcon.cs                    # NEW — 1:1 blob (like ProfileAvatar)
│   ├── BadgeAward.cs                   # NEW — polymorphic subject
│   ├── AchievementDefinition.cs        # NEW
│   ├── AchievementIcon.cs              # NEW
│   └── AchievementAward.cs             # NEW
├── Data/
│   ├── AppDbContext.cs                 # extend — DbSets + OnModelCreating config
│   └── Migrations/                     # NEW — AddBadgesAndAchievements
├── Dtos/
│   ├── Badges/                         # NEW — request/response DTOs
│   └── Achievements/                   # NEW
├── Services/
│   ├── Badges/{IBadgeService,BadgeService}.cs            # NEW
│   └── Achievements/{IAchievementService,AchievementService}.cs  # NEW
├── Controllers/
│   ├── Admin/BadgesAdminController.cs          # NEW — [Authorize(Policy=PlatformAdmin)]
│   ├── Admin/AchievementsAdminController.cs    # NEW
│   └── (extend) ProfilesController / TeamsController read paths OR new read endpoints
└── Program.cs                          # extend — bind AdminOptions, register policy + services

frontend/apps/web/src/app/
├── core/
│   ├── models/recognition.models.ts    # NEW — badge/achievement interfaces
│   └── services/recognition.service.ts # NEW — read + admin calls
├── features/
│   ├── profile/                        # extend — replace badges stub with real display
│   ├── teams/team-detail/              # extend — add badges/achievements display
│   └── admin/                          # NEW — admin management screens (badges, achievements)
└── (routes)                            # extend — guarded admin routes
```

**Structure Decision**: Web-application layout (existing `backend/` + `frontend/`). Backend follows the layered API convention (Entities → Data → Services (interface + impl) → Dtos → Controllers) already used by every prior feature. Badges and achievements are two parallel families to honor the "separate systems" decision; shared enums live in one `RecognitionEnums.cs`. Frontend adds a new `admin/` feature area plus display integration into the existing profile and team-detail features.

## Complexity Tracking

No constitution violations — table intentionally empty.
