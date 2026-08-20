---

description: "Task list for 046 — Showcase image galleries for player and team profiles"
---

# Tasks: Showcase Image Galleries for Player and Team Profiles

**Input**: Design documents from `/specs/046-showcase-galleries/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/showcase-endpoints.md](./contracts/showcase-endpoints.md),
[quickstart.md](./quickstart.md)

**Tests**: Included. Three of the five user stories (US2, US4, US5) assert behaviour that a passing
screen cannot demonstrate — a non-admin being refused, an anonymous caller being refused, a rejected
upload changing nothing — and the constitution's security gate requires them to be enforced
server-side. Test tasks are therefore first-class here, not optional.

**Organization**: Grouped by user story. Each story phase is independently implementable and
testable. Foundational work (Phase 2) blocks every story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5, mapping to the user stories in [spec.md](./spec.md)

## Path Conventions

Web app monorepo: backend at `backend/`, frontend at `frontend/apps/web/src/app/`. Paths below are
repository-relative and exact.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: extend the three existing seams (processing profile, media kind, rate limiting) that the
galleries plug into. Nothing here changes existing behaviour.

- [X] T001 [P] Add the `Showcase` processing profile to `backend/Common/ImageProcessingOptions.cs` — `ResizeMode = Fit`, `MaxDimension = 1280`, `Quality = 80`, `MaxOutputBytes = 1 MB` — with an XML doc saying why it is `Fit` and not the avatar's `SquareCrop` (research R8). Do not touch `MaxInputBytes`, `MaxDecodePixels`, or `AllowedContentTypes`.
- [X] T002 [P] Add `ProfileShowcase` and `TeamShowcase` to `MediaKind` and their prefixes (`profile-showcase`, `team-showcase`) to `MediaObjectKey.Prefix` in `backend/Services/Media/MediaObjectKey.cs`. Leave the UUIDv4 key generation exactly as it is — the file explains why it is not UUIDv7.
- [X] T003 [P] Add the `MediaUpload` rate-limit policy to `backend/Security/RateLimitPolicies.cs` — 20/min via `PartitionByUser` (not `PartitionByCaller`: every upload path requires a session) — with a comment deriving the number from a member filling a five-slot gallery, per research R12.
- [X] T004 [P] Add unit coverage for the new processing profile in `backend/tests/JuggerHub.Api.IntegrationTests/Media/ImageProcessorTests.cs`: a 3000×1000 panorama processed with `Showcase` comes back ≤1280 px on its longest side, **aspect ratio preserved** (not squared), never upscaled, and ≤1 MB.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: no user story work can begin until this phase is complete. T009 in particular is a
data-loss guard — see research R6.

- [X] T005 [P] Create `backend/Entities/ProfileShowcaseImage.cs` per [data-model.md](./data-model.md): `ProfileId`, `Profile`, `Position`, `Caption?`, `ContentType`, `ObjectKey`, `SizeBytes`. XML doc must carry `ProfileAvatar`'s two warnings — the object key never leaves the backend, and deleting this row does **not** delete the object.
- [X] T006 [P] Create `backend/Entities/TeamShowcaseImage.cs` — identical shape keyed on `TeamId`/`Team`, with an XML doc stating the two deliberate differences: **no** ban query filter, and **no** uploader column (research R3, R4).
- [X] T007 Add `ShowcaseImages` navigations to `backend/Entities/PlayerProfile.cs` and `backend/Entities/Team.cs`; add both `DbSet`s and both entity configurations to `backend/Data/AppDbContext.cs` — ban query filter on the profile entity only, `HasMaxLength` per the data model, cascade FKs, non-unique `(OwnerId, Position)` index, unique `ObjectKey` index. Comment why `(OwnerId, Position)` is **not** unique (reorder updates row-by-row; the lock is the guarantee).
- [X] T008 Create the migration `AddShowcaseGalleries` in `backend/Data/Migrations/` (`dotnet ef migrations add`). Verify it creates two tables and **alters none** — if the diff touches an existing table, stop and re-read the data model.
- [X] T009 **⚠ Add both new tables to the referenced-key set in `backend/Services/Media/MediaReconciliationService.cs`**, each with `IgnoreQueryFilters()`, alongside the existing three loops. Without this the next operator sweep deletes every showcase object in the environment; without `IgnoreQueryFilters()` it deletes a banned member's live objects (research R6).
- [X] T010 [P] Add the regression test for T009 in `backend/tests/JuggerHub.Api.IntegrationTests/Media/MediaReconciliationTests.cs`: store profile and team showcase images (one owner banned), sweep with a zero grace period, assert **every** object survives; then delete one descriptor row directly and assert that object *is* reclaimed.
- [X] T011 Create `backend/Services/Media/ShowcaseWriter.cs` — the owner-agnostic write core both surfaces use: `AddAsync`, `RemoveAsync`, `ReorderAsync`, each running `db.Database.CreateExecutionStrategy()` → `ChangeTracker.Clear()` → `BeginTransactionAsync` → `SELECT 1 FROM "<owner table>" WHERE "Id" = {id} FOR UPDATE` → count/mutate → compact positions → commit. Copy the shape of `TeamService.MutateMembershipAsync` (`backend/Services/Teams/TeamService.cs:344-393`), including its comment about why the change tracker must be cleared before a replay. All state mutation stays **inside** the delegate (Principle VII).
- [X] T012 [P] Create `backend/Dtos/Profile/ShowcaseDtos.cs`: `ShowcaseImageDto(Guid Id, string? Caption, int Position)`, `UpdateShowcaseCaptionRequest(string? Caption)`, `ReorderShowcaseRequest(IReadOnlyList<Guid> ImageIds)`. The DTO carries **no** object key, size, content type, or URL (FR-022).
- [X] T013 [P] Add the shared status enums (`ShowcaseAddStatus`, `ShowcaseMutateStatus`) per [data-model.md](./data-model.md), with `GalleryFull` distinct from every processing failure and `StaleOrder` distinct from `NotFound`.

**Checkpoint**: schema, write core, and the sweep guard are in place; the two surfaces can now be
built in parallel.

---

## Phase 3: User Story 1 — A player showcases their best moments (Priority: P1) 🎯 MVP

**Goal**: a member can add, reorder, caption, and remove up to five pictures on their own profile,
and everyone entitled to see that profile sees them.

**Independent Test**: quickstart [US1](./quickstart.md#us1--a-player-showcases-their-best-moments-p1) —
add five, be refused a sixth from curl, reorder, caption, remove, and confirm the avatar never moved.

### Tests for User Story 1

- [X] T014 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Profile/ProfileShowcaseTests.cs` covering: empty gallery lists `[]`; upload returns `201` and the DTO; five succeed and the sixth returns `409`; delete compacts positions to `0..n-1` **and reclaims the stored object** (FR-011 — the ordinary delete path, distinct from the two cascade paths in T028/T031); deleting an already-deleted image is idempotent (`204`); caption set, changed, and cleared, with an over-length caption (>120 chars) returning `400` (FR-005); reorder applies; a non-permutation reorder returns `409` with **nothing written**.
- [X] T015 [P] [US1] Concurrency test in the same file: 10 parallel `POST`s against an empty gallery leave **exactly 5** rows and produce 5 `409`s (SC-002, research R2).
- [X] T016 [P] [US1] Test that the avatar is untouched by every showcase operation and vice versa (FR-004).

