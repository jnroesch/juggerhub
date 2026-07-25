# Phase 0 Research: Structured Locations & "Near You" Discovery

**Feature**: 030-structured-locations | **Date**: 2026-07-25

All decisions below resolve the Technical Context unknowns. Format per decision: **Decision → Rationale → Alternatives rejected**.

---

## R1 — Geocoding provider (self-hosted, all environments)

**Decision**: Use **Photon** (`komoot/photon`) as the self-hosted geocoder, run as a single container in docker-compose (local) and in-cluster (Dev/Prod), fronted by our backend. Photon is queried only server-side.

**Rationale**:
- Photon is purpose-built for **type-ahead autocomplete** (the exact UX we need) and returns structured OSM results as GeoJSON: place name, `country`, `state`/`county` (region), and `lat`/`lon` coordinates — everything Canonical City needs in one call.
- Single self-contained container with a downloadable prebuilt index (no separate DB/import pipeline the way Nominatim needs `osm2pgsql`). Same image everywhere satisfies parity (Principle V); no API key, no per-request billing, and **no user location leaves our infra** (Principle I).
- Fits the existing outbound-resilience pattern unchanged: one `AddJuggerHubResilience(config, "Geocoding")` call (Principle VII).

**Alternatives rejected**:
- **Nominatim** — address-grade geocoder; heavier import (`osm2pgsql` + Postgres), slower for prefix autocomplete, more moving parts. Overkill for city selection.
- **Hosted SaaS (Mapbox/LocationIQ/Google)** — rejected in clarification: keys, billing, and user location sent to a third party; breaks offline parity.
- **Bundled GeoNames dataset** — rejected by owner in favour of an external geocoding API.

**⚠️ Key risk — index data size / parity nuance**: Photon's global index is large (tens of GB). To keep local dev and CI practical we default to a **regional extract** (DACH + surrounding Europe, where Jugger is concentrated) referenced by configuration. The **service, image, and API shape are identical across environments**; only the *size of the index data* differs, which we treat as a Principle V "storage/sizing" knob (like node/replica sizing), not a shape difference. Prod can point at a larger extract via config. This is a conscious call flagged for plan review; if strict same-data parity is required, pin one extract everywhere. Captured as a task-level spike (validate extract size, cold-start time, and volume mount on AKS).

---

## R2 — Proximity distance strategy (no PostGIS; cached city-to-city)

**Decision**: Store `Latitude`/`Longitude` (double) on **Canonical City**. Precompute and persist **city-to-city great-circle (haversine) distances** in a `CityDistance` cache table, computed **in C# at city-creation time** — never trig in SQL, never PostGIS. Proximity sort is then a plain indexed join + `ORDER BY DistanceKm`.

**Rationale**:
- The owner's insight: distance is a pure function of the `(cityA, cityB)` pair, and Jugger has a **small** set of cities (tens–low hundreds). Precomputing all pairs is cheap and bounded; a new city computes its distance to every existing city once, on insert.
- Keeps the stock `postgres:18.3-alpine` image unchanged — **no PostGIS, no `earthdistance`/`cube` extension, no image swap** (parity preserved, no new infra).
- Proximity-sorted browse becomes: `JOIN CityDistance d ON d.FromCityId = <homeCityId> AND d.ToCityId = entity.CityId ORDER BY d.DistanceKm, entity.Id`. Fully **DB-side, paginated** (Principle III), no per-row trig, no SQL-translation fragility.
- Entities whose city has no matching distance row (should not happen once populated) or a null city are simply **not joined → excluded** from the proximity view (FR-016), exactly as specified.

**Alternatives rejected**:
- **In-query haversine in EF LINQ** — relies on Npgsql translating `acos`/`cos`/`sin`; brittle and re-computed per row per request.
- **PostGIS + NetTopologySuite** — most capable, but changes the Postgres image and adds a heavy dependency for a marginal benefit at this scale.
- **Compute distances in memory per request** — works for the sort key but can't drive DB-side pagination ordering cleanly.

**Population & symmetry**: `CityDistance` stores the pair once with a normalized ordering (`FromCityId` < `ToCityId` by GUID) OR both directions; the plan uses **both directions** for a trivial single-sided join (write amplification is negligible at this volume). Backfill on city insert runs inside the EF execution strategy as a single retriable unit (Principle VII), with all inserts inside the delegate.

---

## R3 — Canonical City identity & de-duplication

**Decision**: A Canonical City is uniquely identified by its **provider place identity** from Photon: `(OsmType, OsmId)` (e.g. `N`/`W`/`R` + numeric id). Stored as a single `ExternalId` string (`"{type}:{id}"`) with a **unique index**. Selecting a city **upserts** by this key — first selection creates the row (plus its distance backfill), later selections reuse it.

