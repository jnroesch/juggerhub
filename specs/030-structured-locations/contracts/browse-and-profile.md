# Contract: Browse proximity/country + structured location DTOs

## Shared location read shape

Everywhere a location is returned, it uses this nullable object (null ⇒ no location set):

```jsonc
"location": {
  "name": "Köln",
  "region": "North Rhine-Westphalia", // nullable
  "countryName": "Germany",
  "countryCode": "DE",                // nullable
  "label": "Köln, Germany"            // FR-010 display string
}
```

This replaces the previous string fields: `PlayerCardDto.Hometown`, `TeamCardDto.City`, `EventCardDto.City`, and the corresponding profile/team/event detail DTOs. All Angular models (`profile.models.ts`, `search.models.ts`, `event.models.ts`, `market.models.ts`, `party.models.ts`, `admin.models.ts`) adopt the same shape.

---

## Team browse — `GET` team browse endpoint (TeamsController)

Extends `TeamBrowseQuery` (feature 007):

| Param | Before | After |
|-------|--------|-------|
| `city` | free-text substring filter | **removed** (replaced by `country` + proximity) |
| `country` | — | **new**: ISO code or country name; exact filter (FR-015) |
| `sort` | `NameAsc` \| `PlayerCountDesc` \| … | **+ `Proximity`** (opt-in; FR-014) |

**`sort=Proximity` semantics**:
- Requires the caller to have a `HomeCityId` (derived server-side from the authenticated user — **not** a client param). If the caller has no home city ⇒ **`409 Conflict`** with a generic message the UI turns into "set your city to sort by distance" (US4 scenario 4). The endpoint does **not** silently fall back to another sort — the UI decides whether to prompt or re-request a default sort.
- Joins `CityDistances` from the caller's home city; **nearest-first**, `ThenBy(Id)`; **no radius cut-off** (FR-011).
- Mixteams (`CityId = null`) are **excluded** from this view (FR-016); visible under other sorts.
- Remains paginated via `PagedResult<TeamCardDto>` (Principle III).

**Default (any non-Proximity sort)**: unchanged behavior; `HomeCityId` not required (FR-014).

---

## Event browse — `GET` event browse endpoint (EventsController)

Extends `EventBrowseQuery` analogously:

| Param | Before | After |
|-------|--------|-------|
| `city` | free-text substring | **removed** |
| `country` | — | **new** exact filter |
| `sort` | `StartsAtAsc` \| … | **+ `Proximity`** |

**`sort=Proximity` semantics**: as teams, but the event's `CityId`; **virtual/location-less events (`CityId = null`) are excluded** from the proximity view entirely (FR-016, clarified 2026-07-25) — they reappear under `StartsAtAsc`.

---

## Onboarding team step (029) — proximity default

The onboarding "find your team" step calls the same team browse. When the player has just selected a home city, it requests `sort=Proximity` so nearer teams lead (FR-013). If the player has **no** city or proximity can't be computed (geocoder down), it falls back to the existing default (beginner-friendly `NameAsc`) — the step never blocks or errors (constitution VII; mirrors the 029 "never trap a new player" rule).

---

## Profile / Team / Event write DTOs

Each update DTO embeds the selection fragment from [cities.md](./cities.md):

- **Profile** (`updateMine`): `hometown` (string) → `location: { cityExternalId }`.
- **Team** create/update: `city` (string) → `location: { cityExternalId }`; still required when `type = CityTeam`.
- **Event** create/update: `city`/`country` (strings) → `location: { cityExternalId }` for `LocationKind.InPerson`; ignored/rejected for `Virtual`.

Server re-resolves and links per cities.md; client coordinates are never persisted.
