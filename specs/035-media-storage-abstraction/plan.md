# Implementation Plan: Media Storage Abstraction + Object Storage

**Branch**: `035-media-storage-abstraction` | **Date**: 2026-07-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/035-media-storage-abstraction/spec.md`

## Summary

Introduce a reusable, owner-agnostic **`IMediaStore`** seam in the existing `backend/Services/Media/` namespace (created by 034) and move every media byte out of Postgres into **Azure Blob Storage**, with **Azurite** as the local/test stand-in. The three inline `bytea` columns — `ProfileAvatar.Bytes`, `BadgeIcon.Bytes`, `AchievementIcon.Bytes` — are replaced by an `ObjectKey` + `SizeBytes` descriptor pair on the *same* three tables, so every existing foreign key, cascade delete, and (critically) the `HasQueryFilter` ban gate on `ProfileAvatar` survives untouched.

The platform **proxies every byte**: the blob container is private in every environment, no SAS or direct link is ever issued, and reads keep flowing through the unchanged `/profiles/{handle}/avatar`, `/badges/{id}/icon`, and `/achievements/{id}/icon` endpoints with the authorization decision made exactly where it is made today. Because a media outage now sits on a member-visible **read** path, the store opts into the shared 028 resilience pipeline — with the Azure SDK's own retry **switched off**, since Azure Core has no circuit breaker and Principle VII requires a stop-condition wherever retry is used.

There is **no backfill**: the owner accepted total loss of existing media, so the migration drops the byte columns *and* the now-meaningless descriptor rows, leaving every owner in the clean "never had a picture" state.

## Technical Context

**Language/Version**: C# / .NET 10 (`backend/`, `TreatWarningsAsErrors=true`, nullable enabled)

**Primary Dependencies**: **`Azure.Storage.Blobs`** (new, 12.x, pinned major) and **`Microsoft.Extensions.Azure`** (new, DI registration + transport wiring). Existing: EF Core 10, Npgsql 10, `Microsoft.Extensions.Http.Resilience` (028), `SixLabors.ImageSharp` (034), ASP.NET Core rate limiting (019).

**Storage**: PostgreSQL keeps *descriptors only*; bytes live in an Azure Blob container. One EF migration drops `Bytes` from three tables, adds `ObjectKey`/`SizeBytes`, and deletes the orphaned descriptor rows.

**Testing**: xUnit. `Testcontainers.Azurite` (4.13.0, matching the existing Postgres/Redis modules) added to `JuggerHubApiFactory` so integration tests exercise the real blob path, not a fake. Existing `ProfileTests` / recognition-icon tests extended; the "served type" assertions from 034 stay valid.

**Target Platform**: Linux server (Docker, AKS). Azurite runs in `docker-compose.yml` for local and in `docker-compose.e2e.yml` for E2E.

**Project Type**: Web service (backend) + infrastructure. **No frontend change** — FR-004 holds, the Angular app calls the same URLs.

**Performance Goals**: Media reads must stay within today's budget (SC-004). Mitigated by streaming rather than buffering, plus `Cache-Control: private` + `ETag` so repeat views are `304`s and never touch the store.

**Constraints**: Container private in every environment (FR-012/FR-026). No link to the store ever reaches a client (FR-013). Revocation effective on the next request (FR-016). Bounded time limits + breaker on every store call (FR-027/FR-028). No secret in the repository (FR-024) — the Azurite dev key is the documented exception, see research §7.

**Scale/Scope**: One new interface + one implementation + options; three entities and their EF configuration; one migration; three service read/write paths; one Terraform resource pair; one compose service; one rate-limit policy; one reconciliation sweep. Reused unchanged by galleries (#99).

## Constitution Check

*GATE: evaluated pre-research and re-checked post-design. Constitution v1.4.0.*

| Principle / Gate | Assessment | Verdict |
|---|---|---|
| **I. Security-First, Never Trust the Client** | The whole feature is a security-preservation exercise. Container is private everywhere and the setting is enforced from the shared Terraform definition, not per-environment by hand (FR-026). No SAS, no redirect, no object key ever crosses the client boundary (FR-013). Object keys are random and unguessable, so a misconfiguration alone is not sufficient to expose media (FR-015). Store errors are caught and surfaced as generic responses via the existing `ExceptionHandlingMiddleware`. | ✅ Pass (strengthens) |
| **II. Thin Controllers, Service-Centric** | `IMediaStore` is a DI'd service behind an interface, owner-agnostic by construction. Controllers are unchanged apart from returning a stream instead of a byte array. Services still return DTOs; no object mapper. | ✅ Pass |
| **III. Disciplined Data Access** | Descriptors stay on the existing three tables, so `BaseEntity`, the audit interceptor, the FKs, the cascade deletes, and the `HasQueryFilter` ban gate all continue to apply unchanged. Reads use `.Select` projections + `AsNoTracking`. No list endpoint is added (pagination N/A). `Bytes` removal makes the existing projections strictly cheaper. | ✅ Pass |
| **IV. Secure Auth & Session** | Untouched. Same `[Authorize]` upload, same `[AllowAnonymous]` + service-side visibility gate on read. | ✅ Pass |
| **V. Environment Parity & Containerized Deployment** | Blob storage declared **once** in `infra/modules/storage/`, applied to every workspace; environments differ only in `account_replication_type` and retention sizing. Azurite gives local/E2E the same API. Config flows `.env` → compose and GitHub Environments → `kubernetes_secret_v1.app`, matching the existing `Email__Resend__ApiKey` pattern. **No Key Vault.** | ✅ Pass |
| **VI. Consistent Conventions & Tooling** | Backend C# + HCL only. No `.sh` added (the reconciliation sweep is an admin endpoint, not a script). No Angular change. | ✅ Pass |
| **VII. Resilient by Default** | **This is the gate that shaped the design.** The store opts into the shared 028 pipeline with one chained call and one `Resilience:Outbound:MediaStore` section. The Azure SDK's built-in retry is **disabled** (`MaxRetries = 0`) because leaving it on stacks two resilience implementations — explicitly review-rejectable — and because **Azure Core has no circuit breaker**, so it cannot satisfy "a stop-condition is required wherever retry is used" on its own. Breaker minimum throughput is set to **10, not the 100 default**, because media reads (unlike 028's email) genuinely reach that rate and a breaker that never opens is decorative. Reads degrade to placeholder rather than failing the page (FR-029). | ✅ Pass |
| **Gate 3 — Security review** | OWASP A01 (broken access control) is the live risk and is addressed by proxy-only delivery plus unguessable keys; A05 (misconfiguration) by putting the private-container setting in shared Terraform. | ✅ Pass |
| **Gate 7 — UI/Design compliance** | **N/A** — no UI change. The placeholder fallback already exists in the frontend; no DESIGN.md surface is touched. | ✅ N/A |
| **Dependency Management** | Adds `Azure.Storage.Blobs` and `Microsoft.Extensions.Azure`, both pinned to major, both first-party Microsoft. Dependabot raises majors individually. | ✅ Pass |

**Result**: No violations. See Complexity Tracking for owner-accepted trade-offs that are *not* violations but should not be discovered later by surprise.

**Post-design re-check (after Phase 1)**: the gate was re-evaluated against the produced artifacts and
still passes. Two design decisions moved *toward* the constitution rather than away from it, and are
worth naming because a reviewer might otherwise read them as arbitrary:

- **Principle III / FR-010** — keeping three descriptor tables instead of unifying them (research §6)
  was chosen *because* the polymorphic alternative cannot express `ProfileAvatar`'s ban query filter.
  Schema tidiness would have cost a structural security guarantee.
- **Principle VII** — disabling the Azure SDK's own retry (research §3) is what keeps the project at
  one resilience implementation. The design note that matters at review time is that a mis-wired
  transport leaves the store with **no** resilience while every other test still passes, so the wiring
  itself needs a test.

## Project Structure

### Documentation (this feature)

```text
specs/035-media-storage-abstraction/
├── plan.md              # This file
├── research.md          # Phase 0 — provider, resilience, key scheme, cutover decisions
├── data-model.md        # Phase 1 — descriptor columns, migration, object-key layout
├── quickstart.md        # Phase 1 — validation guide (incl. the privacy proof)
├── contracts/
│   ├── media-store.md        # Internal IMediaStore contract
│   └── media-endpoints.md    # HTTP endpoints — unchanged shape, new headers/failure modes
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
backend/
├── Services/Media/
│   ├── IMediaStore.cs                 # NEW — the seam: Put/Open/Delete/Exists/List (FR-001..FR-003)
│   ├── AzureBlobMediaStore.cs         # NEW — Azure.Storage.Blobs implementation
│   ├── MediaObjectKey.cs              # NEW — kind-prefixed, unguessable key generation (FR-015)
│   └── MediaReconciliationService.cs  # NEW — orphan sweep (FR-030)
├── Common/
│   └── MediaStorageOptions.cs         # NEW — connection, container, cache max-age, sweep grace
├── Entities/
│   ├── ProfileAvatar.cs               # EDIT — Bytes byte[] → ObjectKey string + SizeBytes int
│   ├── BadgeIcon.cs                   # EDIT — same
│   └── AchievementIcon.cs             # EDIT — same
├── Data/
│   ├── AppDbContext.cs                # EDIT — configure ObjectKey (maxlen, unique); ban filter UNCHANGED
│   └── Migrations/                    # NEW — drop bytes + rows, add descriptor columns
├── Services/
│   ├── Profile/ProfileService.cs      # EDIT — Set/GetAvatarAsync via IMediaStore; gate logic unchanged
│   ├── Badges/BadgeService.cs         # EDIT — same shape
│   └── Achievements/AchievementService.cs # EDIT — same shape
├── Controllers/
│   ├── ProfilesController.cs          # EDIT — stream + cache headers; add rate-limit policy
│   ├── RecognitionIconsController.cs  # EDIT — same
│   └── Admin/…                        # EDIT — expose the reconciliation sweep
├── Security/RateLimitPolicies.cs      # EDIT — add MediaRead, partitioned by user OR client IP
├── Program.cs                         # EDIT — AddAzureClients + resilience opt-in + options
└── JuggerHub.Api.csproj               # EDIT — Azure.Storage.Blobs, Microsoft.Extensions.Azure

