---
description: "Task list for feature 051 — Team Logos"
---

# Tasks: Team Logos

**Input**: Design documents from `/specs/051-team-logos/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md),
[contracts/team-logo-api.md](./contracts/team-logo-api.md)

**Tests**: Included. Three facts are invisible in the diff and would rot silently — the
reconciliation sweep counting team logos (T017/T019), the admin-only write rule, and the
not-member-gated read rule.

**Organization**: grouped by user story so each can be implemented and verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable — different file, no dependency on an incomplete task
- **[Story]**: US1 (upload, P1) · US2 (display, P1) · US3 (chat, P2) · US4 (remove, P3)

## ⚠ Bounds — check every task against these

**ONE** entity · **ONE** migration · **THREE** endpoints · **NO** new NuGet or npm dependency ·
**NO** second storage path · **NO** polymorphic media table · **NO** retry/breaker/`AddJuggerHubResilience`
(Principle VII is not engaged — plan Summary) · **NO** party or event crest.
`TeamCardDto.LogoInitial` is **kept** — it is the fallback.

---

## Phase 1: Setup

- [X] T001 Confirm the branch and establish a working toolchain. Neither `dotnet` nor a compatible `node` is installed on this session's host, so both run in the images the repo pins (`mcr.microsoft.com/dotnet/sdk:10.0.401`, `node:26.8.2-alpine`); the backend suite needs the Docker socket mounted so Testcontainers can start Postgres and Azurite as siblings. The full compose stack was **not** brought up — it is not needed for either suite.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the storage seam every later task reads. Nothing displays until this exists.

- [X] T002 Add `MediaKind.TeamLogo` + the `"team-logos"` prefix in `backend/Services/Media/MediaObjectKey.cs`.
- [X] T003 Add the `TeamLogo` profile to `backend/Common/ImageProcessingOptions.cs` — `SquareCrop`, 512 px, quality 80, 512 KB ceiling. The comment must say **why** it is square-crop and not `Fit` like `Icon` (owner decision; six square tiles), so a later reader does not "fix" it.
- [X] T004 Create `backend/Entities/TeamLogo.cs` mirroring `ProfileAvatar`: `TeamId`, `ContentType`, `ObjectKey`, `SizeBytes`, `Team` navigation. Document that deleting the row does not delete the object.
- [X] T005 Wire EF in `backend/Data/AppDbContext.cs`: `DbSet<TeamLogo>`, `Team.Logo` 1:1 cascade, `ContentType` 64, `ObjectKey` `MediaObjectKey.MaxLength`, unique index on `TeamId` and on `ObjectKey`. No query filter (a team is never banned) — say so in a comment, since the three sibling tables all have one.
- [X] T006 ⚠ Add `TeamLogos` to the referenced-key set in `backend/Services/Media/MediaReconciliationService.cs`, with `IgnoreQueryFilters()` like its siblings. **Without this the sweep deletes every team logo one grace period after upload** (plan Summary; FR-020).
- [X] T007 Generate the migration `AddTeamLogos` (`dotnet ef migrations add AddTeamLogos`) and read the generated SQL: one `CREATE TABLE`, two unique indexes, one FK with `ON DELETE CASCADE`, nothing else.

**Checkpoint**: backend compiles; `MediaReconciliationTests` still green.

---

## Phase 3: User Story 1 — A team admin gives the team a logo (P1) 🎯 MVP

### Tests for User Story 1

- [X] T008 [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamLogoTests.cs`: admin upload → `204` and the bytes come back as `image/webp`; **non-admin member → `403`**; **non-member → `404`** (not 403 — no membership oracle); missing file → `400`; a non-image → `400` with the existing logo untouched.

### Implementation for User Story 1

- [X] T009 [US1] Create `backend/Services/Teams/ITeamLogoService.cs` + `TeamLogoService.cs`: `SetAsync`, `RemoveAsync`, `GetAsync`. Authorization through `TeamAccessGuard.ResolveAsync` exactly as `TeamService.UpdateSettingsAsync` does. Write ordering copied from feature 035: key → object → descriptor → delete superseded.
- [X] T010 [US1] Register the service in `backend/Program.cs` beside the other team services.
- [X] T011 [US1] Add `PUT /teams/{slug}/logo` and `GET /teams/{slug}/logo` to `backend/Controllers/TeamsController.cs` per the contract — `[RequestSizeLimit(8 MB)]` on the write, `[EnableRateLimiting(RateLimitPolicies.MediaRead)]` on the read, `MediaResponse.File(...)` for the body, 404 for every read refusal.
- [X] T012 [US1] Add `hasLogo` to `TeamDetailDto` + its projection in `TeamService`, then the frontend model + `TeamService.uploadLogo()` / `logoUrl()` with the per-slug revision map (`frontend/apps/web/src/app/core/services/team.service.ts`).
- [X] T013 [US1] Add the "Team logo" section to `frontend/apps/web/src/app/features/teams/team-settings/*` — admin-only, hidden file input behind a labelled button, current logo or letter placeholder beside it, inline error. Keys in **all three** catalogues in the same change or `catalog-parity.spec.ts` goes red.

**Checkpoint**: an admin can set a logo and see it on the settings page.

---

## Phase 4: User Story 2 — The logo appears wherever the team is pictured (P1)

