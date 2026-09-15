# Research: Tournament Results (050)

Phase 0 for [plan.md](./plan.md). Every decision below was made by reading the code or the external system, not by assumption. Where the code contradicted the spec, the code is quoted.

---

## R1 — The formats Tugeny speaks (the spec's open item, resolved)

**Decision**:
- The **ranking export** (*Export → Export Ranking for JTR*) is a JSON **array of objects with the keys `position` and `name`**.
- The **team import** (*Import Team Names*) accepts that JSON family as `[{"name": …, "status": …}]`, or **plain text with one team per row**.
- JuggerHub **parses** the first and **produces** the second as plain text.

**How it was established**: Tugeny's GPL desktop build (`Tugeny_2.4_linux.tar.xz`, SourceForge) was read, not run. The ELF symbol table locates `JtrJsonService::composeJson(Tournament const&)`, `::extractNames(QString const&)` and `::isConsideredAsJtrJson(QString const&)`. Listing only the `.rodata` strings each function references gives:

| Function | Strings it references |
|---|---|
| `composeJson` (the ranking export) | `"position"`, `"name"`, `"The tournament must have a ranking!"`, `"All teams in the ranking must be determined!"` |
| `extractNames` (the team import) | `"name"`, `"status"`, `"participant"`, `"The JSON must be an array!"`, `"The JSON array must contain only objects!"`, `"The value 'name' must exist for every team!"`, … |
| `isConsideredAsJtrJson` | `"[{\"name\":"` (a prefix test) |

The dialog's own help text confirms both paste targets on the JTR side ("*Administrate teams → Export Team*" and "*Insert results → Import Results*"). **No JTR page was read to learn this.** The spec forbids it (FR-019), and the binary makes it unnecessary.

**Consequences**:
- **The export only exists for a fully decided ranking.** `composeJson` refuses with "All teams in the ranking must be determined!". So a paste can never deliver the "final not played" case. The organizer either enters the final in their local Tugeny first or uses hand entry (FR-001). The edge case in the spec is therefore served by hand entry, not by paste.
- **The parser must be tolerant of details the binary cannot show.** Qt's `QJsonObject` writes keys in sorted order and `QJsonDocument::toJson()` indents by default, but whether `position` is a number or a string cannot be read statically. So the parser:
  - accepts a number or a numeric string
  - accepts any key order and whitespace
  - ignores unknown keys
  - rejects everything else (FR-010)
- **A real export is still captured during implementation** (a task opens the bundled `WCC_2020+_finished.tur` in the Windows build) and committed as a test fixture. It confirms R1 rather than establishing it.

**Alternatives considered**:
- *Guessing from JTR's public pages*: forbidden (FR-019).
- *Asking the owner to produce a sample before planning*: it would have blocked the plan on a manual step that the binary already answers.

---

## R2 — Tugeny's data interface, measured

**Decision**: Import reads three endpoints of `https://tugeny.org/api/persistent/`:

| Purpose | Endpoint |
|---|---|
| Resolve a link | `tournamentsBySlug/{slug}` |
| Ranking | `rankingsByTournamentId/{id}?returnType=json` |
| Matches | `matches?tournamentIds={id}&returnType=json` |

All three were surveyed across **every** finalized tournament (46, on 2026-09-15). Real responses are committed under [contracts/tugeny-samples/](./contracts/tugeny-samples/).

**Measured facts the client must handle**:

| Fact | Measured | Consequence |
|---|---|---|
| Unknown slug | **HTTP 200, body `null`** | "Not found" is decided on the body, never the status |
| Unfinalized tournament | `tournamentsBySlug` **succeeds**; rankings `{"rankings": []}`; matches `[]` | Link succeeds; import says "not finalized" (FR-016) |
| `rankings` shape | an **object** keyed `"1".."N"` when finalized, an **empty array** when not | Read with `JsonDocument`, never a fixed DTO |
| Ties | **never**: keys are unique, ranks contiguous `1..N` in all 46 | Ties arrive only through hand entry or paste |
| Largest ranking | 72 teams | Placement cap 128 (R8) |
| Largest match list | 528 matches, 245 880 bytes | Response cap 4 MiB (R5) |
| Match sides in the ranking | 7 020 of 7 020 | Every match side resolves to a placement of the same import |
| Draws | 318 of 3 510 matches have `victorious_team_id: null`, **all** with equal sets | `null` winner = draw, shown as such (FR-020) |
| Score text | `"5:1 - 2:5 - 0:5"`; 1–5 sets; every one of 3 510 matches `n:n( - n:n)*` | Parse to two `int[]`; unparseable → empty, never a failed import |
| Stage | `group` set for group matches (`"Group 1"`), **`null` for all knockout** matches; round is only in `name` (`"QF1 1-8"`, `"F 1-2"`) | Stage = `group ?? null`; knockout shown under one heading |
| Order | matches are **not** time-ordered in 45 of 46 tournaments | Sort by `timestamp` (`"yyyy-MM-dd HH:mm"`, local, no zone) at import; store only the resulting order |
| Timestamps | local wall-clock without a zone | **Not stored**: FR-020 needs order, not time, and storing a zoneless time would be the first `timestamp without time zone` column in the schema |