backend/tests/JuggerHub.Api.IntegrationTests/
├── JuggerHubApiFactory.cs             # EDIT — start an Azurite container alongside PG/Redis
├── Media/MediaStoreTests.cs           # NEW — round-trip, delete, missing-object, key opacity
├── Media/MediaPrivacyTests.cs         # NEW — the FR-010..FR-016 proof (private/public/banned)
└── (Profile, Recognition tests)       # EDIT — descriptor assertions replace byte assertions

infra/
├── modules/storage/                   # NEW — account + private container, one definition
│   ├── main.tf  variables.tf  outputs.tf
├── main.tf                            # EDIT — wire module "storage" into the composition
├── variables.tf / envs/*.tfvars       # EDIT — replication tier per environment (sizing only)
└── modules/app/main.tf                # EDIT — MediaStorage__* into kubernetes_secret_v1.app

docker-compose.yml                     # EDIT — azurite service + backend env + depends_on
docker-compose.e2e.yml                 # EDIT — same, for E2E parity
.env.sample                            # EDIT — documented Azurite defaults
```

**Structure Decision**: Everything backend-side lands in the **existing** `backend/Services/Media/` namespace that 034 created, keeping "media concerns live in one place" true as galleries arrive. Infrastructure gets a **new `infra/modules/storage/`** module rather than extending `modules/network` or `modules/app`, so the store is composed at the root like every other capability and its per-environment sizing lives in `envs/*.tfvars` alongside the rest.

## Complexity Tracking

> No constitutional violations. These are owner-accepted trade-offs recorded so they are not rediscovered as surprises.

| Trade-off | Why accepted | What was rejected, and why |
|---|---|---|
| Media bytes traverse the backend (no CDN/direct delivery) | Owner chose proxy-only so the visibility gate stays welded to the request (spec Clarifications). Volume is small; `ETag`/`Cache-Control: private` keeps repeat views off the store entirely. | Public container — would let a private profile's avatar be reconstructed and would outlive a ban. Signed SAS links — degrades the gate to an expiry window, unrevocable without key rotation. |
| A third infrastructure dependency on the request path (Postgres, Redis, now Blob) | Unavoidable consequence of moving bytes out, and the reason galleries (#99) become affordable. | Keeping bytes in Postgres — the status quo #97 exists to end. |
| Total loss of existing media at cutover | Owner decision (spec Clarifications); removes the riskiest part of the feature — a data-moving migration. **Waives #97's "Existing avatars migrated" criterion.** | A verified backfill — materially more work and risk for data the owner does not value. |
