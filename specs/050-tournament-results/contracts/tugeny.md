# Contract: What JuggerHub reads from and hands to Tugeny (050)

Tugeny is an external, independently run product. These are the **only** shapes this feature depends on. Each has a real sample or a binary-derived source ([research.md](../research.md) R1 and R2).

---

## 1. Tugeny's data interface (read by the server only)

Base: `Tugeny:BaseUrl` (default `https://tugeny.org/`), path prefix `api/persistent/`. GET only, no credentials, JSON. Data is published under the MIT licence.

| Call | Used for | Samples |
|---|---|---|
| `tournamentsBySlug/{slug}` | Resolve and confirm a link | [finalized](./tugeny-samples/tournament-by-slug.finalized.json) · [not finalized](./tugeny-samples/tournament-by-slug.not-finalized.json) · [unknown → `null` with HTTP 200](./tugeny-samples/tournament-by-slug.unknown.json) |
| `rankingsByTournamentId/{id}?returnType=json` | Ranking | [finalized](./tugeny-samples/rankings.finalized.json) (object `"1".."N"`) · [not finalized](./tugeny-samples/rankings.not-finalized.json) (`"rankings": []`) |
| `matches?tournamentIds={id}&returnType=json` | Matches | [finalized](./tugeny-samples/matches.finalized.json) (78 matches, 9 draws) · [not finalized](./tugeny-samples/matches.not-finalized.json) (`[]`) |

**Fields read**:
- **Tournament**: `id`, `name`, `slug`, `startdate` (ISO with offset; take the date part).
- **Ranking**: key → `position`; `team_id`, `team_name`.
- **Match**: `group`, `name`, `timestamp` (ordering only), `first_team_id`, `second_team_id`, `first_team`, `second_team`, `victorious_team_id` (null means a draw), `score_total` (`"5:1 - 2:5 - 0:5"`).

Everything else is ignored. An unknown key is never an error.

**Tolerance rules**:
- **Unusable body**: non-JSON, wrong root type, or a missing required field. The link fails with 503 / "Tugeny can't be reached", and nothing is saved (FR-018).
- **Unparseable `score_total`**: empty score arrays; the match is still imported.
- **Body over 4 MiB**: permanent failure, not retried (R5).

## 2. Tugeny's ranking export (pasted by an event admin, parsed in the browser)

Produced by Tugeny desktop *Export → Export Ranking for JTR* (`JtrJsonService::composeJson`). It exists only when every rank is decided.

```jsonc
[ { "name": "Seven Sins", "position": 1 }, { "name": "Jugger Basilisken Basel", "position": 2 }, … ]
```

**Parser contract** (`parseTugenyRankingExport(text) → { placements } | { error }`, pure, unit-tested):

**Accepted**:
- a JSON array of 1–128 objects
- `name`: a non-empty string after trimming, ≤ 80 characters
- `position`: a positive integer, or a string of digits
- any key order, any whitespace; unknown keys ignored
- ties (equal positions)

**Rejected with one friendly message**: anything else, including duplicate names. Tugeny forbids them, so a duplicate means the text is not a Tugeny export.

**Output**: rows in `(position, source order)`. **Every row starts unconnected** (FR-011). The admin then connects rows to signed-up teams where they apply, and the result is saved through `PUT …/results/ranking`. The server re-validates everything (Principle I).

**To confirm during implementation**: commit one real export from the bundled `WCC_2020+_finished.tur` as a parser fixture.

## 3. The team list for Tugeny's *Import Team Names* (built in the browser)

Tugeny's dialog accepts "one team name per row in plain text" (help text in the binary) or JTR JSON `[{"name":…,"status":…}]`. JuggerHub produces the **plain-text** form:

```text
Rigor Mortis
Seven Sins
Munich Monks
```

- **Source**: `GET /events/{id}/participants?group=joined`, every page, teams only (R4, R13).
- **Order**: sign-up order (`joinedAt`).
- **Duplicates** (case-insensitive, trimmed) are listed before the copy action, because Tugeny refuses them ("It is not allowed that two teams have the exact same name").
- **Individuals-mode events** have no team list; the action is not offered.

## 4. Links JuggerHub builds (never from input)

| Link | Shown |
|---|---|
| `https://tugeny.org/tournaments/{slug}/live-view` | On the event page while the event has not ended (FR-015) |
| `https://tugeny.org/tournaments/{slug}/all-teams` | The provenance line of imported results (R14 attribution) |