**Alternatives considered**:
- *Scraping `/tournaments/{slug}/tournament-tree`* (an HTML-embedded bracket JSON that also covers unfinalized tournaments): rejected. It is undocumented and can change without notice, and the paste path (R1) already serves unfinalized tournaments through a format Tugeny itself supports.

---

## R3 — Where results live

**Decision**: A new aggregate, **`TournamentResult`**, one per event (unique `EventId`). It owns **`TournamentPlacement`** and **`TournamentMatch`** rows and carries the Tugeny link and provenance. See [data-model.md](./data-model.md).

**Rationale**:
- **Not columns on `Event`.** The link exists before any result (FR-015 live link), and a placement list is a collection. `Event` already carries ~35 columns across five features.
- **Not `EventParticipation`.** It is per-*profile* (unique `(ProfileId, EventId)`), has **no production write path** (only tests insert rows), and feeds profile activity plus the team "active" flag (`TeamService.cs:194`, `TeamSearchService.cs:46`). Writing results into it would switch those on as a side effect. Nothing in this feature writes it.
- **The link lives on the result row, not on its own table.** It is 1:1 with the event, and its lifetime equals the result's. Removing the link nulls four columns (FR-014: "removing a link MUST NOT delete saved results").

---

## R4 — Which teams an event admin may connect: "signed-up teams" (FR-004a)

**Terminology**: throughout this plan a **signed-up team** is a team with a confirmed JuggerHub sign-up for the event. That is a narrower set than "teams that played". Every team of a past tournament added after the fact, teams whose sign-up ran through another channel, and guest teams added on the day all played without one. Their placements are connected by a platform admin (FR-004b), who checks by hand that the team really played.

**Decision**: An event admin may connect a placement to team *T* only if an **`EventSignup` exists with `EventId = event`, `TeamId = T`, `Status = Joined`**.

**Rationale** (read from the code):
- There is **no "confirmed" predicate** in the backend. The only computed one is *occupied*, which includes `AwaitingApproval` (`EventCapacity.cs:21-25`).
- For paid events every entry starts `AwaitingApproval` and becomes `Joined` only on admin approval (`EventSignupService.cs:114-116`, `PartyService.cs:353-355`). The frontend labels `Joined` "confirmed" (`event-detail.component.html:55`). So `Joined` means confirmed, and for paid events also paid.
- Team entries exist only through party apply (direct team sign-up returns `ModeMismatch`, `EventSignupService.cs:80-86`).
- `AwaitingApproval` and `Waitlisted` teams are **not** signed-up teams. Their place was never confirmed.
- **Individuals-mode tournaments have no team sign-ups at all**, so only platform admins connect their placements. That is exactly the spec's edge case.

---

## R5 — The Tugeny HTTP client and Principle VII

**Decision**: A named `HttpClient` **`"Tugeny"`**.
- **Base address** from `Tugeny:BaseUrl` (default `https://tugeny.org/`).
- **Resilience**: `.AddJuggerHubResilience(builder.Configuration, "Tugeny")` plus `Resilience:Outbound:Tugeny` in every place the existing integrations are configured.
- **Size guard**: one **inner `ResponseSizeLimitHandler`** (4 MiB) added after the resilience handler.
- **Reading**: default `ResponseContentRead`, so the body is read **inside** each attempt's time limit.

**Configuration and why each value**:

| Key | Value | Why |
|---|---|---|
| `AttemptTimeoutSeconds` | 10 | Largest observed body (246 KB) arrives in well under a second |
| `TotalTimeoutSeconds` | 30 | The admin is waiting on a button; the shared default |
| `MaxRetryAttempts` | 2 | A GET, so retry is safe; three attempts is enough for a blip |
| `BaseDelaySeconds` | 1 | Human is waiting; jittered by the shared pipeline |
| `BreakerFailureRatio` | 0.5 | |
| `BreakerMinimumThroughput` | **4** | See below |
| `BreakerSamplingSeconds` | 120 | |
| `BreakerDurationSeconds` | 60 | |