- [X] T014 [P] [US2] `TeamPublicDetailDto.HasLogo` + projection; render it in the team-detail header tile, keeping the gradient letter as the `@else`.
- [X] T015 [P] [US2] `TeamCardDto.HasLogo` + `TeamSearchService` projection (`EXISTS`, inside the existing projection — SC-005); render in browse-teams rows **and** onboarding suggestions (same model, two templates).
- [X] T016 [P] [US2] `MyTeamDto.HasLogo` + `HomeService.MyTeamsQuery`; render in the "My team" rows.
- [X] T017 [US2] Extend `Teams/TeamLogoTests.cs`: a signed-in **non-member** can `GET` the logo (FR-012, the browse case); a **signed-out** caller gets `401`; a team with no logo gets `404`; and all four read models report `hasLogo`. The sweep guarantee (FR-020 / SC-007) lives with its siblings in `Media/MediaReconciliationTests.cs` as `Sweep_never_reclaims_a_team_logo`, next to the chat-attachment test it is copied from.

**Checkpoint**: 5 of 6 surfaces show logos; teams without one are unchanged.

---

## Phase 5: User Story 3 — The team chat is recognisable (P2)

- [X] T018 [US3] `ChatAvatarUrl.ForTeam(slug, hasLogo)`; project the team slug + logo presence in `ChatConversationService`'s inbox and detail projections; fill `BuildAvatar`'s `Team` and requester-side `TeamInquiry` branches. Replace the 019 comment that explains why the URL is null.
- [X] T019 [US3] Chat inbox template: the non-`Direct` branch renders `c.avatar.url` when present, falling back to the 2×2 cluster. The conversation **header needs no change** — verify that by reading it rather than editing it.
- [X] T020 [P] [US3] Test the chat facts in `backend/tests/.../Chat/ChatAvatarTests.cs`: a team conversation carries the logo URL in **both** the inbox row and the header when the team has one, and `null` when it does not; a manual group is untouched (FR-015); and a team inquiry shows the **team's logo to the asking player and the asking player's avatar to the admin** (FR-010) — two pictures of one conversation, which is the part a reader is most likely to think is a bug.

---

## Phase 6: User Story 4 — A logo can be taken down (P3)

- [X] T021 [US4] `DELETE /teams/{slug}/logo` → `TeamLogoService.RemoveAsync` (idempotent, deletes the object).
- [X] T022 [US4] `TeamService.DeleteAsync`: read the logo's key before the `ExecuteDeleteAsync`, delete the object after the row delete succeeds (FR-019).
- [X] T023 [US4] Frontend remove control + `TeamService.removeLogo()`, bumping the same revision so the placeholder appears immediately.
- [X] T024 [US4] Tests: remove → `204` and the read 404s; remove with no logo → `204`; non-admin remove → `403`; deleting a team removes its object.

---

## Phase 7: Polish & Cross-Cutting

- [X] T025 Run the UI review checklist in `checklists/ui-review.md` against the diff; DESIGN.md wins any conflict.
- [X] T026 Frontend specs: `core/services/team.service.spec.ts` (URL building, multipart upload, remove, and the per-slug cache bust incl. *not* busting on a failed upload) and `team-settings.component.spec.ts` (admin-only control, remove offered only when a logo exists, upload/remove round trips, refusal keeps the old logo). **A browse-row spec was NOT added** — browse-teams has no spec harness today (`BrowseList` + `toSignal` over query params), and the row is a one-line `@if (team.hasLogo)` over a flag the backend suite already asserts. Recorded rather than quietly dropped.
- [X] T027 Full verification: frontend lint + jest + production build; backend build + the Teams/Media/Chat suites.

---

## Dependencies & Execution Order

Phase 2 blocks everything. US1 (upload) blocks US2/US3/US4 — there is nothing to display or
remove until something can be stored. Within US2, T014/T015/T016 are independent files and
parallelizable. US4's T022 touches `TeamService`, which T012 also touches — sequence them.

## Verification status (2026-09-16)

`dotnet` and `node` are not installed on this session's host, so both toolchains were run in the
images the repo itself pins (`mcr.microsoft.com/dotnet/sdk:10.0.401`, `node:26.8.2-alpine`), with
the Docker socket mounted so Testcontainers could start its Postgres and Azurite siblings.

| Check | Result |
|---|---|
| `dotnet build JuggerHub.slnx` | succeeded, 0 warnings, 0 errors |
| `dotnet test JuggerHub.slnx` | **1080 passed, 0 failed** |
| `npm run lint` | passed (web + web-e2e) |
| `npx nx build web --configuration=production` | succeeded (pre-existing initial-bundle budget warning, unchanged) |
| `npx nx test web --watch=false` | **643 passed, 0 failed** (87 suites; was 630 / 85 before) |
| UI review checklist | run — `checklists/ui-review.md`, all items pass |

**Not run**: the Playwright e2e suite (it needs the full compose stack up) and any deployed-environment
check. The e2e specs do not reference the markup this feature changed — verified by searching them
for the affected test ids.

One detour worth recording: the first full backend run reported 3 failures in
`Terms.TermsVersionParityTests`, which walk up from the test binary to read
`frontend/apps/web/public/i18n/legal`. Only `backend/` had been mounted into the container, so the
directory genuinely was not there. Re-run with the repository root mounted, all three pass. Nothing
in this feature touches them.
