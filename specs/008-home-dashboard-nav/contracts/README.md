# Contracts — feature 008 (Home dashboard & navigation)

`openapi.yaml` documents the **four new read endpoints**. There are **no new write endpoints** in this feature.

## New (this feature)
| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/v1/home` | required | Composite dashboard (viewer + capped modules); first-paint path |
| GET | `/api/v1/home/up-next` | required | Paginated full upcoming-events list ("see all") |
| GET | `/api/v1/home/news` | required | Paginated aggregated news feed ("see all") |
| GET | `/api/v1/profiles/me/teams` | required | Caller's memberships — drives nav "My team" + snapshots |

## Reused unchanged (no contract change)
| Method | Path | Purpose in Home |
|---|---|---|
| POST | `/api/v1/events/{id}/signup` | RSVP from Up next / Open to everyone (`{ teamId: null }` for individuals-mode) |
| DELETE | `/api/v1/events/{id}/signup/{signupId}` | Withdraw from an Up next item (target = `viewerSignupId`) |
| GET | `/api/v1/events` (Browse) | Tournaments "see all" (filter `type=Tournament`) |
| GET | `/api/v1/teams/{slug}` … | "Open team" / team space (existing) |

## Notes
- All reads are **entitlement-filtered server-side** (own sign-ups; member-team news/activity; connected-event news; public tournaments). The client cannot request another user's or non-member team's data — see `data-model.md` → *Entitlement predicates* and the `Home/` integration tests.
- Enums serialize as **names** (`EventType`, `SignupStatus`, `ParticipantMode`, `TeamRole`) via the existing global `JsonStringEnumConverter`.
- `HomeNewsDto.source` is open to a future `"league"` value with **no contract change** (deferred feature).
- Pagination uses the shared `PaginationRequest` (`skip`/`take`, take clamped ≤100) and `PagedResult<T>` envelope.