**Why the breaker opens at 4 (Principle VII: "tune circuit breakers to actual call volume")**:
- Volume is a handful of admin actions per day. One import session is ~5 GETs within two minutes (link 1, preview 2, commit 2).
- The standard pipeline puts the breaker **inside** the retry, so it counts **attempts**. A dead Tugeny therefore produces 3 failed attempts on the first GET and opens on the second. Every later click in the next 60 s fails fast instead of making the admin wait out 30 s again.
- The library default (100 per 30 s) would never open at this volume. That is exactly the decorative breaker the constitution forbids.

**Why an inner size guard and not `MaxResponseContentBufferSize`**:
- Exceeding `MaxResponseContentBufferSize` throws `HttpRequestException`, which the shared retry treats as **transient**. An oversized body would be fetched three times.
- The inner handler reads at most `limit + 1` bytes per attempt, **inside** the attempt timeout (so nothing waits unbounded), and throws a non-HTTP `TugenyResponseTooLargeException`. The standard handler does not retry it. That makes "too large" a permanent rejection (Principle VII: "fail fast on rejections").
- It is a size guard, not a second resilience handler. The rule against stacked resilience handlers is untouched.

**Other points**:
- **429 from Tugeny** is retried by the shared pipeline with `Retry-After` honoured (the provider throttling us). Our **own** `tugeny` rate-limit policy (R11) answers the browser with 429, which the Angular retry interceptor already never retries. The two meanings are named in code where each is configured.
- **Logging**: the named client logs status and byte length only, never a body. A malformed body is logged at Warning as `(endpoint, status, length)`. The path carries only a public tournament slug or id, never personal data.

**Alternatives considered**:
- *Typed client like Resend*: a named client matches `MediaStore` and keeps the parsing service testable without HTTP.
- *`ResponseHeadersRead` plus a manual bounded stream read in the service*: moves the body read **outside** the pipeline's attempt timeout, so it would need its own hand-rolled timeout. That is review-rejectable.

---

## R6 — Linking: the only user input that reaches an outbound request

**Decision**: The admin pastes a Tugeny address or a bare slug. The server extracts the slug:
- **Accepted hosts**: `tugeny.org` and `www.tugeny.org`.
- **Accepted paths**: `/tournaments/{slug}` with any trailing page (`/all-teams`, `/live-view`, …).
- **Slug rule**: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, at most 150 characters.
- The slug is path-encoded into `tournamentsBySlug/{slug}`.
- The **host is never taken from input**. It is `Tugeny:BaseUrl` from configuration. So there is **no SSRF surface** (Principle I).

The link stores Tugeny's `id`, `slug`, `name` and `startdate`. The same Tugeny id linked to another event returns `linkedElsewhere: true`; the link is saved regardless (spec edge case: "allowed but warned").

**Live link (FR-015)**: `https://tugeny.org/tournaments/{slug}/live-view`, built from the stored slug and never from input. It is shown until the event ends. Afterwards the event page links `/tournament-tree` instead, for good (owner decision 2026-09-15: people should still be able to look at the whole bracket). Tugeny serves that page for finished tournaments whether finalized or not, so the link never depends on the import. The provenance line of imported results keeps linking `/all-teams`.

---

## R7 — Importing: stateless preview, then commit

**Decision**: Two endpoints:
1. **Preview** fetches ranking and matches and returns a draft without saving.
2. **Commit** fetches **again**, replaces the result's placements and matches in one `SaveChanges`, applies the connections the admin chose (each validated against R4), and stamps `Source = TugenyImport`, `ImportedAt`, `EditedSinceImport = false`.

**Rationale**:
- **Provenance must be server-established.** FR-017 says "came from Tugeny". If the browser posted the imported rows, the server would be recording the client's word for it (Principle I). Re-fetching on commit means the stored rows are what Tugeny served.
- **A stateless re-fetch is safe here.** Tugeny's manual: "*A tournament can be finalized only once. Later changes cannot be performed.*" Preview and commit therefore see the same data. At ~2 extra GETs per import, caching the draft server-side (with expiry and eviction) costs more than it saves.
- **Connections are sent as `tugenyTeamId → teamId` pairs**, each one an explicit choice by the admin in the preview (FR-011, FR-025). They apply only to that commit. No mapping is stored for reuse (owner decision, spec Clarifications).

