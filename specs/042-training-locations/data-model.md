# Phase 1 Data Model: Structured Locations for Trainings

**Feature**: 042-training-locations | **Date**: 2026-08-04

Reference model: `Event` (feature 030). Nothing in `Cities` / `CityDistances` / `CityReference`
changes — trainings simply become a new referrer.

---

## 1. `Training` (existing entity, `backend/Entities/Training.cs`)

### Added

| Property | Type | Null | Max length | Notes |
|---|---|---|---|---|
| `VenueName` | `string?` | yes | 120 | Optional even for in-person. Mirrors `Event.VenueName`. |
| `Street` | `string?` | yes | 160 | Required when `LocationKind == InPerson`, enforced in the service. |
| `PostalCode` | `string?` | yes | 20 | Required when `LocationKind == InPerson`, enforced in the service. |
| `CityId` | `Guid?` | yes | — | FK → `Cities.Id`, `DeleteBehavior.Restrict`. Required when in-person. |
| `City` | `City?` | yes | — | Navigation. |

### Changed in meaning (not in shape)

| Property | Was | Becomes |
|---|---|---|
| `Location` (`string?`, max 300) | Admin-typed free text, required for in-person | **System-derived legacy label** — `"City, CountryName"` for in-person, `null` for virtual. Never assigned from a request. Retained so pre-042 rows keep a readable label through the display fallback. |

`Location` deliberately stays **nullable**, unlike `Event.Location` which is `IsRequired()` and
holds `"Online"` for a virtual event. A virtual training keeps `Location = null` because
`RowProjection` already nulls the location for a virtual session and the client renders "Online"
from `LocationKind`. Making it required would change virtual rendering for no gain.

### Invariants

- `LocationKind == InPerson` ⟹ `Street != null && PostalCode != null && CityId != null`
- `LocationKind == Virtual` ⟹ `VenueName == Street == PostalCode == CityId == null` **and**
  `VirtualLink != null`
- `Location` is only ever written by the legacy-label helper.

---

## 2. `TrainingSession` (existing entity, `backend/Entities/TrainingSession.cs`)

### Added

| Property | Type | Null | Max length | Notes |
|---|---|---|---|---|
| `VenueNameOverride` | `string?` | yes | 120 | |
| `StreetOverride` | `string?` | yes | 160 | |
| `PostalCodeOverride` | `string?` | yes | 20 | |
| `CityIdOverride` | `Guid?` | yes | — | FK → `Cities.Id`, `DeleteBehavior.Restrict`. **The block marker.** |
| `CityOverride` | `City?` | yes | — | Navigation. |

### Changed in meaning

| Property | Becomes |
|---|---|
| `LocationOverride` (`string?`, max 300) | System-derived legacy label for the session, same rule as `Training.Location`. Never assigned from a request. |

### ⚠ The block rule (FR-007)

Every other override on this entity resolves as `X ?? Training.X`. **The address does not.** It
resolves as one block:

```csharp
// hasOwnAddress — the session carries its own address exactly when it carries its own city.
s.CityIdOverride != null ? s.VenueNameOverride  : s.Training.VenueName
s.CityIdOverride != null ? s.StreetOverride     : s.Training.Street
s.CityIdOverride != null ? s.PostalCodeOverride : s.Training.PostalCode
s.CityIdOverride != null ? s.CityOverride       : s.Training.City
```

Writing this as `s.VenueNameOverride ?? s.Training.VenueName` is a defect: a session relocated to a
venue with no name would render the *series'* venue name against the *session's* street, and a
street-only override would render under the series' city.

This expression must appear identically in `TrainingSeriesService.RowProjection`, the agenda
projection, and `TrainingSessionService`'s detail projection. It is EF-translatable as written.

### Invariants

- `CityIdOverride != null` ⟺ the session carries its own address
- effective kind `Virtual` ⟹ all four override columns are `null`
  (enforced after the request is applied — see §5)
- `CityIdOverride != null` ⟹ `StreetOverride != null && PostalCodeOverride != null`

---

## 3. EF configuration (`backend/Data/AppDbContext.cs`)

Added to the existing `Training` block (~line 870):

