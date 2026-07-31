# Phase 0 Research: Media Storage Abstraction + Object Storage

**Feature**: 035 · **Date**: 2026-07-31 · **Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

Every open question from the Technical Context is resolved below. Items marked **⚠ verify at
implementation time** are version-sensitive details that must be confirmed against the installed
package/provider rather than trusted from this document.

---

## 1. Storage provider and client library

**Decision**: **Azure Blob Storage**, accessed via **`Azure.Storage.Blobs` 12.x**, registered through
**`Microsoft.Extensions.Azure`** (`AddAzureClients`), behind the `IMediaStore` interface.

**Rationale**: The platform already runs on AKS with Terraform-managed Azure resources (015), so Blob
is the option that adds a provider relationship the project already has rather than a new one. The
SDK is first-party, pinned-major friendly, and `Microsoft.Extensions.Azure` gives idiomatic DI plus —
critically — a supported hook for replacing the HTTP transport, which §3 depends on. Keeping it behind
`IMediaStore` mirrors what 034 did with `IImageProcessor`: the provider is an implementation detail,
and #99 consumes the seam without knowing about Azure.

**Alternatives considered**:
- **S3-compatible (MinIO) everywhere** — better provider-neutrality and a nicer local story, but adds
  a service the project would have to host and operate itself, against an Azure-hosted platform. Rejected.
- **Keep bytes in Postgres, add a caching layer** — does not address the actual driver (#99 galleries
  multiply stored bytes), and #97 exists precisely to stop doing this.

---

## 2. Local and test parity: Azurite

**Decision**: **Azurite** (`mcr.microsoft.com/azure-storage/azurite`) as a compose service for local
and E2E, and **`Testcontainers.Azurite` 4.13.0** in the integration-test factory alongside the
existing Postgres and Redis modules.

**Rationale**: Azurite is Microsoft's own emulator and speaks the real Blob REST API, so the same
`Azure.Storage.Blobs` code path runs locally, in tests, in Dev, and in Prod — Principle V satisfied by
actual API parity rather than by a hand-written fake. Pinning Testcontainers to 4.13.0 matches the
Postgres/Redis modules already in the test project, so the three move together.

**Key details**:
- Blob endpoint is port **10000**; the emulator also exposes queue (10001) and table (10002), unused here.
- Under compose the hostname is not `localhost`, so `UseDevelopmentStorage=true` will **not** work —
  the full connection string with an explicit `BlobEndpoint=http://azurite:10000/devstoreaccount1` is
  required. This is the single most likely local-setup mistake; call it out in `quickstart.md`.
- Container creation is not automatic. The app must create-if-absent at startup, which also keeps
  Dev/Prod first-boot working. ⚠ Do this idempotently (`CreateIfNotExistsAsync`) and never with a
  public access level argument.

**Alternatives considered**:
- **A filesystem-backed `IMediaStore` for local, Azure for deployed** — tempting and simpler, but it
  puts a *different implementation* in local than in deployed, which is exactly the environment-shape
  divergence Principle V forbids. Rejected. (An in-memory store remains acceptable for narrow unit
  tests that are not asserting storage behaviour.)

---

## 3. Resilience: why the Azure SDK's own retry is switched OFF

**Decision**: Configure `BlobClientOptions.Retry.MaxRetries = 0` and route the SDK's transport through
a named `HttpClient` that carries the shared 028 pipeline via
`.AddJuggerHubResilience(configuration, "MediaStore")`.

**Rationale** — two independent reasons, either sufficient:

1. **Azure Core has no circuit breaker.** Its retry policy offers attempt counts, exponential backoff
   with jitter, and a network timeout — but no breaker and no equivalent stop-condition. Principle VII
   states plainly that "a circuit breaker or equivalent stop-condition is **required** wherever retry
   is used" and that "retry without a stop condition is a hazard, not a safeguard." The SDK's retry
   therefore *cannot* satisfy Gate 8 on its own.
2. **Leaving both on stacks two resilience implementations.** Polly retrying an operation that the SDK
   is already retrying multiplies attempts (3 × 3 = 9) under exactly the failure conditions where
   amplification is most harmful. `ResilienceExtensions` calls stacked handlers "explicitly
   unsupported"; the constitution calls them review-rejectable.

So: exactly one resilience implementation — the shared one — reached with one chained call and one
configuration section, which is the pattern Principle VII prescribes.

