---

description: "Task list for 035 — Media Storage Abstraction + Object Storage"
---

# Tasks: Media Storage Abstraction + Object Storage

**Input**: Design documents from `/specs/035-media-storage-abstraction/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: **Included.** Not a default choice — SC-006 says "verified by test" in the success criterion itself, SC-003/SC-010 are security guarantees that cannot be asserted by inspection, and the repo already runs integration tests against real Postgres/Redis containers. Test tasks are therefore first-class here, not optional decoration.

**Organization**: Grouped by user story. Note the priority reality below before planning increments.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: US1–US5, mapping to spec.md user stories
- Exact file paths included in every task

## ⚠️ Read first: the MVP is US1 **and** US2 together

The spec makes US1 and US2 **both P1** and states plainly that *"neither story may ship alone."* US1 moves bytes out of Postgres; US2 is what stops that move from silently reversing the feature-026 privacy decision. Shipping US1 alone would be a privacy regression, not a partial delivery.

So the usual "MVP = User Story 1" shortcut **does not apply**. The first shippable increment is Phase 1 → Phase 4.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependencies, local emulator, and configuration surface — nothing behavioural yet.

- [X] T001 Add `Azure.Storage.Blobs` (12.x) and `Microsoft.Extensions.Azure` to `backend/JuggerHub.Api.csproj`, pinned to major, with an explanatory comment in the style of the existing `SixLabors.ImageSharp` entry
- [X] T002 [P] Add `Testcontainers.Azurite` version `4.13.0` to `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj`, matching the pinned Postgres/Redis module versions
- [X] T003 [P] Create `backend/Common/MediaStorageOptions.cs` with `ConnectionString`, `ContainerName` (default `media`), `CacheRevalidate` (default `true` → `private, no-cache`), `OrphanGraceMinutes` (default `60`) and safe-default normalization, per [data-model.md](./data-model.md#configuration)
- [X] T004 Add an `azurite` service (`mcr.microsoft.com/azure-storage/azurite`, blob port 10000) to `docker-compose.yml`, wire `MediaStorage__ConnectionString` + `MediaStorage__ContainerName` into the `backend` service environment, and add `azurite` to the backend's `depends_on`
- [X] T005 [P] Add an `azurite:` block to `docker-compose.e2e.yml` that resets `container_name: !reset null` and `ports: !reset []`, matching the existing `redis` entry. That file is an **isolation overlay**, not a service definition — it only neutralises the base file's pinned container names and published ports. Skipping this reproduces the exact failure its own comment records for Redis: `Conflict. The container name "/juggerhub-azurite" is already in use` when E2E runs while a dev stack is up
- [X] T005a [P] Check whether `backend-test` in `docker-compose.test.yml` needs the Azurite environment wiring, and add it if that service runs the API
- [X] T006 [P] Add the Azurite connection defaults to `.env.sample` with an inline note that `devstoreaccount1` and its key are **published Microsoft constants, not a secret leak** (research §7) — and that `UseDevelopmentStorage=true` will not work inside compose because the host is `azurite`, not `localhost`

**Checkpoint**: `docker compose up` starts Azurite; the app still builds and behaves exactly as before.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The storage seam, its resilience wiring, and the schema every story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 [P] Create `backend/Services/Media/IMediaStore.cs` with `PutAsync` / `OpenReadAsync` / `DeleteAsync` / `ExistsAsync` / `ListKeysAsync`, exactly per [contracts/media-store.md](./contracts/media-store.md) — owner-agnostic, no viewer/permission parameter anywhere. `ListKeysAsync` MUST return `IAsyncEnumerable<string>`, never a materialised list: Principle III forbids a service method returning an unbounded collection, and a container's object count is unbounded by definition
- [X] T008 [P] Create `backend/Services/Media/MediaObjectKey.cs` generating `{kind}/{32-hex}.webp` from `Guid.NewGuid()`, with a comment recording **why UUIDv4 and not the constitution's UUIDv7** (not a key, never indexed, must be unguessable — research §5) so a later reviewer does not "correct" it
- [X] T009 Create `backend/Services/Media/AzureBlobMediaStore.cs` implementing `IMediaStore` over `BlobContainerClient`: missing object returns `null` (never throws), `DeleteAsync` idempotent, content type set on write, container ensured idempotently and **never** with a public access level
- [X] T010 Wire DI in `backend/Program.cs`: `Configure<MediaStorageOptions>`, a named `HttpClient` carrying `.AddJuggerHubResilience(builder.Configuration, "MediaStore")`, and `AddAzureClients` with `BlobClientOptions.Transport = new HttpClientTransport(...)` **and `Retry.MaxRetries = 0`** — see research §3 for why the SDK's own retry must be off
- [X] T011 Add `Resilience:Outbound:MediaStore` defaults to `backend/appsettings.json`: `AttemptTimeoutSeconds` 5, `TotalTimeoutSeconds` 15, `MaxRetryAttempts` 2, `BreakerMinimumThroughput` **10** (not the 100 default — research §4)
- [X] T012 [P] Edit `backend/Entities/ProfileAvatar.cs`: replace `Bytes byte[]` with `ObjectKey string` + `SizeBytes int`; update the XML doc that currently describes `bytea` storage
- [X] T013 [P] Edit `backend/Entities/BadgeIcon.cs` with the same descriptor pair and doc update
- [X] T014 [P] Edit `backend/Entities/AchievementIcon.cs` with the same descriptor pair and doc update
- [X] T015 Edit `backend/Data/AppDbContext.cs`: configure `ObjectKey` as required, `HasMaxLength(200)`, with a unique index on each of the three entities — and **leave `ProfileAvatar`'s `HasQueryFilter(a => a.Profile.User.Status != AccountStatus.Banned)` exactly as it is**; that filter is the structural ban gate (research §6)
- [X] T016 Generate the EF migration in `backend/Data/Migrations/`: delete all rows from the three tables **first** (FR-019 — a surviving row would point at an object that was never written), then drop `Bytes`, then add the non-nullable descriptor columns and unique indexes
- [X] T017 Edit `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHubApiFactory.cs` to start an Azurite container alongside Postgres and Redis and inject its connection string into the test host
- [X] T018 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Media/MediaStoreTests.cs` covering round-trip, overwrite-at-same-key, missing object returns `null`, delete-absent succeeds, and key opacity (no handle or profile id in a generated key)
- [X] T019 Add a transport-wiring assertion to `backend/tests/JuggerHub.Api.IntegrationTests/Media/MediaStoreTests.cs` proving store calls travel through the named resilience-carrying `HttpClient` — **without this, a mis-wired transport leaves the store with zero resilience and every other test still passes** (research §3)

