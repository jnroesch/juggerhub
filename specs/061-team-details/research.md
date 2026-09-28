# Research: Team Details — Editable Name, Type and City, a Description, and Links

All facts below were established by reading the code on `main` at `ed805c6`. The owner's six
product decisions are in [spec.md](spec.md) → Clarifications.

## R1 — One new endpoint, `PUT /teams/{slug}/details`; the existing `PATCH` stays as it is

**Decision**: add `PUT /api/v1/teams/{slug}/details`. It replaces the whole editable identity in
one request: name, type, city, description and links. It returns the updated `TeamDetailDto`,
following the event edit's precedent (`EventService.EditAsync`). `PATCH /teams/{slug}`
(`UpdateTeamSettingsRequest`, beginners flag only) is left byte-identical.

**Rationale**:
- FR-006 needs all of it saved or none of it, from one Save press. One request is the only way
  the server can make that atomic.
- FR-007 (the details save never touches the flag or the logo, and the reverse) becomes
  structural: the two endpoints write disjoint columns. The beginners switch keeps its instant
  apply-on-toggle behaviour.
- A full replace has no "null means unchanged or means clear?" ambiguity. The description and
  the links can be cleared, so this matters.

**Alternatives rejected**:
- *Grow the PATCH with nullable fields.* The toggle would then either resend everything or keep
  the null ambiguity, and "clear the description" would need a sentinel.
- *Two endpoints (identity; about).* One Save could no longer be atomic, and FR-001 puts all of
  it in one section.
- *A separate `PUT …/description` for the wizard.* It would be a second writer of the same
  field. The wizard instead composes the full request from the **create response**. That is the
  server's own record of the name, type and city it just stored, so nothing can be clobbered, and
  nobody else can be an admin yet (R11).

## R2 — The write is ONE strategy transaction: lock, compare, update, then the two rewrites

**Decision**: `TeamService.UpdateDetailsAsync`:
1. Guard: `TeamMembershipGuard.ResolveAsync` → not a member ⇒ `NotFoundOrNotMember` (404);
   member but not admin ⇒ `Forbidden` (403). This is the order and the answers every team-admin
   action already gives.
2. Validate everything, collecting the **first** failure as a code (R8): name → type/city shape
   → description → links (R6). Nothing is read from or written to the team yet.
3. Resolve the city **before** any transaction (R5). `ResolveAndUpsertAsync` owns its own save
   (the `StructuredAddress` remarks). An inserted `City` row that outlives a later refusal is
   harmless: nothing cleans up unused cities today, and it is a shared reference row.
4. `CreateExecutionStrategy().ExecuteAsync` (Principle VII; the `MutateMembershipAsync` shape):
   - `ChangeTracker.Clear()` (so a replay starts clean);
   - `BEGIN`; `SELECT 1 FROM "Teams" WHERE "Id" = @id FOR UPDATE`;
   - read the current `Name` (inside the lock, so the "did it change?" answer is the committed
     truth);
   - `ExecuteUpdate` the team row: `Name`, `Type`, `CityId`, `Description`, **`ModifiedDate`**;
   - `ExecuteDelete` the team's `TeamLinks`, then `AddRange` fresh rows and `SaveChanges`;
   - **only if the name changed** (FR-011): the alert rewrite (R3) and the placement refresh
     (R4);
   - `COMMIT`.
5. Re-project and return the `TeamDetailDto`.

**Rationale**:
- Every statement is either an `ExecuteUpdate`/`ExecuteDelete` with fixed values or an insert of
  entities created **inside** the delegate. A replay therefore converges instead of doubling up.
- The row lock serialises two concurrent saves, so the alert and placement rewrites of the later
  save always run after the earlier one has committed. The final name and every rewritten copy
  agree.
- The same lock is what `MutateMembershipAsync` takes. A details save racing a role change only
  waits.
- Nothing is sent: no realtime push, no email, no push notification. Nothing needs to happen
  after the commit, because no badge count changes (FR-009 is silent).

**Alternatives rejected**:
- *Best-effort rewrites after the save.* A crash in between would leave the team renamed and its
  alerts and results on the old name, with nothing to repair them.
- *Tracked entity + `SaveChanges`.* It is equivalent, but the 057/058 idiom is fixed-value
  statements. Those make replay-safety readable at a glance.