**⚠ verify at implementation time**: the transport is set via `BlobClientOptions.Transport = new
HttpClientTransport(httpClient)`, with the `HttpClient` resolved from `IHttpClientFactory` by the name
the resilience pipeline was registered under. Confirm the wiring against the installed
`Microsoft.Extensions.Azure` version, and add an assertion-style test that a store call actually flows
through the named client — a silently-unwired transport would leave the store with *no* resilience at
all and would look identical in green tests.

**Alternatives considered**:
- **Use Azure Core retry alone, configured from a `ResilienceOptions`-shaped section** — fails
  requirement 1 above (no breaker) and creates a second resilience dialect with its own telemetry.
- **Wrap `IMediaStore` calls in a hand-rolled Polly pipeline** — a per-call-site resilience decision,
  which Principle VII explicitly forbids.

---

## 4. Circuit-breaker sizing (the 028 trap, inverted)

**Decision**: default `Resilience:Outbound:MediaStore` to `BreakerMinimumThroughput = 10`,
`AttemptTimeoutSeconds = 5`, `TotalTimeoutSeconds = 15`, `MaxRetryAttempts = 2`.

**Rationale**: Feature 028 learned that the .NET standard handler's default minimum throughput —
100 calls per 30s sampling window — meant the breaker **never opened** at JuggerHub's email volume,
making it decorative. Media reads invert that situation: a directory or team page issues one media
request per displayed member, so 100/30s is *plausibly* reachable at real usage but nowhere near
guaranteed at today's. Sizing the threshold at 10 makes the breaker actually able to trip while
staying above incidental noise. Attempt/total timeouts are tighter than the email defaults because
this sits on a member-visible read path where a slow response is worse than a fast placeholder.

Retries are capped at 2: uploads are the only mutating operation, they are server-to-server (not the
browser hop), and a duplicated blob write is idempotent because the object key is generated once per
upload before the first attempt — a replay overwrites the same key rather than creating a second object.

**⚠ Revisit** these numbers once real traffic exists. They are configuration with safe defaults, per
Principle VII, so tuning is a config change and not a code change.

---

## 5. Object key scheme — and a deliberate divergence from the UUIDv7 rule

**Decision**: `{kind}/{32-hex-random}.webp`, e.g. `avatars/9f2c…a1.webp`, generated with
**`Guid.NewGuid()` (v4, random)** — *not* `Guid.CreateVersion7()`.

**Rationale**: Principle III mandates UUIDv7 for **primary keys**, and gives the reason explicitly:
timestamp-prefixing makes inserts append to the right edge of the B-tree. An object key is **not a
primary key** — it is never a database key, is never indexed for range scans, and gains nothing from
locality. What it *must* be is **unguessable** (FR-015), and UUIDv7 is the wrong tool for that because
its timestamp prefix is partially predictable. Using v4 here is therefore not a deviation from the
principle's intent but an application of it: use the identifier whose properties match the job.

This is recorded deliberately so a future reviewer does not "fix" it back to v7.

**Supporting detail**: keys are stored in the descriptor row and **never** leave the backend (FR-013),
so unguessability is defence-in-depth against a future container misconfiguration, not the primary
control. The primary control is that the container is private (FR-012/FR-026).

**Alternatives considered**:
- **`avatars/{profileId}.webp`** — derivable from data already exposed in DTOs. Directly violates
  FR-015 and would make a single misconfigured container a bulk-enumeration incident. Rejected.
- **`avatars/{handle}.webp`** — worse; handles are in the public URL. Rejected.
- **Content-addressed (hash of bytes)** — deduplicates, but makes deletion unsafe when two owners
  share an image and leaks equality ("these two members uploaded the same picture"). Rejected as
  unnecessary at this scale.

---

## 6. Descriptor placement: keep three tables, do not unify

**Decision**: Keep `ProfileAvatar`, `BadgeIcon`, and `AchievementIcon` as they are and swap
`Bytes byte[]` for `ObjectKey string` + `SizeBytes int`. Do **not** introduce a unified polymorphic
`MediaAsset` table.

**Rationale**: This is the highest-leverage decision in the plan and it is driven by the privacy
requirement rather than by taste. `AppDbContext` configures

```csharp
entity.HasQueryFilter(a => a.Profile.User.Status != AccountStatus.Banned);
```

on `ProfileAvatar` (and the analogous filter on `PlayerProfile`). That global filter is what makes
"a banned account's media is not served" true **by construction** rather than by remembering to check.
A unified polymorphic table has no single owner navigation, so the filter cannot be expressed — the ban
gate would have to be re-derived by hand at every call site, which is precisely the class of change
FR-010 forbids and which the spec calls a regression rather than a trade-off. Keeping the tables also
preserves the existing FKs and cascade deletes that satisfy FR-009 for free.

