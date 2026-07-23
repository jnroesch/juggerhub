# Quickstart: Validate Authenticated-Only Access + Opt-In Public Profiles

End-to-end validation for feature 026. Assumes the standard local stack
(docker-compose: Postgres, Mailpit, API, web) is up and the EF migration
`AddProfileVisibility` has been applied.

## Prerequisites

- Backend + frontend running locally (`docker compose up` per the repo's run steps).
- Two registered accounts: **A** (will have a public profile) and **B** (a signed-in
  viewer). Note A's handle, e.g. `@alice`.
- A browser (or `curl`) with no auth cookie for the anonymous checks.

## 1. Anonymous access is denied (Story 1 / SC-001, SC-002)

With **no** auth cookie:

- `GET /api/v1/teams` → **401**
- `GET /api/v1/teams/{slug}/public` → **401**
- `GET /api/v1/events` → **401**
- `GET /api/v1/events/{id}` → **401**
- `GET /api/v1/events/{id}/participants?group=joined` → **401**
- `GET /api/v1/profiles` (browse) → **401**

Frontend (signed out): opening `/t/{slug}`, `/events/{id}`, `/browse/teams`,
`/browse/events`, `/browse/players` redirects to **sign-in** — no content flashes.

**Pass**: every call is 401; every route redirects.

## 2. Authenticated access is unchanged (SC-003)

Signed in as **B**, repeat all of the above via the app → all load normally
(teams, events, browse, any profile). No regression versus pre-026 behavior.

## 3. Public-profile opt-in round-trip (Story 2 / SC-005)

1. Sign in as **A**, open profile/account settings, toggle **Make my profile
   public** on, save. (`PUT /api/v1/profiles/me` with `isPublic: true`.)
2. Sign out. Open `/u/alice` (or `GET /api/v1/profiles/alice`) **anonymously** →
   **200** with card + team memberships + activity.
   - `GET /api/v1/profiles/alice/avatar` → **200** (if an avatar is set).
   - `GET /api/v1/profiles/alice/activity` → **200**.
3. From the anonymous public profile, click one of A's listed teams → redirected to
   **sign-in** (FR-014); team content never shows.
4. Sign in as **A** again, toggle public **off**, save.
5. Anonymously reload `/u/alice` → behaves as **not found** (see §4).

**Pass**: visibility flips anonymous access on/off within one reload; team links from
a public profile still require auth.

## 4. No existence oracle (SC-004)

Anonymously compare:

- `GET /api/v1/profiles/alice` while A is **private** →
- `GET /api/v1/profiles/does-not-exist-zzz` →

Both must return an **identical** `404` ProblemDetails (same status, title, detail).
No wording, header, or timing distinguishes "private" from "missing".

**Pass**: responses are byte-for-byte equivalent apart from any request-id.

## 5. Default is private (FR-017/FR-018)

- A freshly registered account **C**, never toggled, viewed anonymously at its handle
  → **404** (private by default).
- After the migration, spot-check an existing (pre-026) profile anonymously → **404**
  (backfilled to private).

**Pass**: nobody is anonymously visible without an explicit opt-in.

## 6. Banned owner never leaks (FR-019)

With A public, have an admin **ban** A, then view `/u/alice` both anonymously and as
signed-in **B** → **404** in both cases (global ban filter wins over `IsPublic`).

**Pass**: a public flag cannot resurface a banned profile.

## 7. Discovery is direct-link only (Story 3 / SC-006)

Signed out, confirm there is no anonymous players/teams/events browse reachable
(all under §1's 401 / redirect). A public profile is reachable only by entering its
`/u/{handle}` link.

**Pass**: no anonymous enumeration/search surface exists.

## Automated coverage (where these assertions live)

- **Backend integration**: anonymous-401 for teams/events/profiles reads; visibility
  gate matrix; no-oracle equality; migration default; a route-enumeration allowlist
  test proving only intended endpoints stay anonymous.
- **Frontend**: guard specs for the newly-guarded routes; e2e signed-out redirect +
  public-profile opt-in flow.
