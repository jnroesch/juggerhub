# Contracts — Search / Browse (feature 007)

[`openapi.yaml`](./openapi.yaml) describes the HTTP surface for discovery. It is a **design contract** for `/speckit-tasks` and implementation, not a generated artifact — keep it in sync with `TeamsController` / `EventsController` / `ProfilesController` as they land.

## Conventions (inherited)

- Base path `**/api/v{version}**` (`v1`). JWT in an **httpOnly cookie** (bearer scheme); no tokens in `localStorage`.
- Lists use `skip`/`take` query params and return a **`PagedResult<T>`** envelope (`items`, `totalCount`, `skip`, `take`); `take` is capped server-side (≤ 100, default 20). **`take=0`** returns empty `items` with the real `totalCount` — this powers the filter panel's live **"Show N"** count.
- Enums serialize as **names** (`"Tournament"`, `"QTip"`, `"NameAsc"`).
- Errors are RFC7807 **`ProblemDetails`**; no stack traces/secrets. Typical: `400` malformed, `401` unauthenticated (writes only), `403` not a team admin (`PATCH /teams/{slug}`), `404` unknown team.
- **Browse never errors on bad filter input** — unknown/invalid filter or sort values fall back to defaults rather than `400`.

## Authorization map (server-enforced)

| Surface | Who |
|---|---|
| `GET /teams`, `GET /events`, `GET /profiles` (browse) | **Anonymous** (public card fields only) |
| `PATCH /teams/{slug}` (BeginnersWelcome) | **Team admin** of that team |
| `PUT /profiles/me` (AppearInSearch + existing fields) | **Authenticated**; mutates only the caller's own profile |

## Invariants surfaced by the contract

- **Player opt-in (privacy)** — `GET /profiles` returns **only** `AppearInSearch = true` players, for every query/filter/sort and both anonymous and authenticated callers. The opt-in is **not** a query parameter and cannot be bypassed (spec FR-042 / SC-003).
- **Cancelled events excluded** — `GET /events` never returns `Status = Cancelled`, regardless of `hidePast`.
- **Hide-past default** — `GET /events` hides ended events unless `hidePast=false`.
- **Team active (derived, default on)** — `GET /teams` with `activeOnly=true` (default) returns only teams with an event participation in the last 12 months.
- **Public fields only** — no card exposes emails, fee/IBAN, rosters, invitations, or other admin-only/internal data.
- **Stable paging** — every sort has a stable secondary key (entity `Id`) so `skip`/`take` never drop or duplicate a row across pages.

## Notes for the frontend

- `logoInitial` on `TeamCardDto` may be computed client-side from `name` instead of read from the API — either is acceptable; the card contract includes it for convenience.
- `hasAvatar` on `PlayerCardDto` gates whether the row fetches the avatar image from the existing `GET /profiles/{handle}/avatar`; the browse response carries no image bytes.
- Row links: player → `/u/{handle}` (public), event → `/events/{id}` (public), team → `/t/{slug}` (auth-guarded today — see plan research §7).
