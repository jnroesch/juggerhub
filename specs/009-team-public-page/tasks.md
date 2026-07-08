---
description: "Tasks for feature 009 — public team page & request to join"
---

# Tasks: Public team page & request to join

**Prerequisites**: spec.md, plan.md. Stacked on the 008 branch.

## Phase 1: Backend — join-request domain

- [ ] T001 Add `enum JoinRequestStatus { Pending, Approved, Declined }` to `backend/Entities/TeamEnums.cs`
- [ ] T002 Create `backend/Entities/TeamJoinRequest.cs` (BaseEntity: TeamId, UserId, Status, DecidedByUserId?, DecidedDate?, nav Team/User/DecidedBy)
- [ ] T003 Add `DbSet<TeamJoinRequest>` + config in `backend/Data/AppDbContext.cs` — FKs; partial unique index `(TeamId, UserId) WHERE Status = 0` (one pending per player+team); index `(TeamId, Status)`
- [ ] T004 Generate migration `AddTeamJoinRequests`

## Phase 2: Backend — public composite read

- [ ] T005 Add DTOs to `backend/Dtos/Teams/TeamDtos.cs`: `TeamViewerRelation` enum (Anonymous/NonMember/Requested/Member/Admin), `TeamPublicDetailDto` (overview + relation + memberCount + isActive + beginnersWelcome + capped roster/activity/trainings), `PublicMemberDto` (handle, displayName, position, role — no contact), `TrainingDto` (eventId, name, startsAt, locationLabel)
- [ ] T006 Extend `TeamActivityService` with a public (un-gated) recent-activity read; extend `ITeamService`/`TeamService` with `GetPublicDetailAsync(slug, viewerUserId?)` composing overview + viewer relation + join status + capped roster (public fields) + activity + upcoming team-mode trainings
- [ ] T007 Rework `TeamsController` `GET {slug}/public` → return `TeamPublicDetailDto` using optional auth (`GetOptionalUserId`)

## Phase 3: Backend — join-request workflow

- [ ] T008 Create `ITeamJoinRequestService` + `TeamJoinRequestService` — `RequestAsync` (signed-in non-member; idempotent pending), `ListPendingAsync` (admin, paginated), `ApproveAsync` (admin → create membership, mark approved, idempotent if already member), `DeclineAsync` (admin)
- [ ] T009 Register `ITeamJoinRequestService` in `backend/Program.cs`
- [ ] T010 Add controller endpoints: `POST {slug}/join-requests`, `GET {slug}/join-requests`, `POST {slug}/join-requests/{id}/approve`, `POST {slug}/join-requests/{id}/decline` (thin; admin-guarded; ProblemDetails)
- [ ] T011 Backend tests `tests/…/Teams/JoinRequestTests.cs` + `PublicTeamTests.cs` — public visibility + viewer relation (anon/non-member/member/admin), no news/contact leak, request idempotency + duplicate block, approve→membership, decline, non-admin 403, pagination

## Phase 4: Frontend — public team page

- [ ] T012 `app.routes.ts` — remove `authGuard` from `t/:slug` (public)
- [ ] T013 Extend `core/models/team.models.ts` + `core/services/team.service.ts` — public detail, viewer relation, join-request models + `getPublicDetail`, `requestToJoin`, `listJoinRequests`, `approveJoinRequest`, `declineJoinRequest`
- [ ] T014 Rework `features/teams/team-detail` — load the public composite; render overview + public roster + activity + trainings for all; request-to-join button (state-aware: sign-in prompt / request / requested / none); members: news + contact; admins: the request queue + existing tools
- [ ] T015 Add the admin `join-requests` sub-component (list + approve/decline)

## Phase 5: Verify

- [ ] T016 Backend `dotnet test`; frontend `nx build` + service spec
- [ ] T017 Docker deploy + drive (anon view, non-member request, admin approve) with screenshots; commit milestones
- [ ] T018 Note spec drift in `specs/005-team-space` (roster now public)