### Implementation for User Story 1

- [X] T017 [US1] Create `backend/Services/Profile/IProfileShowcaseService.cs` and `ProfileShowcaseService.cs`: `ListAsync(handle, viewerUserId)` applying `ProfileService.IsVisibleTo`'s rule, `GetImageAsync(handle, imageId, viewerUserId)` (descriptor read → gate → **then** `IMediaStore.OpenReadAsync`, in that order), `AddAsync`, `SetCaptionAsync`, `RemoveAsync`, `ReorderAsync` — the four writers delegating to `ShowcaseWriter`. Reads use `AsNoTracking` + `.Select` projections ordered by `(Position, Id)`.
- [X] T018 [US1] Implement the add path ordering in `ProfileShowcaseService` exactly per research R5: process → cheap pre-count → mint key → `PutAsync` → locked transaction (re-count, insert) → on refusal or failure, best-effort `DeleteAsync` of the object just written, logged, never surfaced.
- [X] T019 [US1] Map `ImageProcessingStatus` to `ShowcaseAddStatus` in `ProfileShowcaseService`, mirroring `ProfileService.MapProcessingStatus`, so each failure category keeps a distinct non-technical reason (FR-016).
- [X] T020 [US1] Add the five profile endpoints to `backend/Controllers/ProfilesController.cs` per [contracts](./contracts/showcase-endpoints.md): `GET {handle}/showcase` and `GET {handle}/showcase/{imageId}/image` (`[AllowAnonymous]`, the image one also `[EnableRateLimiting(MediaRead)]` and returning through `MediaResponse.File`), plus `POST`/`PATCH`/`DELETE`/`PUT …/order` under `me/showcase` (`[Authorize]`, `POST` with `[RequestSizeLimit(8 MB)]` and `[EnableRateLimiting(MediaUpload)]`). Controllers stay thin: marshal `IFormFile`, map status → result, nothing else.
- [X] T021 [US1] Register `IProfileShowcaseService` and `ShowcaseWriter` in `backend/Program.cs` alongside the existing profile services.
- [X] T022 [P] [US1] Add `frontend/apps/web/src/app/core/models/showcase.models.ts` (`ShowcaseImage`, request shapes) and `core/services/showcase.service.ts` — one service serving both surfaces, with `profileImageUrl(handle, id)` / `teamImageUrl(slug, id)` helpers mirroring `ProfileService.avatarUrl`. **No automatic retry on any mutation** (Principle VII, browser hop).
- [X] T023 [US1] Create `frontend/apps/web/src/app/shared/showcase/showcase-gallery.component.{ts,html,css}` — read-only thumbnail grid, signals-based, with the three states per DESIGN.md: one muted `jh-loading` line, `jh-alert` + "Try again" on failure, and **nothing rendered at all** for a viewer who cannot edit an empty gallery (FR-026). Enlarged view arrives in US3.
- [X] T024 [US1] Create `frontend/apps/web/src/app/shared/showcase/showcase-manager.component.{ts,html,css}` — add (file input), caption edit, move up/down, remove, plus the disabled-when-full affordance. **No drag-and-drop library** (research R13). Optimistic UI is fine; a failed call must restore the previous order.
- [X] T025 [US1] Wire the gallery into `features/profile/components/profile-view/` (public and owner profile) and the manager into `features/profile/profile-owner/`, per DESIGN.md card and spacing tokens.
- [X] T026 [P] [US1] Jest specs for both components in `shared/showcase/`: order rendering, full-gallery state, failure restores previous order, empty renders nothing for a non-editor.
- [X] T027 [US1] Extend the pre-transaction key harvest in `backend/Services/Account/AccountDeletionService.cs` to collect profile showcase keys (with `IgnoreQueryFilters()`, next to the existing avatar read at line ~134) and generalise `ReclaimAvatarObjectAsync` to reclaim the whole list **after commit**, keeping its contract: never fail the request, log at error, leave the remainder to the sweep (research R7).
- [X] T028 [P] [US1] Test in `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/`: a member with five showcase images erases their account → zero rows **and** zero objects remain (FR-012, SC-010).

