# Quickstart & Validation: Media Storage Abstraction + Object Storage

**Feature**: 035 · **Plan**: [plan.md](./plan.md) · **Contracts**: [media-store.md](./contracts/media-store.md) · [media-endpoints.md](./contracts/media-endpoints.md)

How to run this feature locally and prove it works. Scenarios map to the spec's user stories and
success criteria. Shell examples are PowerShell (Principle VI — no `.sh` in this repo).

## Prerequisites

- Docker Desktop running
- `.env` present (copy from `.env.sample`)
- The stack up: `docker compose up -d --build`

`.env.sample` ships working Azurite defaults, so **no cloud account and no credentials are needed**
(FR-023 / SC-005).

> **The published-credential note.** Azurite's `devstoreaccount1` account name and key are constants
> published in Microsoft's documentation — identical on every machine that has ever run the emulator.
> They are committed on purpose and are **not** a secret leak. Real account keys only ever come from
> GitHub Environments. Both `.env.sample` and `docker-compose.yml` carry this note inline.

> **The most likely setup mistake.** `UseDevelopmentStorage=true` only works when Azurite is on
> `localhost`. Inside compose the hostname is `azurite`, so the connection string must name the
> endpoint explicitly (`BlobEndpoint=http://azurite:10000/devstoreaccount1`). A wrong value here shows
> up as every picture 404-ing while the rest of the app works fine.

## Smoke test — the happy path (US1, SC-001)

1. Sign in and upload an avatar (profile → edit → picture).
2. Reload the profile. The picture displays, from the same URL as before:
   `GET /api/v1/profiles/{handle}/avatar`.
3. Confirm the bytes are **not** in Postgres:

```powershell
docker compose exec database psql -U postgres -d appdb -c '\d "ProfileAvatars"'
```

Expect `ObjectKey` and `SizeBytes` columns and **no `Bytes` column** (SC-002).

4. Confirm the object is in the store — browse the Azurite container, or:

```powershell
docker compose exec database psql -U postgres -d appdb -c 'SELECT "ObjectKey", "SizeBytes" FROM "ProfileAvatars";'
```

The key should look like `avatars/<32 hex>.webp` — **no handle, no profile id** in it (FR-015).

## The privacy proof (US2, SC-003, SC-010) — the one that matters most

This is the scenario the whole design exists to protect. Run all five.

| # | Do this | Expect |
|---|---|---|
| 1 | Set a profile **private**, then `GET /api/v1/profiles/{handle}/avatar` signed out | `404` |
| 2 | Same request, signed in as any member | `200` + image |
| 3 | Set the profile **public**, request signed out | `200` + image — public profiles stay anonymously visible |
| 4 | Request badge/achievement icons signed out | `200` — catalogue icons remain anonymous (FR-014) |
| 5 | Ban an account (admin area), then request its avatar by any route | `404` immediately, no cached grace (FR-016) |

Then the **direct-store check** (SC-010): take an `ObjectKey` from the database and request it straight
from the store, bypassing the backend.

```powershell
curl.exe -i "http://localhost:10000/devstoreaccount1/media/avatars/<key>.webp"
```

Expect a failure, not the image. If this returns bytes, FR-012 is broken and the feature is not
shippable regardless of what the other tests say. Repeat against Dev/Prod storage endpoints after each
deployment — SC-010 requires this verified **per environment**, not inferred from Terraform.

## Outage behaviour (US5, SC-006)

```powershell
docker compose stop azurite
```

- Load a profile page and the player directory → pages **render**, pictures show the placeholder (FR-029).
- Attempt an avatar upload → clear non-technical failure; the previous picture is unchanged.
- Check the backend logs → retries/breaker transitions are visible, with **no credentials and no
  object content** in them (FR-032).

```powershell
docker compose start azurite
```

Service resumes with no manual cleanup.

## Cutover behaviour (US3, SC-007)

Against a database that already holds inline media:

1. Apply migrations → the three tables are empty, `Bytes` is gone, the new columns exist.
2. Load a profile whose picture was discarded → placeholder, **not** a broken image or an error.
3. Upload a new picture → works end to end.
4. Re-apply migrations → no-op, no errors.
5. Re-upload catalogue icons via the admin area → they display everywhere icons appear (FR-021).

## Orphan reclamation (FR-030)

1. Upload an avatar, note the `ObjectKey`.
2. Upload a replacement → the superseded object is deleted; the new key is stored.
3. To exercise the sweep, delete a descriptor row directly in the database, then:

```powershell
curl.exe -X POST http://localhost:8080/api/v1/admin/media/reconcile -H "Authorization: Bearer <admin token>"
```

The unreferenced object is removed **only** if older than `OrphanGraceMinutes`; a just-uploaded object
must survive the sweep (that is the grace period doing its job).

## Automated tests

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
```

Integration tests start Azurite via Testcontainers alongside Postgres and Redis, so they exercise the
real blob path. Coverage to expect:

- `Media/MediaStoreTests` — round-trip, overwrite, absence, key opacity, **transport wiring** (a
  mis-wired transport leaves the store with no resilience and every other test still passes)
- `Media/MediaPrivacyTests` — the five-row table above, as code
- Existing `Profile` / recognition-icon tests — descriptor assertions replacing byte assertions

## Deployment checklist

- [ ] `terraform plan` shows the storage account + **private** container, with
      `allow_nested_items_to_be_public = false`
- [ ] `MediaStorage__ConnectionString` set in the GitHub Environment (never in the repo)
- [ ] Environments use **separate accounts** — confirm Dev cannot read Prod's container (FR-025)
- [ ] Post-deploy: run the direct-store check above against that environment (SC-010)
- [ ] Post-deploy: re-upload catalogue icons