**Alternatives considered**:
- *Browser fetches Tugeny directly*: provenance not verifiable, cross-origin, and it puts an outbound dependency on every admin's browser.
- *Import saves immediately, connections afterwards*: the admin would need two passes and would save an unconnected ranking over a connected one.

---

## R8 — Writing a ranking without losing platform-admin connections

**Decision**:
- `PUT /events/{id}/results/ranking` **replaces** the ranking. Each row carries an optional `id` of an existing placement.
- **Connection rule (FR-004, FR-027)**: a row may carry `teamId` *T* only if *T* is a signed-up team (R4) **or** the row's `id` already carries *T*. Only in the latter case are the existing connection's `ConnectedByUserId`/`ConnectedAt` kept.
- **Positions**: the server normalises positions to **standard competition ranking** (1, 2, 3, 3, 5) from the submitted order and ties. The admin types positions; the stored value is always the displayed one (FR-003).
- **Caps**: at most **128 rows**; names 1–80 characters after trimming. A team appears at most once (FR-005), enforced by validation and a unique partial index.
- **After an import**, a hand save sets `EditedSinceImport = true` (FR-017).

**Rationale**: On a past tournament, no team has a JuggerHub sign-up. A platform admin connects "Rigor Mortis" to the Rigor Mortis team after checking that it played, as the owner intended. Later, the event's admin fixes a typo in another row and saves. Without the `id` rule that save would face two bad outcomes. It would either silently drop the platform admin's connection, or, if the client echoed the `teamId` back, let a team without a sign-up through the event admin's check. The `id` rule keeps exactly the connections that already exist and nothing more. It never lets an event admin create one, or move one to a different row.

**Pagination deviation**: the ranking is returned as a bare capped list, not `PagedResult<T>`. A ranking is one unit: the winner, the ties and the count must render together. **Precedent**: `Roster` (48) and `RecentActivity` (6) on the team page, and team happenings (10). This is recorded in the plan's Complexity Tracking. Matches, which reach 528, **are** paged.

---

## R9 — Names, renames and deletions

**Decision**: A placement keeps two strings:
- **`SourceName`**: exactly what was typed, pasted or imported (e.g. `"Ecplise"`). It never changes.
- **`Name`**: what is shown. It equals the connected team's name while connected, and falls back to `SourceName` when disconnected.

`TeamId` is `SetNull` on team delete, following `EventParticipation`'s precedent: "*SetNull preserves activity history on team delete*" (`AppDbContext.cs:400-404`). After a delete, `Name` still holds the team's name (spec edge case: "under the name it had").

**Rationale**:
- Teams **cannot be renamed** today (no write path to `Team.Name`, `TeamService.cs:121` only). The snapshot is for deletion, and it covers a future rename too.
- `SourceName` is what a platform admin needs to judge a connection ("*Ecplise* → Eclipse?"), so the admin list shows both.
- **Match sides** hold a name snapshot plus a `SetNull` reference to the placement of the same result. A connected placement therefore links its matches without any per-match connection. That is the same entity, not "another placement", so FR-025 is not touched.

---

## R10 — "Connected by": attribution columns, not a log

**Decision**: `ConnectedByUserId` (FK to `User`, **`Restrict`**) and `ConnectedAt` sit on the placement, cleared on disconnect. `LastChangedByUserId` (`Restrict`) sits on the result.

**Rationale**:
- This is the award precedent: `BadgeAward`/`AchievementAward.GrantedByUserId` are `Restrict` with the comment "Preserve who granted".
- Account erasure (037) **never deletes the `User` row**; it neutralises it. So a `Restrict` actor survives with **no change to `AccountDeletionService`**, and the admin list shows `MemberPlaceholder.For(culture)` for an erased connector.
- `AdminActionRecord` does not fit: `TargetUserId` is a required user FK and its enum is account actions (`AccountEnums.cs:66-73`).
- FR-026 asks who made **a** connection. The current connection's attribution answers it. A full history of connect/disconnect toggles is not asked for.

---

## R11 — Endpoints, guards and rate limit