The owner-agnosticism FR-001–FR-003 demands lives in **`IMediaStore`**, which knows nothing about
profiles, badges, or achievements. That is the correct seam for it; the descriptor rows are owner-specific
by design.

**Alternatives considered**:
- **One `MediaAsset` table with `(OwnerType, OwnerId)`** — the 006 `EventSignup` polymorphic precedent
  exists, but there the polymorphism *was* the domain. Here it would trade a working, structural
  security guarantee for schema tidiness. Rejected on those grounds.

---

## 7. Configuration, secrets, and the one committed credential

**Decision**: A connection string in `MediaStorage__ConnectionString`, flowing `.env` → compose locally
and GitHub Environments → `kubernetes_secret_v1.app` when deployed — the same path
`Email__Resend__ApiKey` already takes. **No Key Vault.**

**The exception, stated explicitly**: Azurite's development account (`devstoreaccount1`) and its key
are **published constants in Microsoft's documentation**, identical on every machine that has ever run
the emulator. Committing them to `.env.sample` and `docker-compose.yml` is therefore not a secret leak
and does not violate FR-024 — but it *will* look like one to a reviewer or a secret scanner. Annotate
it in place, at both sites, so nobody has to re-derive that judgement. Real account keys never appear
outside GitHub Environments.

**Alternatives considered**:
- **Managed Identity / AKS Workload Identity instead of an account key** — genuinely better: no
  long-lived credential to rotate or leak, and it would let `shared_access_key_enabled = false` on the
  account. Deferred rather than rejected: it requires federated-identity setup and an OIDC issuer
  configuration on the cluster that the current Terraform does not have, and it has no local-parity
  story (Azurite has no managed identity), so it would reintroduce an environment-shape difference.
  **Tracked as GH #103**, together with moving the account behind a private endpoint.

---

## 8. Terraform shape

**Decision**: A new `infra/modules/storage/` module composed from the root, creating an
`azurerm_storage_account` in the existing per-environment resource group plus one private
`azurerm_storage_container`. Only `account_replication_type` (and any retention sizing) differs per
environment, set from `envs/*.tfvars`.

**Settings that carry the security requirement (FR-026)**:
- `allow_nested_items_to_be_public = false` — the account-level kill switch; even a container
  mistakenly created with a public access level cannot serve anonymously.
- container access type **private**.
- `min_tls_version = "TLS1_2"`.

**Naming gotcha**: storage account names are globally unique across Azure and must be **3–24 lowercase
alphanumeric characters** — no hyphens. The existing `local.name_prefix` is `juggerhub-dev`, which is
invalid as-is. Sanitise (`replace(…, "-", "")` → `juggerhubdev`, 12 chars) and append a short
`random_string` suffix for global uniqueness, leaving room inside the 24-char limit.

**⚠ verify at implementation time**: `azurerm` is pinned `~> 4.0`, and within the 4.x line
`azurerm_storage_container` moved from `storage_account_name` to `storage_account_id`. Check which the
installed provider expects before writing the resource — this is a plan-time error, not a runtime one,
so it will surface immediately.

**Alternatives considered**:
- **Extend `modules/app` or `modules/network`** — would bury a first-class capability inside an
  unrelated module and break the root-composition pattern the other capabilities follow. Rejected.

---

## 9. Serving: streaming, caching, and rate limiting

**Decision**: Read the blob as a stream and return `File(stream, contentType)`; set
`Cache-Control: private, no-cache` plus an `ETag` hashed from the object key; add a
`MediaRead` rate-limit policy.

**Rationale**:
- **Buffer the object before responding; do not hand back a lazy stream.** ⚠️ **Corrected during
  implementation** — the original decision here was to stream, on the reasoning that copying through
  avoids materialising each object per request (FR-033). That reasoning was wrong in a way that only
  showed up against a real broken blob. Azure's `OpenReadAsync` returns a *lazily loading* stream, so
  the actual fetch happens when ASP.NET copies it into the response body — **after** the status line
  and headers are already committed. A store failure at that point cannot become a graceful "no
  picture" (FR-029): the response is already a `200` and the request dies mid-body. Worse, the defect
  is invisible to anyone whose browser holds the image (they revalidate and get a `304` without ever
  touching the store) and breaks only for **first-time viewers** — which is how it survived a green
  test suite and a manual check by the uploader, and surfaced only for an anonymous visitor.
  The store now downloads fully inside its `try`, so every failure happens while the response can
  still be chosen. Affordable because 034 bounds stored media at ≤512 KB (avatars) / ≤128 KB (icons),
  and repeat views short-circuit to `304` without reaching the store at all. If gallery volume ever
  makes buffering cost real, the answer is caching — **not** a stream that cannot fail safely.