**Checkpoint**: `IMediaStore` round-trips against Azurite; the schema has descriptors; nothing reads or writes media through it yet.

---

## Phase 3: User Story 1 — Pictures load as before, bytes leave the database (Priority: P1)

**Goal**: All three media kinds store to and stream from the media store, through unchanged URLs.

**Independent Test**: Upload an avatar, fetch it at the same address, confirm the image matches and `ProfileAvatars` has no `Bytes` column.

- [X] T020 [US1] Rewire `SetAvatarAsync` in `backend/Services/Profile/ProfileService.cs` to the ordering **generate key → `PutAsync` → save descriptor → delete superseded object** (research §10), storing the processor's output unchanged (FR-005)
- [X] T021 [US1] Rewire `GetAvatarAsync` in `backend/Services/Profile/ProfileService.cs` to project the descriptor (`ObjectKey`, `ContentType`, `IsPublic`), apply the existing visibility gate, and **only then** call `OpenReadAsync` — returning the same "no picture" outcome when the object is absent
- [X] T022 [P] [US1] Rewire icon set/get in `backend/Services/Badges/BadgeService.cs` to the same store-then-descriptor ordering and stream-based read
- [X] T023 [P] [US1] Rewire icon set/get in `backend/Services/Achievements/AchievementService.cs` identically
- [X] T024 [US1] Update the avatar read/write signatures in `backend/Services/Profile/IProfileService.cs` to carry a stream instead of `byte[]` (`AvatarData`), keeping `AvatarSetStatus` values unchanged
- [X] T025 [P] [US1] Update the icon read signatures in `backend/Services/Badges/IBadgeService.cs` and `backend/Services/Achievements/IAchievementService.cs` to match
- [X] T026 [US1] Update `GetAvatar` in `backend/Controllers/ProfilesController.cs` to return `File(stream, contentType)`, keeping route, verb, and status codes identical (FR-004)
- [X] T027 [P] [US1] Update both icon endpoints in `backend/Controllers/RecognitionIconsController.cs` to stream, unchanged in shape
- [X] T028 [US1] Delete the stored object explicitly wherever **application code** deletes media: avatar replacement/removal in `backend/Services/Profile/ProfileService.cs`, and the `_db.BadgeIcons.Remove(icon)` / `_db.AchievementIcons.Remove(icon)` paths at `backend/Services/Badges/BadgeService.cs:125` and `backend/Services/Achievements/AchievementService.cs:125`. **Do not attempt to cover database-level cascades here** — all three entities are `OnDelete(DeleteBehavior.Cascade)` and `User → PlayerProfile` cascades too, so those deletes execute inside PostgreSQL with no application code running; the FR-030 sweep (T054) is their guaranteed backstop (FR-009)
- [X] T028a [US1] Add a code comment at each of the three `Cascade` configurations in `backend/Data/AppDbContext.cs` recording that a cascade delete orphans the stored object and is reclaimed only by the reconciliation sweep — so a future hard-delete path (GDPR erasure being the obvious candidate) is written with that in mind rather than discovering it silently
- [X] T029 [US1] Update `backend/tests/JuggerHub.Api.IntegrationTests/Profile/ProfileTests.cs` to assert descriptors and streamed content instead of stored bytes, preserving the `image/webp` served-type assertions introduced by 034
- [X] T030 [P] [US1] Update the recognition-icon integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/` for the same descriptor-based assertions
- [X] T031 [US1] Add an upload→fetch→replace→fetch integration test proving the replacement is served and the superseded object no longer exists (US1 scenario 2)

**Checkpoint**: All media flows through the store. **Do not ship yet — Phase 4 is part of this increment.**

---

## Phase 4: User Story 2 — The move opens no privacy hole (Priority: P1)

**Goal**: Prove the visibility gate, the ban filter, and the closed store all still hold.

**Independent Test**: The five-row table in [quickstart.md](./quickstart.md#the-privacy-proof-us2-sc-003-sc-010), plus a direct request to the store that must fail.

- [X] T032 [US2] Audit every media read path in `ProfileService`, `BadgeService`, and `AchievementService` to confirm the gate is evaluated **before** `OpenReadAsync` and that no path can reach bytes without it (FR-011, [contracts/media-endpoints.md](./contracts/media-endpoints.md#read-semantics))
- [X] T033 [US2] Grep the controllers, DTOs, and OpenAPI output for any exposure of `ObjectKey`, container name, or storage URL and confirm there is none (FR-013)
- [X] T034 [P] [US2] Create `backend/tests/JuggerHub.Api.IntegrationTests/Media/MediaPrivacyTests.cs`: private profile anonymous → `404`; private profile signed-in → `200`; **public profile anonymous → `200`**; catalogue icons anonymous → `200`
- [X] T035 [US2] Add a banned-account case to `MediaPrivacyTests.cs` asserting the avatar is unreachable by every route, and that the block takes effect on the **next** request with no grace window (FR-016)
- [X] T036 [US2] Add a direct-store test to `MediaPrivacyTests.cs` that requests a known object key straight from Azurite, bypassing the backend, and asserts it is refused (FR-012 / SC-010)
- [X] T037 [US2] Add a test asserting a generated object key contains neither the handle nor the profile id, so a leaked or guessed key cannot be constructed from public identifiers (FR-015)

**Checkpoint**: 🎯 **First shippable increment.** US1 + US2 together deliver the move without weakening privacy.

---

## Phase 5: User Story 3 — The cutover is clean (Priority: P2)

**Goal**: End-state consistency after deployment — one mechanism, no half-migrated remains, no broken images.

**Independent Test**: Deploy over a database holding inline media; confirm empty descriptors, working placeholders, working fresh uploads, and a no-op re-apply.

> The schema migration itself is T016 (foundational — nothing compiles without it). These tasks cover the **behaviour** of the cutover.

- [X] T038 [US3] Add a migration test in `backend/tests/JuggerHub.Api.IntegrationTests/Media/` seeding descriptor rows before migrating and asserting they are removed, so no record survives pointing at a nonexistent object (FR-019)
- [X] T039 [US3] Add a test asserting a member whose media was discarded returns the ordinary "no picture" `404` — not an error — and can upload successfully afterwards (US3 scenario 2)
- [X] T040 [US3] Add a re-apply test asserting a second migration run changes nothing and reports no errors (FR-020 / SC-007)
- [X] T041 [US3] Verify the admin catalogue icon upload path in `backend/Controllers/Admin/` re-populates badge and achievement icons after cutover with no new tooling (FR-021)
- [X] T042 [US3] Record the post-deploy step "re-upload catalogue icons" in [quickstart.md](./quickstart.md#deployment-checklist) and in the eventual PR description, since no automated seed restores them

**Checkpoint**: A deployment over existing data lands in a clean, single-mechanism state.

---

## Phase 6: User Story 4 — Same storage shape in every environment (Priority: P2)

**Goal**: One Terraform definition, present everywhere, differing only in sizing; secrets via GitHub Environments.

**Independent Test**: `terraform plan` shows a private container in each workspace; a developer with no cloud credentials can upload and view a picture locally.

- [X] T043 [P] [US4] Create `infra/modules/storage/main.tf` with `azurerm_storage_account` (`allow_nested_items_to_be_public = false`, `min_tls_version = "TLS1_2"`) and a **private** `azurerm_storage_container` — ⚠ confirm whether the pinned `azurerm ~> 4.0` expects `storage_account_name` or `storage_account_id` on the container (research §8)
- [X] T044 [P] [US4] Create `infra/modules/storage/variables.tf` and `outputs.tf` exposing the name prefix, location, resource group, replication type, tags, and the connection string as a `sensitive` output
- [X] T045 [US4] Handle the account-name constraint in `infra/modules/storage/main.tf`: names are globally unique and **3–24 lowercase alphanumeric**, so `juggerhub-dev` is invalid — sanitize the prefix and append a `random_string` suffix
- [X] T046 [US4] Compose `module "storage"` in `infra/main.tf` into the existing network → aks → platform → app chain, placing the account in the environment's resource group
- [X] T047 [P] [US4] Add `storage_replication_type` to `infra/variables.tf` and set it per environment in `infra/envs/dev.tfvars` and `infra/envs/prod.tfvars` — **sizing only**, never a difference in which resources exist (Principle V)
- [X] T048 [US4] Add `MediaStorage__ConnectionString` to `kubernetes_secret_v1.app` in `infra/modules/app/main.tf` and `MediaStorage__ContainerName` to `kubernetes_config_map_v1.app`, following the existing `Email__Resend__ApiKey` pattern
- [X] T049 [US4] Confirm each environment gets its **own storage account**, so no environment can read or overwrite another's objects (FR-025)
- [X] T050 [US4] Document the deployment checklist in [quickstart.md](./quickstart.md#deployment-checklist), including the per-environment direct-store verification SC-010 requires (verified, not inferred from Terraform)

**Checkpoint**: The store exists identically in every environment and no credential lives in the repository.

---

## Phase 7: User Story 5 — Outages degrade gracefully (Priority: P3)

**Goal**: A store outage costs pictures, never pages, never data consistency.

**Independent Test**: Stop Azurite; pages render with placeholders, uploads fail cleanly, nothing is left half-written.

- [X] T051 [US5] Ensure store failures on the read path resolve to the ordinary "no picture" `404` rather than a `500`, in `ProfileService`, `BadgeService`, and `AchievementService` (FR-029, US5 scenario 4)
- [X] T052 [US5] Ensure store failures on the upload path return the existing generic non-success status with the **previous picture untouched**, preserving the 034 guarantee (US5 scenario 2)
- [X] T053 [US5] Add operator-facing logging for store failures, retries, and breaker transitions with the operation identified and **no credentials, keys, or media content** in the records (FR-032)
- [X] T054 [P] [US5] Create `backend/Services/Media/MediaReconciliationService.cs`: stream container keys via `IAsyncEnumerable`, anti-join **in batches** against descriptor keys across all three tables, delete only objects older than `OrphanGraceMinutes`. Never materialise the full key set (Principle III). Per FR-009 this sweep is a **correctness guarantee, not an optimisation** — it is what reclaims objects orphaned by database-level cascade deletes, which run beneath the application (FR-030, research §10, [data-model.md](./data-model.md#what-deliberately-does-not-change))
- [X] T055 [US5] Expose the sweep as an admin-only endpoint in `backend/Controllers/Admin/AdminMediaController.cs`, returning a count of reclaimed objects. **Operator-triggered by design** — no scheduler, no background timer (spec Clarifications): orphans are rare and inert, while an unattended process whose job is deleting media is a hazard if its grace-period logic is wrong
- [X] T056 [US5] Add a `MediaRead` policy to `backend/Security/RateLimitPolicies.cs` partitioned by user when authenticated and **by client IP when anonymous** — the existing `PartitionByUser` cannot serve these endpoints, which are anonymous by design (FR-033, research §9)
- [X] T057 [US5] Apply the `MediaRead` policy to the avatar and icon read endpoints in `backend/Controllers/ProfilesController.cs` and `backend/Controllers/RecognitionIconsController.cs`
- [X] T058 [P] [US5] Create `backend/tests/JuggerHub.Api.IntegrationTests/Media/MediaOutageTests.cs` asserting that with the store unreachable, reads return the placeholder outcome and uploads fail without altering existing media (SC-006)
- [X] T059 [P] [US5] Add a reconciliation test asserting an unreferenced object older than the grace period is reclaimed while a just-uploaded object survives the sweep

**Checkpoint**: All five stories independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T060 [P] Add `Cache-Control: private, no-cache` and an `ETag` that is an opaque **hash of** `ObjectKey` to the media read endpoints, with `304` handling on `If-None-Match`. Three constraints, each with a reason: **`private`** or a shared cache recreates the exposure the proxy design prevents; **`no-cache`** (revalidate) not a long `max-age`, or a banned/newly-private member keeps rendering in a viewer's browser for the whole window and breaks FR-016; **hashed, never the raw key**, or the ETag publishes the object location FR-013 forbids disclosing (research §9)
- [X] T061 [P] Update the `ProfileAvatar` / `BadgeIcon` / `AchievementIcon` XML docs to describe descriptor-plus-object storage and drop the now-stale "documented migration path to object storage; GitHub issue #13" note in `backend/Entities/`
- [X] T062 Run the full [quickstart.md](./quickstart.md) validation, including the direct-store privacy proof and the outage scenario
- [X] T063 Run `dotnet build` and `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, plus `terraform validate` / `terraform plan` for at least one workspace
- [X] T064 Run `/security-review` over the branch diff, with OWASP A01 (broken access control) and A05 (misconfiguration) as the focus
- [X] T065 [P] Open a follow-up issue for **Managed Identity / AKS Workload Identity** to replace the account key, noting it would allow `shared_access_key_enabled = false` and that it was deferred only for the missing local-parity story (research §7)
- [ ] T066 Update GitHub issue #97: close it, and state explicitly that the *"Existing avatars migrated"* acceptance criterion was **deliberately waived** by owner decision, with the other three criteria met

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: needs Setup — **blocks every user story**
- **US1 (Phase 3)** and **US2 (Phase 4)**: both P1, both needed for the first ship
- **US3 (Phase 5)**, **US4 (Phase 6)**, **US5 (Phase 7)**: independent of one another once Foundational is done
- **Polish (Phase 8)**: after the stories it touches

