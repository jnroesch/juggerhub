# Implementation Plan: Showcase Image Galleries for Player and Team Profiles

**Branch**: `claude/speckit-flow-dqq76p` (spec dir `046-showcase-galleries`) | **Date**: 2026-08-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/046-showcase-galleries/spec.md`

## Summary

Up to **5 showcase pictures per player profile and per team**, shown on the profile and team pages,
with add / reorder / caption / remove for the owner (a member for their own profile, a **team admin**
for a team) and a thumbnails-plus-enlarged-view for everyone entitled to see them.

The plumbing already exists and is not rebuilt: **feature 034** supplies the image processor (this
feature adds a `Showcase` profile to it — an extension point 034 explicitly anticipated for #99) and
**feature 035** supplies `IMediaStore`, the proxy-only read path, the `MediaRead` rate limit, the
response shaping, and the reconciliation sweep. What this feature adds is **two descriptor tables**,
**one service per surface**, **ten thin endpoints**, and **two Angular components**.

Three things carry most of the risk, and all three are integration points rather than new code:
the **reconciliation sweep must learn the two new tables or it deletes every gallery** (R6); the
**two owner-deletion cascades run with no application code**, so object keys must be harvested before
and reclaimed after (R7); and the **cap is only real under a per-owner lock** (R2), for which the
repo already has a copyable precedent.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular + Nx (frontend)

**Primary Dependencies**: EF Core 10 + Npgsql, ASP.NET Core rate limiting, ImageSharp (via the
existing `IImageProcessor`), Azure Blob Storage SDK (via the existing `IMediaStore`), Transloco,
Tailwind. **No new package on either side** — including no drag-and-drop library (R13).

**Storage**: PostgreSQL 18 for the two descriptor tables; Azure Blob Storage (Azurite locally) for
the bytes, through `IMediaStore` only.

**Testing**: xUnit integration tests against `JuggerHubApiFactory` (Testcontainers: Postgres +
Azurite); Jest for Angular components.

**Target Platform**: Linux containers — AKS in Dev/Prod, docker-compose locally.

**Project Type**: Web application (existing `backend/` + `frontend/` monorepo).

**Performance Goals**: a gallery listing is one indexed query returning ≤5 rows; an image read is one
descriptor read plus one blob fetch, revalidated by `ETag` so a repeat view costs a `304`. A profile
or team page issues **one** listing request and at most five image requests (SC-008).

**Constraints**: stored image ≤1 MB, longest side 1280 px, five per owner (SC-005); every refusal
`404`; no object key in any response (SC-004); no new outbound integration, so **Principle VII adds
nothing to this diff** (R12).

**Scale/Scope**: 2 entities, 1 migration, 2 services, 10 endpoints, ~20 i18n keys × 3 locales, 2
shared Angular components + edits to 3 existing screens (owner profile, public profile, team detail).

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Result: **PASS** with one
recorded deviation (Complexity Tracking).*

| Principle / Gate | How this feature satisfies it | Verdict |
|---|---|---|
| **I — Security-first, never trust the client** | The cap, the visibility gate, the ban filter, and the admin check are all server-side; the disabled "add" button is UX only and the quickstart tests the bypass explicitly. Every refusal is a `404` with no oracle value (FR-023). No stack trace or store location reaches the client. | PASS |
| **II — Thin controllers, service-centric** | Controllers do `IFormFile` marshalling and status mapping only; `ProfileShowcaseService` / `TeamShowcaseService` hold the logic behind interfaces. DTOs are built with explicit `.Select` projections — **no object mapper**. | PASS |
| **III — Disciplined data access** | Both entities derive from `BaseEntity` (UUIDv7); audit fields come from the interceptor; reads use `AsNoTracking` + projections; the delete/compact path uses a single transaction. **Pagination**: deliberate deviation, see Complexity Tracking. | PASS w/ deviation |
| **IV — Auth & sessions** | Nothing new. Images are fetched by `<img src>` carrying the existing httpOnly cookie; no token handling is added anywhere. | PASS |
| **V — Environment parity** | No new configuration variable, no new resource. The showcase processing profile has a safe built-in default so the feature runs with zero configuration, identically in local/Dev/Prod. | PASS |
| **VI — Conventions** | Angular `.html`/`.css`/`.ts` kept separate; any script added is `.ps1`. | PASS |
| **VII — Resilient by default** | **Not engaged.** This feature adds no outbound integration: blob calls inherit `Resilience:Outbound:MediaStore` from 035. Adding a retry loop, a `Task.Delay`, or a second breaker here is review-rejectable. The one thing it *does* engage: the multi-step add/reorder/delete transactions run through `CreateExecutionStrategy` with all mutation inside the delegate, and the frontend never auto-retries an upload (browser-hop mutation). | PASS |
| **Gate 7 — UI/design compliance** | Engaged. This feature ships new UI on two screens, so `checklists/ui-review.md` is instantiated from the template and verified against the diff before verification. DESIGN.md governs the grid, the enlarged view, and the loading/error/empty states. | INSTANTIATED |
| **Gate 8 — Resilience review** | Applies only to confirm nothing was added — see VII. | PASS |

## Project Structure

### Documentation (this feature)

```text
specs/046-showcase-galleries/
├── plan.md                       # This file
├── spec.md                       # Requirements + owner clarifications
├── research.md                   # Phase 0 — 16 decisions, each read out of the code
├── data-model.md                 # Phase 1 — two entities, invariants, migration
├── contracts/
│   └── showcase-endpoints.md     # Phase 1 — ten endpoints, both surfaces
├── quickstart.md                 # Phase 1 — end-to-end validation guide
├── checklists/
│   ├── requirements.md           # Spec quality (complete)
│   └── ui-review.md              # Gate 7 — instantiated during the UI phase
└── tasks.md                      # Phase 2 — /speckit-tasks, NOT created here
```

### Source Code (repository root)

```text
backend/
├── Common/
│   └── ImageProcessingOptions.cs         # + Showcase profile (Fit, 1280px, 1 MB)
├── Entities/
│   ├── ProfileShowcaseImage.cs           # NEW
│   ├── TeamShowcaseImage.cs              # NEW
│   ├── PlayerProfile.cs                  # + ShowcaseImages navigation
│   └── Team.cs                           # + ShowcaseImages navigation
├── Data/
│   ├── AppDbContext.cs                   # + 2 DbSets, 2 entity configurations
│   └── Migrations/…_AddShowcaseGalleries # NEW — creates 2 tables, alters none
├── Dtos/Profile/ShowcaseDtos.cs          # NEW — ShowcaseImageDto + 2 requests
├── Services/
│   ├── Media/
│   │   ├── MediaObjectKey.cs             # + ProfileShowcase / TeamShowcase kinds
│   │   ├── MediaReconciliationService.cs # ⚠ + the two new tables (R6)
│   │   └── ShowcaseWriter.cs             # NEW — the shared locked add/reorder/delete core
│   ├── Profile/ProfileShowcaseService.cs # NEW (+ interface)
│   ├── Teams/TeamShowcaseService.cs      # NEW (+ interface)
│   ├── Teams/TeamService.cs              # + harvest & reclaim on team delete (R7)
│   └── Account/AccountDeletionService.cs # + harvest & reclaim on erasure (R7)
├── Controllers/
│   ├── ProfilesController.cs             # + 5 endpoints
│   └── TeamsController.cs                # + 5 endpoints
├── Security/RateLimitPolicies.cs         # + MediaUpload policy (20/min, per user)
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Profile/ProfileShowcaseTests.cs   # NEW
    ├── Teams/TeamShowcaseTests.cs        # NEW
    ├── Media/MediaReconciliationTests.cs # + galleries-survive-a-sweep test
    ├── Security/AnonymousAllowlistTests.cs # + the two new anonymous profile reads
    └── AccountDeletion/…                 # + objects reclaimed on erasure

