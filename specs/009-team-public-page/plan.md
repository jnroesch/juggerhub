# Implementation Plan: Public team page & request to join

**Branch**: `008-home-dashboard-nav` (stacked) | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

## Summary

Make `/t/:slug` a public team page and add a request-to-join workflow. Backend adds a new **`TeamJoinRequest`** domain (entity + enum + migration + service + endpoints) and a **public team composite read** (`GET /teams/{slug}/public`) that returns the overview + the **viewer relation** (anonymous / non-member / requested / member / admin) + capped public roster, activity, and upcoming trainings. Join-request endpoints let a signed-in non-member request, and team admins list/approve/decline (approval creates the membership via the existing membership path). Frontend makes `/t/:slug` anonymous (removes the guard) and reworks the team page to render viewer-scoped sections: public overview/roster/activity/trainings for everyone, news/contact for members, and the admin request queue + tools for admins, with a state-aware request-to-join button. Every visibility decision is server-side (never trust the client); public reads carry public fields only.

## Technical Context

Same stack as 008: .NET 10 / EF Core / PostgreSQL backend; Angular (signals) frontend. Reuses `TeamMembershipGuard`, `TeamActivityService`, `PaginationRequest`/`PagedResult<T>`, Mapster, the events model (team-mode `EventSignup` = trainings), and the `GetOptionalUserId` optional-auth pattern from `EventsController`. **One new entity + one migration** (`AddTeamJoinRequests`). Frontend keeps `.html/.css/.ts` separate; styled from DESIGN.md.

## Constitution Check

| # | Principle | Compliance | Verdict |
|---|-----------|-----------|---------|
| I | Security-First | Viewer relation + every section's visibility decided **server-side** from the session; public DTOs carry no contact/news/admin data. Request creation authorizes "signed-in non-member"; approve/decline require **team admin** (`TeamMembershipGuard`), non-admin → 403. Approval creates exactly one membership (idempotent). OWASP A01 (access control) is the core control. | ✅ |
| II | Thin controllers | New `[HttpGet]`/`[HttpPost]` actions on `TeamsController` bind slug + caller + `PaginationRequest`, delegate to `ITeamJoinRequestService` and the public read on `ITeamService`. DTOs via projection/Mapster. | ✅ |
| III | Disciplined data access | Reads are `.Select` + `AsNoTracking`; composite modules capped; the request queue paginated with a stable `Id` tiebreak. New entity derives from `BaseEntity`; a **partial unique index** enforces one pending request per (team, user). Approval is a tracked save (interceptor sets audit fields). | ✅ |
| IV | Auth/session | Reuses JWT-in-cookie; public reads are `[AllowAnonymous]` with optional auth to compute the viewer relation; writes require the bearer scheme. | ✅ |
| V | Env parity | One auto-applied migration; no new services/secrets. | ✅ |
| VI | Conventions | `.ts/.html/.css` separate; `.ps1` only; enums serialize as names; DESIGN.md tokens. | ✅ |

**Result**: PASS. Accepted change: widens feature 005's public/internal split (roster becomes public — names + positions only), recorded as spec drift in 005.

## Project structure (delta)

```text
backend/
├── Entities/TeamJoinRequest.cs                 # NEW — TeamId, UserId, Status, DecidedByUserId?, DecidedDate?
├── Entities/TeamEnums.cs                        # EXTEND — enum JoinRequestStatus { Pending, Approved, Declined }
├── Dtos/Teams/TeamDtos.cs                       # EXTEND — TeamPublicDetailDto, PublicMemberDto, TrainingDto, JoinRequestDto, TeamViewerRelation
├── Services/Teams/
│   ├── ITeamService.cs / TeamService.cs         # EXTEND — GetPublicDetailAsync(slug, viewerUserId?) composite (+ viewer relation)
│   ├── ITeamJoinRequestService.cs / …           # NEW — Request / ListPending / Approve / Decline (admin-guarded)
│   └── TeamActivityService.cs                    # EXTEND — public activity read (no member gate) for the public page
├── Data/AppDbContext.cs                          # EXTEND — TeamJoinRequest config + partial unique index (TeamId,UserId) WHERE Status=Pending
├── Data/Migrations/                              # NEW — AddTeamJoinRequests
├── Controllers/TeamsController.cs                # EXTEND — GET {slug}/public (composite); join-request endpoints
├── Program.cs                                    # EXTEND — register ITeamJoinRequestService
└── tests/…/Teams/                                # NEW — JoinRequestTests, PublicTeamTests (visibility + entitlement)

frontend/apps/web/src/app/
├── app.routes.ts                                 # EXTEND — t/:slug: remove authGuard (public)
├── core/services/team.service.ts                 # EXTEND — getPublicDetail, requestToJoin, listJoinRequests, approve/decline
├── core/models/team.models.ts                    # EXTEND — public detail, viewer relation, join-request models
└── features/teams/team-detail/                    # REWORK — public overview/roster/activity/trainings + request button;
    (+ components/join-requests.component)          #          members: news/contact; admins: request queue + tools
```

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/teams/{slug}/public` | anonymous (optional auth) | Public composite: overview + viewer relation + join status + capped roster/activity/trainings |
| POST | `/teams/{slug}/join-requests` | signed-in non-member | Create/confirm a pending request (idempotent) |
| GET | `/teams/{slug}/join-requests` | team admin | Paginated pending requests |
| POST | `/teams/{slug}/join-requests/{id}/approve` | team admin | Approve → create membership |
| POST | `/teams/{slug}/join-requests/{id}/decline` | team admin | Decline |

Existing member endpoints (`/members`, `/activity`, `/news`, settings, invitations) stay unchanged for the internal sections.

## Testing

Backend integration (Testcontainers): public composite shows overview/roster/activity/trainings + correct viewer relation for anon/non-member/member/admin; **no news/contact in public payload**; request creates one pending (idempotent on re-request); duplicate blocked; approve creates membership + resolves; decline leaves membership; **non-admin 403** on queue/approve/decline; member/anonymous cannot request when inapplicable; pagination on the queue. Frontend: build + a service spec (request/approve/decline calls) + the team page renders the viewer-scoped sections.

## Verification

Backend `dotnet test`; frontend `nx build`/`nx test`; Docker deploy + drive: anon views a team (no bounce), non-member requests (button → Requested), admin approves (requester becomes member) — with screenshots.