**Rationale**: OSM ids are stable and globally unique per object, giving reliable de-dupe so two "Springfield"s stay distinct and one Köln is stored once. Matches the constitution's UUIDv7 `BaseEntity` PK while keeping the external key for reconciliation.

**Alternatives rejected**: `name + country + rounded coords` composite — fragile (rounding, transliteration, renames); OSM id is authoritative.

---

## R4 — City search surface (backend-proxied, select-to-persist)

**Decision**: The browser **never** calls Photon directly. Backend exposes `GET /api/cities/search?q=...` returning a list of **transient** `CityOptionDto` (not yet persisted). When a user *selects* a city, the owning resource's update (profile/team/event) sends the chosen **provider id**; the backend **re-resolves the canonical city server-side** (from the City cache, else from Photon by id), upserts it, and links the FK. Client-sent name/coords are display hints only and are **never trusted** as the stored values (Principle I, never-trust-the-client).

**Rationale**: Keeps the provider internal (resilience, privacy, no CORS/key exposure, cacheable), and makes the stored city authoritative regardless of a tampered client payload. Persisting only on selection (not on every keystroke search) keeps the City table clean (FR-022).

**Alternatives rejected**: browser→Photon directly (leaks provider, no resilience, no server validation); persist-on-search (pollutes City with every typed query).

---

## R5 — Resilience configuration for the geocoder

**Decision**: Register `AddHttpClient<IGeocodingClient, PhotonGeocodingClient>().AddJuggerHubResilience(config, "Geocoding")` with a new `Resilience:Outbound:Geocoding` section. **Retries are left enabled** for these calls.

**Rationale**:
- City search and city-by-id lookups are **GET / idempotent**, so retrying a transient fault is safe — this is the *opposite* of the email POST case, and the reason must be written where it lives (Principle VII). A duplicate GET costs nothing.
- Autocomplete needs snappy limits: short **attempt timeout** (~3s) and modest **total timeout** (~8–10s) so a slow provider degrades to the retryable "can't search right now" state fast, never a hung picker.
- Breaker **minimum throughput** tuned to real city-search volume (low, interactive) — not the library's 100/30s default that would never open (the 028 lesson).
- On breaker-open / exhausted retries: city search returns a graceful transient error; proximity sort falls back to default ordering; no unrelated flow blocks (FR-018, FR-019). No PII (the query string tied to a user) or secrets in resilience logs (FR-021).

**Alternatives rejected**: disabling retry (unnecessary — GET is safe); per-call `HttpClient.Timeout` or hand-rolled backoff (review-rejectable per Principle VII).

---

## R6 — No migration; drop-and-reseed

**Decision**: No data migration. Replace freeform columns outright:
- `PlayerProfile.Hometown` (string) → `HomeCityId` (nullable FK → City).
- `Team.City` (string) → `CityId` (nullable FK; still required for CityTeam, absent for Mixteam).
- `Event.City`/`Event.Country` (strings) → `CityId` (nullable FK); the event's `Street`/`PostalCode`/`VenueName` and legacy `Location` string handling are reviewed (legacy `Location` retained only if still needed by activity display).

A single EF migration adds `City` + `CityDistance`, drops the freeform columns, and adds the FKs. `DevDataSeeder` is updated to seed a handful of real cities and link the seeded profiles/teams/events. Because the owner confirmed only test data exists, no best-effort matching is needed.

**Rationale**: Clean model, no dual-read/back-compat code paths. Matches owner decision.

**Alternatives rejected**: keep-and-backfill (unnecessary complexity for test-only data).

**Cross-cutting touch**: `Hometown` appears in Profile, Search, Events, Marketplace, Parties, and Admin DTOs (+ their Angular models). All are updated in lock-step to expose the structured `{ city, country }` shape (frontend + backend ship together, per the 020 pattern).

---

## R7 — Frontend city picker & location display

**Decision**: New shared standalone Angular component **`jh-city-picker`** (separate `.html`/`.css`/`.ts` per Principle VI), debounced 250ms (matching `BrowseShellComponent`/onboarding), calling a new `CityService.search(q)`. Emits a selected `CityOption` (provider id + label). Reused by: onboarding city step, profile edit, team create/edit, event create/edit. A small `locationLabel(city)` helper renders `"City, Country"` everywhere location is shown.

**Rationale**: One component, one interaction, DESIGN.md-governed; reuses the established debounced-search idiom so it feels identical to existing search. Onboarding's team step (029) gains proximity ordering once the player has picked a city.

**Alternatives rejected**: per-screen bespoke inputs (duplication, inconsistent UX).

---

## Open items intentionally deferred to implementation

- Exact Photon image tag + regional extract file and its AKS volume/init strategy (spike — R1 risk).
- Whether legacy `Event.Location` free-text can be fully removed or must remain for historical activity rows (verify against `ActivityItemDto` readers during implementation).
- Confirmation of the `CityDistance` backfill cost at the seeded data volume (trivially small; measured in quickstart).
