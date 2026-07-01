# Quickstart — Player Profile & Public Share Link

End-to-end validation that the feature works. Docker-only workflow (no host runtimes). Assumes the 002 auth stack is functioning.

## Prerequisites

- `.env` present (see `.env.sample`); `docker compose` available.
- Stack up: `docker compose up --build` → backend `:8080`, frontend `:3000`, Postgres `:5432`, Mailpit `:8025`.
- Migrations auto-apply on startup (`AddProfilesAndEvents` creates the profile/event tables).

## Scenario A — Register with an immutable handle (US1)

1. Open `http://localhost:3000/register`.
2. Fill email + a policy-valid password; type a handle, e.g. `nik-berlin`. Availability shows **available** as you type; a taken/reserved/malformed handle shows **unavailable** with a reason.
3. Submit → neutral "check your email" message. Verify via Mailpit (`:8025`) and sign in.

**Expected**: account created with the handle permanently attached. Re-registering the same handle (different email) is rejected. There is no UI or API to change the handle afterward.

**Contract checks**: `POST /auth/register` with a duplicate handle → 409/400; `GET /auth/handle-available?handle=admin` → `available:false` (reserved).

## Scenario B — Edit my profile (US3)

1. Signed in, open `http://localhost:3000/profile`.
2. Set display name, hometown, a short description; upload a PNG/JPEG/WebP avatar (≤ ~2 MB); toggle several pompfen (e.g. Stab, Schild, Läufer). Save.

**Expected**: values persist; reload shows them. An oversized or non-image file is rejected with a clear message and the previous avatar is untouched.

**Contract checks**: `PUT /profiles/me` (200, returns `OwnerProfileDto`); `PUT /profiles/me/avatar` with a `.txt` renamed `.png` → 400 (magic-byte sniff); `PUT /profiles/me` with a `handle` field → handle unchanged.

## Scenario C — View & share the public profile (US2)

1. **Sign out.** Open `http://localhost:3000/u/nik-berlin`.
2. Confirm the page shows display name, `@nik-berlin`, hometown, avatar, description, **only the selected** pompfen, and recent activity — and offers **Copy link**.
3. Open `http://localhost:3000/u/does-not-exist` → friendly "profile not found".

**Expected / SC-002 (critical)**: inspect the network response for `GET /api/v1/profiles/nik-berlin` and confirm it contains **no** `email` and no account/security fields. Copy-link yields `http://localhost:3000/u/nik-berlin`.

**Contract checks**: `GET /profiles/{handle}` → `PublicProfileDto` (no email); unselected pompfen absent; unknown handle → 404 generic ProblemDetails.

## Scenario D — Recent activity from the minimal events model (US4)

1. Seed a couple of events + participations for the profile (dev seed / SQL), e.g. "Sommerturnier Berlin" (Aug '25, with Team A), "Liga-Spieltag Hamburg" (Jun '25, with Team B).
2. Reload the profile (public and owner).

**Expected**: activity lists those events **newest-first**, each with name, date/month, location, and "with <Team>". A profile with no participation shows a friendly empty state.

**Contract checks**: `GET /profiles/{handle}/activity?take=1` returns a `PagedResult` with `totalCount` > items length (bounded); ordering is `Event.Date` desc.

## Scenario E — Authorization & never-trust-the-client

- `GET/PUT /profiles/me` without a valid session → 401.
- Attempt to edit via API while authenticated as user X but targeting user Y's profile → refused (endpoints only ever act on the authenticated subject).
- Teams/Badges render as empty **stub** sections and imply no data.

## Responsiveness (SC-008)

Validate `/u/:handle` and `/profile` at phone (~375px) and desktop (~1280px) viewports — layout, empty/loading/error states legible per `DESIGN.md` (Playwright runs both).

## Automated validation

- Backend: `docker compose -f docker-compose.test.yml ...` runs the xUnit integration suite (register+handle, immutability, owner-only edit, public-DTO-omits-email, avatar validation, activity ordering/pagination).
- Frontend: Jest unit (ProfileService, handle validator, pompfe selector) + Playwright `profile.spec.ts` (register → edit → signed-out public view; asserts no email on the wire), desktop + mobile.