```csharp
entity.Property(t => t.VenueName).HasMaxLength(120);
entity.Property(t => t.Street).HasMaxLength(160);
entity.Property(t => t.PostalCode).HasMaxLength(20);

entity.HasOne(t => t.City)
    .WithMany()
    .HasForeignKey(t => t.CityId)
    .OnDelete(DeleteBehavior.Restrict);
```

Added to the existing `TrainingSession` block (~line 890), the same four lengths on the
`…Override` columns plus:

```csharp
entity.HasOne(s => s.CityOverride)
    .WithMany()
    .HasForeignKey(s => s.CityIdOverride)
    .OnDelete(DeleteBehavior.Restrict);
```

`Restrict` matches the event and team precedent (`AppDbContext.cs:249-252`): a city referenced by a
training cannot be deleted. No new index — `CityId` lookups in this feature are navigations from an
already-filtered training/session set. A proximity search (out of scope) may add one later.

---

## 4. Migration

`AddTrainingStructuredLocations` — eight `AddColumn` calls, two `CreateIndex` + `AddForeignKey`
pairs. **No data migration.** Existing rows get nulls and keep their free-text `Location`, which
the display fallback still renders. `Down` drops the FKs and columns; no data is recoverable
because none is written by the migration.

---

## 5. Write-path state transitions

### Create (`TrainingSeriesService.CreateAsync`)

1. Validate name / times / recurrence as today.
2. `StructuredAddress.Resolve(kind, venue, street, postal, virtualLink)` → reason ⟹ `Invalid`.
3. `StructuredAddress.ResolveCityAsync(cities, kind, request.Location, ct)` → reason ⟹ `Invalid`
   (unknown city id ⟹ "That city could not be found.").
4. Assign the four address columns (all `null` when virtual) and `Location = legacy label`.

### Series edit (`TrainingSeriesService.EditSeriesAsync`)

Address is replaced **as a block** when `request.Location` is present; otherwise untouched. A
change to `LocationKind` re-runs steps 2–4 with the new kind, which clears the address on a switch
to virtual. Upcoming non-detached sessions inherit automatically — no session rows are rewritten.

### Single-session edit (`TrainingSessionService.EditSessionAsync`)

Order matters:

1. **Freeze** — extend the existing `??=` block (`TrainingSessionService.cs:76-80`) to the four
   address columns, so the detached session stops tracking the series.
2. **Apply** the request's address block, if present, after validating street + postal + city.
3. **Virtual guard** — if the effective kind is now `Virtual`, set all four address overrides and
   `LocationOverride` to `null`.
4. `Detached = true` (unchanged).

Consequence, intended: after a *time-only* single-session edit of an in-person training,
`CityIdOverride` is non-null even though the admin never touched the address. The session is
detached; its address is now its own. This is 018's existing semantics, not a new rule.

### Clearing a relocation (FR-009)

Setting the four override columns back to `null` returns the session to the series address — the
block marker goes false and the projection falls through. No separate flag to reset.

---

## 6. Read shape

| DTO | Location members after this feature |
|---|---|
| `TrainingSessionRowDto` | `LocationKind`, **`LocationLabel`**, `VirtualLink` |
| `AgendaSessionDto` | `LocationKind`, **`LocationLabel`**, `VirtualLink` |
| `TrainingSessionDetailDto` | `LocationKind`, `VenueName`, `Street`, `PostalCode`, `LocationDto? Location`, `VirtualLink`, **`LocationLabel`** |

`LocationLabel` is built server-side by the existing shared
`HomeProjections.LocationLabel(city, venue, legacy)` — city → venue → legacy text. Computing it
once on the server is what makes SC-003 ("identical to an event at the same address") structural
rather than a convention two client templates have to keep.

List DTOs deliberately do **not** carry `Street`/`PostalCode`: a list never renders them, and
events' list rows don't either. The detail DTO carries the full structured block because the edit
forms prefill from it.

The free-text `string? Location` member is **removed** from all three read DTOs and from the
frontend models.

---

## 7. Entity relationship summary

```text
Team 1 ──< Training 1 ──< TrainingSession
                │                │
                │ CityId         │ CityIdOverride
                │ (Restrict)     │ (Restrict)
                ▼                ▼
              City ◄─────────────┘

City is also referenced by Event.CityId, Team.CityId and PlayerProfile.HomeCityId — unchanged.
```