**Checkpoint**: US1 is a complete, shippable slice — a player has a working showcase.

---

## Phase 4: User Story 2 — A team shows what the team is like (Priority: P1)

**Goal**: team admins manage a team gallery; every signed-in member can see it and nobody else can
change it.

**Independent Test**: quickstart [US2](./quickstart.md#us2--a-team-shows-what-the-team-is-like-p1) —
admin adds, ordinary member sees but is refused by API, outsider sees, caps are independent, and
deleting the team removes the objects.

### Tests for User Story 2

- [X] T029 [P] [US2] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamShowcaseTests.cs`: admin can add/reorder/caption/remove; an ordinary member gets `403` on every write; a non-member gets `404` on writes and `200` on reads; an anonymous caller gets `401`; the sixth image returns `409`.
- [X] T030 [P] [US2] Test that a profile gallery at five and a team gallery at five coexist — caps are per owner, never pooled (FR-003).
- [X] T031 [P] [US2] Test that deleting a team removes both the rows and the stored objects (FR-012, SC-010).
- [X] T032 [P] [US2] Test that a team gallery stays visible after a member who uploaded to it is banned (research R3) — the team is not punished for a member's standing.

### Implementation for User Story 2

- [X] T033 [US2] Create `backend/Services/Teams/ITeamShowcaseService.cs` and `TeamShowcaseService.cs` — same five operations, authorization via `TeamMembershipGuard.ResolveAsync`: reads require any signed-in caller, writes require `IsAdmin` (`403`), a non-member gets `404`. Writes delegate to the same `ShowcaseWriter`.
- [X] T034 [US2] Add the five team endpoints to `backend/Controllers/TeamsController.cs` per [contracts](./contracts/showcase-endpoints.md). No `[AllowAnonymous]` anywhere — the class-level `[Authorize]` is the team surface's rule (feature 026).
- [X] T035 [US2] Register `ITeamShowcaseService` in `backend/Program.cs`.
- [X] T036 [US2] Harvest team showcase object keys **before** `Teams.ExecuteDeleteAsync` in `TeamService.DeleteAsync` (beside the existing "archive the chat BEFORE the team goes" step) and delete the objects after the delete succeeds, best-effort and logged (research R7).
- [X] T037 [US2] Render the gallery on `features/teams/team-detail/` for every signed-in viewer, and the manager for admins only — the manager component must not be in the template at all for a non-admin, so the "offered nothing" rule is structural rather than CSS.
- [X] T038 [P] [US2] Jest spec for the team surface: admin sees controls, member and outsider see none.

**Checkpoint**: both surfaces work end to end; the feature's scope is met apart from the enlarged
view and the polish phases.

---

## Phase 5: User Story 4 — The showcase does not open a privacy hole (Priority: P1)

**Goal**: prove — not assume — that the gating built into US1/US2 matches the avatar's rules exactly.
No production code is expected here beyond fixes the tests find.

**Independent Test**: quickstart [US4](./quickstart.md#us4--the-showcase-does-not-open-a-privacy-hole-p1),
every numbered check.

- [X] T039 [P] [US4] Gating tests in `Profile/ProfileShowcaseTests.cs`: private profile → anonymous listing and image both `404`, signed-in both `200`; public profile → anonymous both `200`; banned owner → all four `404`; a public→private switch takes effect on the very next request (FR-018, FR-019, FR-021, SC-003).
- [X] T040 [P] [US4] Test that every refusal is `404` and byte-identical across "no such image", "not permitted", and "store unavailable" — no status, header, or body distinguishes them (FR-023).
- [X] T041 [P] [US4] Test that no response body or header from any of the ten endpoints contains an object key, container name, or storage URL, and that the `ETag` is the 32-hex fingerprint rather than the key (FR-022, SC-004). Model it on feature 035's `MediaPrivacyTests.cs`.
- [X] T042 [P] [US4] Test that another member cannot mutate someone else's gallery: `PATCH`/`DELETE`/`PUT …/order` against a foreign image id → `404`, target gallery unchanged (FR-007).
- [X] T043 [US4] Add the two new anonymous profile reads to `backend/tests/JuggerHub.Api.IntegrationTests/Security/AnonymousAllowlistTests.cs`, and assert the team showcase endpoints are **not** anonymous — the allowlist is the record of what 026 deliberately leaves open.
- [X] T044 [US4] Test that a descriptor whose object has vanished degrades to a `404` for that one image while the rest of the gallery still lists and renders (FR-024).

**Checkpoint**: the privacy story is evidenced by tests, not by reading the code.

---

## Phase 6: User Story 5 — A bad upload fails clearly and changes nothing (Priority: P2)

**Goal**: every rejection is distinguishable, non-technical, and leaves the gallery byte-identical.

**Independent Test**: quickstart [US5](./quickstart.md#us5--a-bad-upload-fails-clearly-and-changes-nothing-p2) —
the six-row table, then the outage and missing-object checks.

- [X] T045 [P] [US5] Backend tests: PDF-as-JPEG, oversized file, 45 MP image, truncated JPEG, zero bytes, and a sixth-into-full each return the right status and a **distinct** reason; after each, the gallery holds exactly what it held before (FR-015, FR-016, SC-006).
- [X] T046 [P] [US5] Test the store-outage path against the Azurite Testcontainer (model it on feature 035's `MediaOutageTests.cs`): an upload that cannot be stored writes **no** row and consumes no slot; a stale descriptor is never created.
- [X] T047 [US5] Surface each reason in `showcase-manager.component` as a translated, human sentence with a retry affordance — never a status code, never a stack trace (DESIGN.md "Loading, error & retry states", Principle I).
- [X] T048 [P] [US5] Jest spec: each failure category renders its own message and leaves the rendered gallery unchanged.

---

## Phase 7: User Story 3 — Viewers can look at a picture properly (Priority: P2)

**Goal**: an enlarged view with next/previous, closable, keyboard-navigable, usable at 375 px.

**Independent Test**: quickstart [US3](./quickstart.md#us3--viewers-can-look-at-a-picture-properly-p2).

- [X] T049 [US3] Add the enlarged view to `shared/showcase/showcase-gallery.component.{ts,html,css}`, copying the established modal markup at `features/teams/team-detail/team-detail.component.html:250` — `fixed inset-0 z-50 … bg-black/40`, `role="dialog"`, `aria-modal="true"`, a labelled close control.
- [X] T050 [US3] Keyboard handling: Enter/Space opens from a thumbnail, ArrowLeft/ArrowRight page within the gallery order and stop at the ends, Escape closes, focus is trapped while open and **returned to the originating thumbnail** on close (FR-027, SC-007).
- [X] T051 [US3] Responsive behaviour: uniform thumbnail grid regardless of source aspect ratio, and an enlarged view that fits the whole picture (`object-contain`) at 375 px with no horizontal page scroll and no clipped controls (FR-025, edge case "very tall or very wide").
- [X] T052 [P] [US3] Jest spec for open/next/previous/close, end-stops, and focus restoration.
- [X] T053 [P] [US3] Alt text: caption when present, otherwise a translated generic alternative naming the owner (FR-028); captions rendered as text, never markup (FR-029).

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T054 Add the `showcase.*` block to `frontend/apps/web/public/i18n/en.json`, `de.json`, and `es.json` — all three in the same commit, or feature 042's parity guard turns the suite red (by design). Sentence case, "you" voice, no emoji (DESIGN.md).
- [X] T055 Instantiate `specs/046-showcase-galleries/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify every item against the diff — Gate 7 is engaged because this feature ships new UI on two screens. DESIGN.md wins any conflict; report conflicts rather than resolving them silently.
- [X] T056 [P] Verify `SC-008` in the browser: a five-image gallery issues one listing request and five image requests; an empty gallery issues the listing and **zero** image requests.
- [X] T057 [P] Confirm no `PagedResult<T>` crept into the listing endpoints and that the Complexity Tracking deviation in [plan.md](./plan.md#complexity-tracking) still describes what shipped.
- [ ] T058 Run the full quickstart end to end against a local stack, including the two ⚠ cross-cutting checks (sweep, account deletion). Record what was run; never report a check that was not made.
- [ ] T059 Run `dotnet test backend/JuggerHub.slnx` and `npx nx lint web && npx nx test web && npx nx build web`; both green before the feature is considered done.
- [ ] T060 [P] Update `README`/docs only if a documented surface changed — otherwise skip deliberately rather than inventing documentation.

---

## Dependencies

```text
Phase 1 (T001–T004)  ─┐
                      ├─→ Phase 2 (T005–T013)  ─┬─→ Phase 3  US1 (T014–T028) ─┬─→ Phase 5 US4 (T039–T044)
                      │      T009 is a data-loss │                             │
                      │      guard — never defer │                             ├─→ Phase 6 US5 (T045–T048)
                      │                          └─→ Phase 4  US2 (T029–T038) ─┘
                      │                                                        └─→ Phase 7 US3 (T049–T053)
                      └────────────────────────────────────────────────────────────→ Phase 8 (T054–T060)
```

- **Phase 2 blocks everything.** T011 (`ShowcaseWriter`) is used by both surfaces; T009 must land with
  the entities, not after them.
- **US1 and US2 are independent of each other** once Phase 2 is done and can be built in parallel by
  two people — they share only `ShowcaseWriter` and the DTOs.
- **US4 depends on US1 and US2** existing (it tests their gating) but adds no product code.
- **US5 depends on US1** (the upload path) and extends to US2 for free.
- **US3 depends on US1's gallery component** and is otherwise self-contained.
- **Phase 8** runs last; T054 must not be split across commits.

## Parallel execution examples

**Phase 2 setup** — `T005`, `T006`, `T010`, `T012`, `T013` touch different files and can run at once;
`T007` and `T008` must follow T005/T006.

**Two-person split after Phase 2** — one takes T014–T028 (profile, incl. account deletion), the other
T029–T038 (team, incl. team deletion). They meet at Phase 5.

**Test-heavy phases** — every task in Phase 5 is `[P]`; they are separate assertions in separate
files or independent test methods.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1).** That delivers a working player showcase, correctly
gated, with the sweep guard and account-deletion reclaim already in place. It is shippable on its own:
the team half, the enlarged view, and the failure-message polish are additive.

**Then, in order of value**: US2 (the other half of the issue) → US4 (evidence for the gating, cheap
and high-consequence) → US5 (upload honesty) → US3 (the enlarged view) → polish.

**Never defer**: T009. An environment that runs a reconciliation sweep between Phase 2 and T009
losing its galleries is not a bug to fix later — the objects are gone.