frontend/apps/web/src/app/
├── core/
│   ├── models/showcase.models.ts         # NEW
│   └── services/showcase.service.ts      # NEW — both surfaces, one service
├── shared/showcase/
│   ├── showcase-gallery.component.{ts,html,css}   # NEW — read + enlarged view
│   └── showcase-manager.component.{ts,html,css}   # NEW — add/reorder/caption/remove
└── features/
    ├── profile/components/profile-view/  # + gallery
    ├── profile/profile-owner/            # + manager
    └── teams/team-detail/                # + gallery, and manager for admins

frontend/apps/web/public/i18n/{en,de,es}.json   # + showcase.* block (~20 keys each)
```

**Structure Decision**: the existing two-project web layout. No new project, library, or module is
introduced — the backend work lands in the existing `Services/Profile`, `Services/Teams` and
`Services/Media` namespaces and on the two existing controllers, because the galleries are
subresources of profiles and teams rather than a domain of their own.

## Phased delivery

Ordered so each phase is independently verifiable and the highest-risk integrations are covered by a
test before the UI exists.

| Phase | Content | Ends when |
|---|---|---|
| **1 — Model** | Two entities, two configurations, DbSets, one migration, the `Showcase` processing profile, the two `MediaKind` members. | `dotnet ef database update` applies cleanly; the suite is green. |
| **2 — Write core** | `ShowcaseWriter`: the locked add / reorder / delete / compact core (R2, R5), used by both services. | Concurrency test: 10 parallel adds → exactly 5 rows, 5 refusals. |
| **3 — Profile surface** | `ProfileShowcaseService` + 5 endpoints + the visibility gate + ban filter. | The US1, US4 and US5 API-level tests pass. |
| **4 — Team surface** | `TeamShowcaseService` + 5 endpoints + `TeamMembershipGuard` admin checks. | The US2 API-level tests pass. |
| **5 — ⚠ Lifecycle** | Sweep learns the two tables; team delete and account deletion harvest + reclaim. **Do not defer this** — the sweep gap destroys data. | A sweep leaves live galleries alone (including a banned member's); team delete and account erasure leave zero objects. |
| **6 — Frontend read** | `showcase-gallery` + service + models; wired into the public profile, owner profile, and team page; loading/error/empty states; enlarged view. | US3 and the DESIGN.md checks pass at 375 px and by keyboard. |
| **7 — Frontend manage** | `showcase-manager`: upload, caption, move up/down, remove; full-gallery and failure messaging. | US1 and US5 pass in the browser. |
| **8 — i18n + Gate 7** | `showcase.*` in all three catalogues; `checklists/ui-review.md` instantiated and verified. | Parity guard green; checklist complete. |

**Execution order lives in [tasks.md](./tasks.md), not in this table.** The phases above are a
narrative of the same work; tasks.md folds each surface's frontend into its user story so US1 is
shippable whole rather than as a backend half waiting for a UI phase. Where the two differ, the
task list wins.

## Traps this plan exists to prevent

1. **The sweep deletes every gallery.** `MediaReconciliationService` deletes every object no
   descriptor references. Ship without adding the two tables to its referenced-key set and the next
   operator sweep destroys all showcase media, irreversibly. And it must use
   `IgnoreQueryFilters()`, or a banned member's live objects look unreferenced. (R6)
2. **Two cascades run with no application code.** `TeamService.DeleteAsync` and
   `AccountDeletionService.EraseOwnedDataAsync` both hand deletion to Postgres. Rows vanish; objects
   do not. Harvest keys **before**, reclaim **after** commit — `ReclaimAvatarObjectAsync` is the
   pattern to extend, not to duplicate. (R7)
3. **The cap without a lock is a race.** Read-then-insert lets two adds into a four-image gallery.
   The `SELECT … FOR UPDATE` + execution-strategy pattern already exists in
   `TeamService.MutateMembershipAsync`; copy it rather than inventing a second idiom, and do not
   "simplify" it into a unique index — that yields 1 accepted out of 10 concurrent adds, not 5. (R2)
4. **Do not merge the two tables into one polymorphic media table.** `AppDbContext` carries an
   explicit instruction against it; the ban filter needs a single owner navigation. (R1)
5. **Never square-crop a showcase image.** The avatar profile is `SquareCrop`; using it here crops
   the subject out of the pictures the feature exists to show. The `Showcase` profile is `Fit`. (R8)
6. **The object key must not reach the client.** No DTO field, no header, no link. The `ETag` is a
   hash of the key, produced by `MediaResponse.File`, which is why that helper is called rather than
   re-implemented. (FR-022 / SC-004)
7. **`403` vs `404`.** Image and listing refusals are always `404`, never `403` — otherwise the
   endpoint reports whether a private member has pictures. The only `403` in the feature is a team
   **member** attempting an admin write, where existence is already known to the caller.
8. **Do not add a drag-and-drop dependency.** `@angular/cdk` is not in this repo. Move up/down
   buttons are accessible by construction and satisfy SC-007. (R13)
9. **Do not build moderation.** No report, takedown, or per-image admin surface — the platform has
   none, and the spec's Out of Scope says so. (Same discipline feature 041 recorded.)
10. **No i18n key may land in `en.json` alone.** Feature 042's parity guard turns the suite red; that
    is the guard working, not a flake.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `GET …/showcase` returns a bare `ShowcaseImageDto[]`, not `PagedResult<T>` (Principle III: "pagination is mandatory") | The collection is hard-capped at **5** by the feature's central requirement (FR-001), enforced server-side. The response is bounded by construction, which is the outcome the pagination rule exists to guarantee. | A `PagedResult<T>` envelope on a five-element list would ship `totalCount`, `skip` and `take` that can never vary, advertising a paging affordance that does not exist and inviting a client to implement "show more" against a closed set. Precedent on the same pages: `Roster` (48) and `RecentActivity` (6), and feature 044 recorded this identical deviation for the team happenings feed. |

## Implementation notes

Recorded after the build, for the next person reading this feature.

- **The write core is generic over the two entities** via a small `IShowcaseImage` interface
  (`Id`, `Position`, `ObjectKey`) that both descriptor entities implement. It is not mapped by EF —
  it exists only so `ShowcaseWriter` can lock, count, renumber, and compact either gallery without
  knowing whose it is. That is what keeps the cap identical on both surfaces rather than
  reimplemented twice.
- **Two places refuse differently, on purpose.** A team *member* who is not an admin gets `403`
  (they already know the team exists, so nothing is disclosed); a *non-member* gets `404`, matching
  every other team write. Everything about an image — listing, bytes, caption, delete, reorder — is
  `404` for every refusal.
- **`DELETE` is not idempotent**, contrary to the first draft of the contract. A repeated delete
  answers `404` like every other refusal, because a `204` there would distinguish "an id that used
  to be yours" from "an id that is not yours". The contract file records the change and the reason.
- **Editing and viewing are separate states on both surfaces.** The profile already splits view
  mode from edit mode, so the gallery's controls live in edit mode next to the avatar; the team
  page has no such split, so its card toggles between the gallery and the editing list. Rendering
  both at once listed the same five pictures twice and contradicted the page's own Edit button.
- **The owner's own profile fetches the gallery once** and passes it to both the read view and the
  editing controls (`jh-profile-view [showcase]`), so SC-008's "one listing request per page load"
  holds on the surface most likely to violate it.
- **The upload path pre-checks the cap before storing an object**, then re-checks it under the lock.
  The pre-check is not the guarantee — it only avoids writing an object that would immediately be
  deleted in the common non-concurrent case.

## Spec drift

- **SC-002 ("exactly 5 stored and 5 refusals" from a burst of 10)** is met *because of* the per-owner
  lock (R2). It is recorded here because it is a property of that decision, not of the feature: if
  implementation ever replaces the lock with a unique index, SC-002 must be renegotiated with the
  owner rather than quietly reinterpreted.
- **FR-011 ("no unreferenced stored object behind on the ordinary path")** is satisfied by the
  explicit reclaims. Two extraordinary paths remain the sweep's job by design (a crash between blob
  write and commit; a Postgres cascade) — that is feature 035's architecture, not a gap introduced
  here.
- **No other drift.** Every functional requirement maps to a phase above; nothing in the spec was
  narrowed during planning.
