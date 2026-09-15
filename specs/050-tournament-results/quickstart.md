# Quickstart: validating Tournament Results (050)

These are runnable scenarios that prove the feature end to end. The shapes are in [contracts/results-api.md](./contracts/results-api.md) and [contracts/tugeny.md](./contracts/tugeny.md); the rules are in [data-model.md](./data-model.md).

## Prerequisites

```powershell
docker compose up -d          # postgres, redis, mailpit — redis is required (rate limiter fails closed)
cd frontend; npm run start    # → http://localhost:4200
```

- Three test accounts: an **event creator** (A), a **team admin** of two teams (B, with teams *Alpha* and *Bravo*), and a **platform admin** (P, listed in `Admin:Emails`).
- Scenario 6 needs outbound internet to `tugeny.org` from the backend container. It is the only scenario that does; every automated test fakes Tugeny.

## Automated checks

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Results"
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Resilience"
cd frontend
npx nx test web --watch=false --testPathPattern="results|placements|tugeny|catalog-parity|join-actions"
npm run lint
npm run build
```

What they must cover:

| Area | Must cover |
|---|---|
| Ranking | FR-001 gates (not started / cancelled / not a tournament → 409). Ties normalised (1,2,3,3,4 submitted → 1,2,3,3,5 stored). 128 cap. Same team twice → 400. `teamId` of a team without a confirmed sign-up → 422. `AwaitingApproval` and `Waitlisted` teams do **not** count as signed up. A platform-admin connection to a team without a sign-up survives an event admin's re-save when the row `id` is kept, and is refused when the `teamId` moves to another row |
| Import | The finalized sample reproduces 20 placements and 78 matches with 9 draws (SC-002). `rankings: []` → 422 not-finalized. Body `null` → 404 on link. 500/timeout → 503 and saved results untouched (SC-006). A body over 4 MiB → not retried (exactly 1 transport call). A connection to a team without a confirmed sign-up → 422. Commit re-fetches (2 transport calls per commit) |
| Resilience | A twin of `CircuitBreakerTests` for `"Tugeny"`: breaker opens at 4 failed attempts. Retries 5xx/408/429, never 400/404. `client.Timeout == InfiniteTimeSpan`. No body in any log |
| Team history | Connected placements only. Newest first. Paged. A deleted team keeps its name on the event and drops out of history |
| Admin | Only `PlatformAdmin` (403 otherwise). Connecting touches exactly one row (SC-007). Disconnect restores `sourceName`. An erased connector shows the placeholder |
| Frontend | Paste parser: valid, ties, string positions, junk, duplicate names. Team-list builder: joined only, duplicates flagged. `join-actions` hidden after `endsAt`. Catalogue parity |

## Manual scenarios

### 1. Hand entry on a tournament run in JuggerHub (US1, SC-001)

1. A creates a **Teams** tournament starting in 5 minutes. B's *Alpha* and *Bravo* enter through parties, and A confirms both.
2. Before the start: *Manage event → Results*. The ranking editor says results can be recorded once the tournament has started. Linking Tugeny is available.
3. After the start, record the ranking:
   - 1 → *Alpha* (picked from the signed-up teams)
   - 2 → "Kiel Guests" (typed)
   - 2 → *Bravo* (a tie)

   Save.
4. **Expect**:
   - The event page shows the positions 1, 2, 2, the winner *Alpha*, "3 teams ranked", and "last changed" with the date.
   - *Alpha*'s page (`/t/alpha`) lists "1st of 3". "Kiel Guests" appears on no team page.

### 2. Paste Tugeny's export (US2)

1. Paste the committed real export fixture into *Results → Paste from Tugeny*. **Expect** a draft with every row unconnected and nothing saved yet.
2. Paste `hello`. **Expect** "We couldn't read that as a Tugeny ranking export", and the saved ranking unchanged.
3. Save a valid draft over the scenario-1 ranking. **Expect** a replace warning first.

### 3. Team list for Tugeny (US3, SC-004)

On the Results page, the team-list box shows *Alpha* and *Bravo*, one per line, with no pending or waitlisted teams. Paste it into Tugeny desktop's *Import Team Names*. **Expect** it to be accepted unchanged.

### 4. Past tournament plus platform-admin connection (Q1/Q2, US6)

1. A creates a Teams tournament dated **last month**. **Expect**:
   - no Join or Enter-party button anywhere on it (R12)
   - `POST …/signup` returns 409 (unchanged behaviour)
2. A records a ranking with "Alpha" typed as a plain name. **Expect** A cannot connect it: *Alpha* has no JuggerHub sign-up for this past event, so only a platform admin can connect it.
3. P opens *Admin → Results*. "Alpha" is listed unconnected with its event and date. P connects it to *Alpha*.
   - **Expect** it to appear on *Alpha*'s history.
   - **Expect** the same name on any **other** event to stay unconnected (SC-007).
4. A edits the ranking, keeps that row, and saves. **Expect** the connection kept, still attributed to P.

### 5. Authorization spot checks

- B, as a non-admin of the event: `PUT …/results/ranking` returns 403. The UI shows no editor.
- B: `PUT /admin/results/placements/{id}/team` returns 403.
- A signed-out browser: every results route returns 401 (026 unchanged).

### 6. Real Tugeny import (US4, SC-002): needs internet

1. Create a past tournament for 20–21 Sept 2024 and link `https://tugeny.org/tournaments/25-deutsche-meisterschaft/all-teams`. **Expect** "25. Deutsche Meisterschaft · 21 Sept 2024" to confirm.
2. *Import results*. **Expect**:
   - a preview of 20 placements and 78 matches, every one unconnected
   - after committing: the ranking (1 Seven Sins … 20 Zonenzwerge) and matches grouped by Group 1–5 plus Knockout, with 9 draws shown as draws
   - scores in the mono face
   - a "Results from Tugeny" line linking the tournament
3. Edit one placement by hand. **Expect** "Imported from Tugeny, edited since".
4. Link `26-deutsche-meisterschaft` on a second event and import. **Expect** "not finalized in Tugeny yet", with a pointer to paste and hand entry.

### 7. Tugeny unavailable (FR-018)

Set `Tugeny__BaseUrl=http://localhost:9` and restart the backend. Import. **Expect**:
- "Tugeny can't be reached right now…" within about 30 s
- existing results untouched
- after 4 failed attempts, further clicks within 60 s fail immediately (the breaker is open)

## Browser walk (owner rule; Gate 7)

Real browser, **German**, at **375 px and desktop**, with screenshots of:
- the event page with results (winner callout, ties, matches with scores)
- the results editor (the one coral CTA is Save)
- the paste dialog error
- the Tugeny card before and after linking
- the team-list box with a duplicate warning
- the team page history, with load more
- the admin results list and team picker, including the **fifth admin tab** at 375 px (R15)

Fill `checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` against the diff.