**Decision**:
- **Event-side**: a new `EventResultsController` at `api/v1/events/{eventId}/results`. **Platform side**: `AdminResultsController` at `api/v1/admin/results` under the existing `PlatformAdmin` policy. **Team history**: a new action on `TeamsController`. See [contracts/results-api.md](./contracts/results-api.md).
- **Guards**:
  - Event admin via `EventAdminGuard.ResolveAsync` (the same guard the event services use).
  - `Type == Tournament`, not cancelled, and `StartsAt <= UtcNow` for writes (FR-001).
  - Reads are open to any signed-in user who can see the event, which is every signed-in user (026, `EventsController` class-level `[Authorize]`).
- **Rate limit**: a new policy `tugeny`, **10 per minute per user** (`PartitionByUser`), on link, preview and commit. Those are the only endpoints that cause an outbound call. The breaker (R5) caps what reaches Tugeny across all admins.

**Rationale**: `EventsController` is already 536 lines. Results have their own lifecycle and their own outbound dependency.

---

## R12 — Past-dated tournaments (FR-023): mostly already true

**Finding**: **Nothing needs building on the server.**
- **Past dates are accepted today.** `CreateEventRequest` has only `[Required]` (`EventDtos.cs:20-21`); the service only checks end ≥ start (`EventService.cs:79-84`). The frontend wizard has no `min` (`event-create.component.html:55,60`). A test already creates a 2020 event (`EventTests.cs:780`).
- **Every create/join path already closes once `EndsAt < now`:**
  - sign-up (`EventSignupService.cs:64`)
  - party form, apply and join (`PartyService.cs:75,324`, `PartyRosterService.cs:99`)
  - market listing, apply, invite, accept and recruiting (`MarketListingService.cs:76,128`, `MarketRequestService.cs:71,125,188,332`, `MarketRecruitingService.cs:107`)
- **Creation fans out nothing** (`EventService.cs:156-158`). Only cancel notifies.

**Gap, fixed here**: the **frontend** still offers actions on ended events that the server then refuses with 409:
- `join-actions.component.ts:47-50` ignores dates.
- The party context's `CanForm = t.IsAdmin && t.Party is null` ignores `EndsAt` (`PartyService.cs:237`).

A past-dated tournament would show a "Join" or "Enter party" button that always fails. This feature makes `CanForm` respect `PartyAccess.IsEventOpen`, hides the join actions once `endsAt` has passed, and keeps the server as the boundary. It is a small, pre-existing defect that FR-023 makes visible, so it is fixed rather than filed.

---

## R13 — The team list for Tugeny (FR-013): no endpoint

**Decision**: The results page reads `GET /events/{id}/participants?group=joined` (existing, paged; it reads every page) and builds the text **in the browser**. It uses one `teamName` per line, flags duplicates before copying (Tugeny refuses them, R1), and offers a read-only text box with a copy button. The box works even where the Clipboard API is blocked.

**Rationale**: The data is already served, and the output is a pure transformation of it. A server endpoint would add a route, a DTO and a test for a string join. Plain text was chosen over `[{"name","status":"participant"}]` because it is what a person can read and check before pasting, and Tugeny accepts both.

---

## R14 — Privacy and legal

**Decision**: **No change to the privacy policy.**

**Rationale**:
- Results are about **teams**, which are not natural persons.
- Nothing about a user is sent to Tugeny; the requests carry a public tournament slug or id.
- `ConnectedByUserId` is an internal attribution of an administrative act, the same category as the award grantor already covered.
- 036's owner rule "legal content stays generic" (organise by category of data, never feature-by-feature) means a new feature that adds no new category of personal data adds no text.
- **Attribution**: Tugeny publishes this data under the MIT licence. Imported results carry a visible "Results from Tugeny" line linking the tournament (FR-017).

---

## R15 — What the UI must survive (Gate 7 inputs)

- **The fifth admin tab.** The admin bottom bar already holds four tabs (`admin-bottom-nav`). German "Ergebnisse" is the binding label at 375 px. If five do not fit, DESIGN.md wins and the results list moves under the Teams tab as a sub-view. Decide at the browser walk.
- **One coral CTA per view.** On the results editor the primary is **Save ranking**. Paste, Import, Copy team list and Clear are secondary or ghost.
- **Scores in the mono face** (`5 : 3`), per DESIGN.md "Numbers & scores". Placements show as ordinals in the mono face.
- **Error vs empty.** "No results recorded yet" (`jh-empty-state`) must never render for a failed load (`jh-alert` + retry). A Tugeny failure is an inline `jh-alert` on the results page, never a page-level block.
- **German strings to walk at 375 px**: "Teamliste für Tugeny kopieren", "Mit einem Tugeny-Turnier verknüpfen", "Ergebnisse von Tugeny übernehmen".