## R3 — Delivered alerts are found by the team's SLUG inside the payload and rewritten in place

**Facts**:
- `Notifications.Payload` is **`jsonb`** (`AppDbContext.cs:551`), serialised camelCase
  (`NotificationService.PayloadJson`).
- Nine kinds carry the team's name as `teamName`, and **every one of them also carries
  `teamSlug`**: `TeamInvite`, `TeamRoleChanged`, `TeamNews`, `TeamJoinRequest`,
  `TeamJoinRequestAnswered`, `PartyRequest` (both the request and the nudge), `PartyNews`,
  `MarketInvite`.
- The party nudge rows have a **null** `DedupeKey` (`PartyRosterService.cs:288`). 057's lookup
  by dedupe prefix therefore cannot find them.
- Home's "your role changed" entries are **read from these rows at request time**
  (`HomeService.StateChangeEntry`). Rewriting the rows covers Home with no other change.
- No notification DTO exposes `ModifiedDate`, and the inbox orders by `CreatedDate`.

**Decision**: a new engine method, `INotificationService.ReplaceTeamNameAsync(string teamSlug,
string teamName, CancellationToken ct)`. It runs one parameterised statement
(`ExecuteSqlInterpolatedAsync`, so no string concatenation reaches SQL):

```sql
UPDATE "Notifications"
SET    "Payload" = "Payload" || jsonb_build_object('teamName', @name::text),
       "ModifiedDate" = @now
WHERE  "Payload" ->> 'teamSlug' = @slug
  AND  jsonb_typeof("Payload" -> 'teamName') = 'string'
  AND  "Payload" ->> 'teamName' IS DISTINCT FROM @name
```

**Rationale**:
- **By slug, never by roster.** Former members' rows are included (057's lesson; spec edge
  case). The slug is immutable (FR-004), so it is a stable key for the team. It cannot collide
  with another team's rows, because the slug is unique.
- **By key, not by a list of types.** Any future kind that carries `teamSlug` + `teamName` is
  covered with no edit. The one hidden list that would otherwise drift is not written. The
  integration test enumerates all nine kinds today.
- **The engine owns row mutations** (057's rule: `ReplacePayloadAsync`/`DeleteManyAsync` live
  there). This keeps the payload's JSON shape knowledge in one place, next to `PayloadJson`. The
  `'teamName'`/`'teamSlug'` literals are the camelCase of the payload records' `TeamName`/
  `TeamSlug`. A comment says so, and the test would fail if either were renamed.
- It uses the context's connection, so it **enlists in the ambient transaction** (R2).
- It touches **only** `Payload` and `ModifiedDate`. `IsRead`, `CreatedDate`, `DedupeKey` and
  `Type` are untouched, which is FR-009's "silent". It is realtime-free, like
  `ReplacePayloadAsync`.
- `IS DISTINCT FROM @name` skips rows already carrying the name, which makes replays and repeat
  saves no-ops.

**Cost / residual**: a sequential scan of `Notifications` per **rename** (not per save — FR-011
gates it). Renames are rare admin actions, and 057's news edit already pays the same kind of
scan. The recorded fix, if it is ever needed, is an expression index on
`("Payload" ->> 'teamSlug')`.

**Alternatives rejected**:
- *Resolve the name at read time* (join `Teams` by the payload's slug in the inbox, the Home
  feed and the push composer). That means three readers to change, a JSON-path join in the
  inbox's hot query, and a fallback path for deleted teams. It would also contradict the owner's
  answer, which was framed as "the rename rewrites".
- *`ReplacePayloadAsync` per kind.* It replaces the whole payload and needs one per row, since
  each row has other per-row fields. It also cannot find the null-dedupe nudge rows.

## R4 — Connected placements are refreshed; results are not "changed"

**Facts**:
- `TournamentPlacement.Name` is the shown name (050 data-model: "the team's name while
  connected"). It is written only at connect time (`TournamentResultService.Apply` L302,
  `TugenyImportService` L242, `AdminPlacementService` L114).
- Match sides show `FirstPlacement.Name` when linked (`TournamentResultService.cs:77,80`). A
  match therefore needs nothing of its own.
- No results DTO exposes a placement's `ModifiedDate`.