### User Story Dependencies

- **US1 (P1)**: after Foundational
- **US2 (P1)**: after US1 — it verifies and hardens the paths US1 rewires. **Not independently shippable, by design**
- **US3 (P2)**: after Foundational; verification-only, so it can run alongside US4/US5
- **US4 (P2)**: after Foundational; infrastructure-only, no backend code dependency
- **US5 (P3)**: after US1 (it hardens the paths US1 creates)

### Within Each Story

- Interfaces before implementations; entities before the migration; services before controllers; behaviour before its tests where the test asserts real wiring

### Parallel Opportunities

- T002, T003, T005, T006 in Setup
- T007, T008 then T012, T013, T014 in Foundational (three different entity files)
- T022 and T023 (badge vs achievement services — separate files)
- T043, T044, T047 in the Terraform phase
- **US4 is fully parallel with everything backend**: it touches only `infra/` and can be done by a different person start to finish

---

## Parallel Example: Foundational entity changes

```bash
Task: "Edit backend/Entities/ProfileAvatar.cs — Bytes → ObjectKey + SizeBytes"
Task: "Edit backend/Entities/BadgeIcon.cs — Bytes → ObjectKey + SizeBytes"
Task: "Edit backend/Entities/AchievementIcon.cs — Bytes → ObjectKey + SizeBytes"
# Then, sequentially (same file):
Task: "Edit backend/Data/AppDbContext.cs — configure ObjectKey + preserve the ban query filter"
```

---

## Implementation Strategy

### First shippable increment (US1 + US2)

1. Phase 1 Setup
2. Phase 2 Foundational
3. Phase 3 US1 — bytes move
4. Phase 4 US2 — prove privacy held
5. **STOP and VALIDATE**: run the quickstart privacy proof in full
6. Ship only when the direct-store check fails as intended

### Incremental delivery after that

1. US4 (Terraform) — required before any deployed environment can run the feature at all
2. US3 (cutover verification) — required before deploying over existing data
3. US5 (resilience, sweep, rate limiting) — hardening
4. Phase 8 polish, security review, issue closure

### Parallel team strategy

- Developer A: Phases 1–4 (backend seam, services, privacy)
- Developer B: Phase 6 (Terraform, compose, secrets) — no overlap with A's files
- Then either picks up US3 and US5

---

## Notes

- `[P]` = different files, no dependency on incomplete work
- Commit per task or logical group; small commits per CLAUDE.md
- **Do not ship US1 without US2** — the spec forbids it and the reason is a real privacy regression
- Two research items are marked ⚠ for implementation-time verification: the `azurerm` container argument name (T043) and the Azure SDK transport wiring (T010/T019)
- Deployment order matters: US4 must land before any environment can serve media at all