- **`private`, never `public`** — a gated avatar must never be storable in a shared/intermediary cache.
  Getting this token wrong would reintroduce, in a CDN, exactly the exposure the proxy decision was
  made to avoid. Catalogue icons *could* be `public`, but the uniform `private` is chosen deliberately:
  one rule, no per-kind reasoning, and the win from caching is already captured client-side.
- **`no-cache`, not a long `max-age`** — `no-cache` permits storing but forces revalidation. A long
  freshness window would stop the browser issuing a request at all, so a member who goes private or is
  banned would keep rendering in a viewer's browser until it expired — which is exactly the "window
  during which previously-issued access continues to work" that FR-016 forbids. Revalidation keeps the
  guarantee and still costs only a descriptor read.
- **`ETag` + `304`** — the cheapest possible answer to SC-004, since a repeat view then costs one
  descriptor read and no store call at all. Derive it from a **hash of** the object key (which is
  regenerated per upload, so the hash changes exactly when the bytes do) rather than from a timestamp —
  and never from the raw key, which FR-013 forbids disclosing to a client.
- **Rate limiting** — reuse the 019 `RateLimitPolicies` pattern. ⚠ The existing partitioner is
  `PartitionByUser`, which does not fit here: these endpoints serve **anonymous** callers by design
  (public profiles, catalogue icons). The policy must partition by user *when authenticated* and fall
  back to client IP otherwise, and it must remain fail-closed like the existing Redis-backed limiter.

---

## 10. Cutover and orphan reclamation

**Decision (cutover)**: One EF migration that (a) deletes all rows from the three descriptor tables,
(b) drops `Bytes`, (c) adds `ObjectKey`/`SizeBytes` as required columns. Deleting the rows is what
satisfies FR-019 — a surviving row would point at an object that was never written, which is exactly
the "record without object" edge case. Because rows are deleted rather than migrated, adding the new
columns as `NOT NULL` needs no default backfill.

**Decision (orphans)**: Ordering is **write object → save descriptor → delete superseded object**, so
the failure modes are limited to unreferenced objects (never a descriptor pointing at nothing), plus a
`MediaReconciliationService` that lists container objects, anti-joins against known descriptor keys,
and deletes only those older than a configurable grace period. The grace period is what keeps an
in-flight upload from being swept mid-request. Exposed as an admin-triggered operation rather than a
background timer — the volume does not justify a scheduler, and an operator-invoked sweep is easier to
reason about and to test.

**Alternatives considered**:
- **Delete the old object before saving the new descriptor** — creates a window where a failure leaves
  the member with no picture at all. Strictly worse; the orphan is the cheaper failure.
- **A background hosted-service sweep on a timer** — more moving parts, and with multiple replicas it
  needs leader election to avoid concurrent sweeps. Not justified at this scale.

---

## Summary of decisions

| # | Decision |
|---|---|
| 1 | Azure Blob via `Azure.Storage.Blobs` 12.x behind `IMediaStore` |
| 2 | Azurite for local/E2E/integration tests — same API everywhere, no alternate implementation |
| 3 | Azure SDK retry **off**; shared 028 Polly pipeline via a named `HttpClient` transport |
| 4 | Breaker minimum throughput **10** (not the 100 default); 5s attempt / 15s total / 2 retries |
| 5 | Object keys are random **UUIDv4** hex — a reasoned divergence from the UUIDv7 PK rule |
| 6 | Keep the three descriptor tables; a unified polymorphic table would break the ban query filter |
| 7 | Account key via `.env` / GitHub Environments; Azurite's published dev key is an annotated exception |
| 8 | New `infra/modules/storage/`; `allow_nested_items_to_be_public = false`; sanitised account name |
| 9 | Fully buffer before responding (never a lazy stream) + `Cache-Control: private, no-cache` + hashed `ETag`; new rate-limit policy with an IP fallback partition |
| 10 | Cutover deletes descriptor rows and drops bytes; orphans reclaimed by an admin-triggered sweep — which FR-009 makes a correctness guarantee, since database-level cascade deletes run beneath the application and cannot delete a blob |