**Decision**: inside R2's transaction, if the name changed:
`TournamentPlacements.Where(p => p.TeamId == teamId).ExecuteUpdate(Name = newName, ModifiedDate
= now)`.

- The `TournamentResult`'s own "last changed" date is **not** touched. A rename is not a change
  to anyone's results (050 US1-4 shows that date to readers).
- Placements with `TeamId == null` are untouched by construction (spec edge case).
- 050's data-model rule 3 gains "renaming the connected team sets `Name = Team.Name`". An
  amendment note is added to that file.

## R5 — The city: create's rules, and the current city is simply resent

**Decision**:
- `CityTeam` with no `CityExternalId` → `cityRequired`.
- `Mixteam` with a `CityExternalId` → `mixteamHasCity` (create's rule, FR-002).
- `CityTeam` → `ResolveAndUpsertAsync`, where `CityNotResolvableException` → `cityNotFound`.
- `Mixteam` → `CityId = null` (FR-003).

**Rationale**: the settings form resends the current city on every save (`LocationDto` already
echoes `ExternalId` "so an edit form can resend the current city without re-picking").
`ResolveAndUpsertAsync` reuses a city it already holds **without** a reference lookup
(`CityService.cs:140`), so resending the current city costs one read and cannot fail.

*Corrected during implementation*: the plan first proposed skipping resolution when the city is
unchanged, "so a save can never fail on the city after a reference-dataset refresh". Reading
`CityService` showed that failure cannot happen: an existing city never needs its reference row.
The skip was dropped as complexity with nothing to guard.

## R6 — Link rules live in one pure class, `TeamDetailsPolicy`

**Decision**: a static class in `Services/Teams/` (the `TeamSlugPolicy` / `InviteReference`
precedent) with the constants and the pure normalisers:

| Rule | Value | Code on failure |
|---|---|---|
| Description | trim; empty/whitespace ⇒ `null`; ≤ **1000** | `descriptionTooLong` |
| Link count | ≤ **5** | `tooManyLinks` |
| Label | trim; **1–30**; no control characters (a line break is one) | `linkLabelInvalid` |
| Address | trim; no `://` ⇒ prefix `https://`; `Uri.TryCreate(Absolute)`; scheme **exactly `https`**; host is a DNS name containing a `.`; **no `UserInfo`**; normalised `AbsoluteUri` ≤ **500** | `linkUrlInvalid` |
| Duplicate | two links with the same normalised address | `linkDuplicate` |

- The **normalised `AbsoluteUri`** is what gets stored. It is canonical (host lowercased, spaces
  escaped), which is what makes duplicate detection meaningful and the stored `href` safe to
  render.
- A scheme-less `javascript:alert(1)` becomes `https://javascript:alert(1)`, which does not
  parse (the "port" is not a number). A scheme-less `mailto:x@y.de` parses with user info and is
  refused by the no-user-info rule. `http://…` keeps its scheme and is refused. All three are
  unit-tested.
- Name rules are **not** duplicated here. They stay `2..TeamOptions.NameMaxLength` exactly as
  `CreateAsync` has them. The two call the same private helper, so create and edit cannot drift
  apart.

*Refined during implementation* (both in `TeamDetailsPolicy`, both unit-tested):

- **Is there a scheme?** A leading `scheme:` (`^[A-Za-z][A-Za-z0-9+.-]*:`) decides it, not the
  `://` check the virtual-link precedent uses. Otherwise `a.de/?u=https://b.de` would be
  mistaken for an address that has a scheme and refused. `javascript:` and `mailto:` are
  recognised as schemes and refused directly.
- **Labels refuse bidirectional formatting characters** (U+061C, U+200E/F, U+202A–E,
  U+2066–9) as well as control characters. The label is rendered right before the host. An
  unterminated right-to-left override in it would visually **reverse the host beside it**,
  which is exactly the disguise FR-017 exists to prevent. The template also isolates each part
  with `<bdi>` as defence in depth (R10).
- IP-literal hosts (`https://192.168.0.1`, `https://[::1]`) are refused by the DNS-host rule.

**Why constants and not `TeamOptions`**: the limits are also **column lengths** (R9). An options
knob raised above its column would turn a validation message into a 500. `NameMaxLength` already
has that hazard, and it is not extended here.

## R7 — The team page reads description and links from data it already loads

**Decision**:
- `TeamPublicDetailDto` (the `{slug}/public` payload every team-page viewer loads) gains
  `string? Description` and `IReadOnlyList<TeamLinkDto> Links`, appended **at the end** because
  it is a positional record with no defaults.
- `TeamDetailDto` (members-only; the settings page loads it) gains the same two, with defaults.
- Both are projected in the query that already runs, so SC-006 holds: no new request.

**Links are an embedded, bounded list, not a paginated endpoint**: at most five, enforced on
write. This follows the `Roster` (48) and `RecentActivity` (6) precedents on the same payload,
and is recorded under the constitution check.

`TeamLinkDto(string Label, string Url)`. There is no id: the list is replaced as a whole, and
nothing addresses a single link.

## R8 — Refusals carry a machine-readable `code`; the client never shows the server's English

**Decision**: a refused save is `400` ProblemDetails with `extensions.code` (one of `nameInvalid`,
`cityRequired`, `mixteamHasCity`, `cityNotFound`, `descriptionTooLong`, `tooManyLinks`,
`linkLabelInvalid`, `linkUrlInvalid`, `linkDuplicate`). For link codes it also carries
`extensions.link` (the 0-based index of the offending row). `EventResultsController` already sets
`extensions.row` the same way. The client maps `code` → `teams.details.errors.<code>` and marks
row `link` invalid. An absent or unknown code (model-binding 400, 5xx, offline) →
`teams.details.errors.generic`. 403 → `errors.forbidden`. 404 → `errors.notFound`, then reload
(the admin was removed meanwhile).

Model-binding attributes on `UpdateTeamDetailsRequest` are **payload guards only**, set well above
the rules (`MaxLength(200)` name, `4000` description, `20` links, `200`/`2000` label/address). An
over-limit value therefore reaches the service and gets a coded, translatable refusal instead of
MVC's uncoded one. That is the reasoning already written on `CreateTeamRequest.Slug`: "the format
rule lives in exactly one place".

## R9 — Storage: one column, one table, one migration

- `Teams.Description` `varchar(1000) NULL`. There is no backfill: null is "no description" for
  every existing team.
- `TeamLinks` (`BaseEntity`): `TeamId` (FK → Teams, **Cascade**, FR "goes when the team goes"),
  `Label varchar(30)`, `Url varchar(500)`, `Position int`, unique index `(TeamId, Position)`. The
  unique index also serves the FK lookup.
- `TeamService.DeleteAsync` already deletes the team with one `ExecuteDelete`, so the cascade
  removes the links inside PostgreSQL. There is no stored object to clean up, unlike the logo.
- 037 (account erasure) is **not** affected: links belong to a team, not to a user.
- Migration `AddTeamDescriptionAndLinks`, **generated WITH a build** (056: `--no-build` emitted
  an empty migration).

## R10 — The team page's About card

**Decision**: a `jh-card` titled **About** (`h2`), at the **top of the main column**, after the
actionable party-request, join-notice and join-queue blocks and before the roster. It is shown to
every viewer of the page (members, non-members, requesters), and rendered **only** when there is
a description or at least one link (FR-014).

- Description: `whitespace-pre-line break-words text-body-md text-body`. DESIGN.md typography:
  "prose — anything written to be read: a description" is `body-md`. Angular interpolation only,
  never `[innerHTML]`, so nothing in it can become markup or a link (FR-014).
- Links: a list below the description. Each row is an `<a>` with the **label underlined**
  (`underline`: the issue asks for DESIGN.md's in-prose rule, and colour alone is a weak
  affordance), an `external-link` icon (`sm`), and the **host** in `caption`/`text-muted` beside
  it (FR-017). `target="_blank" rel="noopener noreferrer nofollow ugc"` (FR-018):
  - `noopener`: no `window.opener`, so no tab-nabbing;
  - `noreferrer`: the destination is not told which team page linked it;
  - `nofollow ugc`: no vouching.
  An `sr-only` "(opens in a new tab)" suffix goes on each link.
- Host = a pure util `linkHost(url)` in `core/utils/`: `new URL(url).host` (the WHATWG parser
  returns the **ASCII/punycode** host, which is FR-017's "encoded form"), with a leading `www.`
  dropped for readability. Dropping `www.` cannot make one site look like another. It is
  unit-tested with an IDN host.
- Long hosts and labels wrap (`break-all` on the host, `break-words` on the label) (FR-024).
- No "add a description" nudge for admins on the page. Owner rule (060): members' actions live
  in the side-column card, and Manage is already there.

*Changed after the browser walk* (both found only by the screenshots):

- **The host sits on its own line under the label, inside the link**, not beside it after a
  `·`. At 375px a long label ("Turnierergebnisse und Archiv") pushed the host onto the next
  line, which then began with a stranded middot. This is the same class of defect 057 fixed on
  the news meta line. Inside the link, a screen reader also hears where it goes.
- **The settings form's address inputs use the body face, not `font-mono`.** DESIGN.md
  reserves mono for numbers, scores, times and counts. The walk also showed that `font-mono`
  renders in the platform fallback, because "Mona Sans Mono" is never loaded (pre-existing,
  #339). The character counter is a count and keeps mono. The label/address columns split
  2:3 from `sm`.

## R11 — The wizard's `about` step

**Decision**: `STEPS` becomes `basics · type · review · logo · about · invite`. `createdTeam`, a
signal set together with the `createdSlug` latch, holds the create response.

- The step has one textarea (signal-bound, outside the `FormGroup`), with `maxlength=1000` and a
  counter.
- **One** primary control, mirroring the logo step's "skipping and continuing are the same move":
  - label **Skip** while the text is blank;
  - label **Continue** once there is text;
  - label **Saving…** while the request runs.
  Blank ⇒ no request (FR-021). Text ⇒ `PUT …/details` composed from `createdTeam` (name, type,
  `location` → `{cityExternalId, name}`), with the typed description and `links: []`. Success ⇒
  `invite`.
- On failure: the translated error (R8), the text kept, and a **secondary Skip** button beside
  the primary, the only moment the two differ (FR-022). A retry is a press (Principle VII:
  browser-hop mutations are never auto-retried; the interceptor retries only GET/HEAD, verified
  at `retry.interceptor.ts:32`).
- `onSubmit()` gains the `about` case. Enter inside a textarea inserts a line break anyway.

## R12 — What the acting admin sees without reloading (FR-008)

- The settings page replaces its `detail` from the PUT response and calls `membership.load()`.
  The nav and "My team" read that cache (the logo precedent, `team-settings.component.ts:128`).
- The team page, Home, Alerts, Browse and the chat inbox all fetch on open. The inbox calls
  `loadInbox()` in `ChatInboxComponent` on init (`chat-inbox.component.ts:69`). There is
  therefore no stale client cache to patch. A page **already open** in another tab shows the old
  details until reloaded (spec edge case: nothing updates live).

## R13 — Principle VII, security and legal

- **Principle VII is NOT engaged as an integration.** No outbound call is added: the server
  never fetches a team's link (no preview, no unfurl, **no SSRF surface**), and the browser opens
  it as a navigation. What VII does require is here: the multi-step write runs through the
  execution strategy (R2), and the `PUT` is a mutation the browser never retries (R11).
- **OWASP**:
  - A01: admin gate in the service, 404-not-403 for outsiders.
  - A03: parameterised raw SQL; interpolation-only rendering; `https`-only `href`s, which Angular's
    URL sanitiser would also neutralise.
  - A04: user-info and IDN-homograph disguises.
  - A05: no new anonymous surface; the OpenAPI allowlist is untouched.
- **Legal**: no change. The privacy policy's "whatever you put on the site … teams" and the
  terms' "team, tournament and training descriptions" already cover it, and the owner's rule is
  that policy text is organised by category, not by feature.

## R14 — i18n

About 30 keys across `teams.details.*` (settings section and error codes), `teams.detail.about*`
(team page) and `teams.create.about*` (wizard), in **all three catalogues in one commit**
(`catalog-parity.spec.ts`).

- German: *Teamdetails*, *Über das Team*, *Links*, *Link hinzufügen*, *Bezeichnung*, *Adresse*,
  *Beschreibung*, *Speichern*.
- Spanish: *Detalles del equipo*, *Sobre el equipo*, *Enlaces*, *Añadir enlace*, *Etiqueta*,
  *Dirección*, *Descripción*, *Guardar*.
- German uses `–`, not `—` (DESIGN.md "Dashes and separators").
