# Quickstart / Validation: City Search Relevance Ranking

A run/validation guide proving the feature works end-to-end. Implementation details (entity/service
edits, migration, seed regeneration) live in `tasks.md` and the code.

## Prerequisites

- Backend builds: `dotnet build backend/JuggerHub.Api.csproj`
- Integration tests use Testcontainers (Docker running).
- The `CityReferences` table is seeded from the regenerated bundle (10-column seed incl. population).
  In existing environments, reseed once (truncate `CityReferences`; the seeder reloads on startup).

## Automated validation (authoritative)

Extend the existing suite; these are the acceptance checks:

```bash
dotnet test backend/tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj \
  --filter "FullyQualifiedName~CitySearchTests"
```

Expected new/updated tests (fixtures added to `TestReferenceCities`):

| Test | Asserts | Maps to |
|------|---------|---------|
| Populous city ranks first (no home city) | For `q=berlin` as a user with no home city, the highest-population "Berlin" is item 0. | SC-001, FR-005 |
| Same name+country ordered by population | Two same-name/same-country fixtures return most-populous first; region label still present. | FR-005 (+ 030 label) |
| Nearby city outranks distant larger city | User whose `HomeCity` is near a small same-named fixture sees it above a larger distant same-named fixture. | SC-002, FR-001 |
| No home city → no error, population fallback | User without a home city gets ranked results, no distance term, no error. | SC-003, FR-004 |
| Match tier preserved | An exact-prefix match still ranks above an alternate-name-only match. | SC-005, FR-002 |
| Ordering deterministic | Identical repeated query yields identical order. | FR-009 |

## Manual smoke (local)

1. Bring up the stack (`docker-compose up`) with a seeded `CityReferences`.
2. Sign in as a user **without** a home city; in the city picker type `berlin` → the German Berlin
   (largest population) is at the top; obscure same-named villages are pushed down.
3. Set that user's home city to a location near a small same-named town; search that name again →
   the nearby town now ranks above the larger distant one.
4. Confirm no geolocation permission prompt ever appears and labels/layout are unchanged (only order
   differs).

## Rollback

- Revert the code + migration. The regenerated 10-column seed is backward compatible only with the new
  seeder; if rolling back, restore the previous 9-column bundle and reseed. Reference data only — no
  user data affected.
