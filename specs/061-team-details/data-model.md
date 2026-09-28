# Data Model: Team Details — Editable Name, Type and City, a Description, and Links

One column, one new table and one migration (`AddTeamDescriptionAndLinks`, generated **with** a
build). Two existing tables get rows **rewritten** on a rename, with no schema change. The
reasoning is in [research.md](research.md) R3, R4 and R9.

## Team (existing, `Teams`) — one new column, three fields become editable

| Field | Type | Change |
|---|---|---|
| `Slug` | `varchar(30)` | **Unchanged: immutable** (`init`-only, no writer). FR-004 |
| `Name` | `varchar(50)` | Now **editable** by an admin. 2–`TeamOptions.NameMaxLength` after trim; not unique |
| `Type` | `int` (`CityTeam`=0, `Mixteam`=1) | Now **editable** |
| `CityId` | `uuid NULL` → `Cities` (Restrict) | Now **editable**. Required iff `CityTeam`, null iff `Mixteam` |
| `Description` | **`varchar(1000) NULL`** | **NEW**. Null = no description. Blank input is stored as null |
| `BeginnersWelcome` | `bool` | Unchanged. Still only via `PATCH /teams/{slug}` |
| `Links` (navigation) | `ICollection<TeamLink>` | **NEW** |

**Invariant (unchanged, now enforced on two paths)**: `Type == CityTeam ⇔ CityId != null`. It is
enforced by `CreateAsync` and `UpdateDetailsAsync` through the same rule. As today there is no
database check constraint: both writers are in one service, and the create path has never had
one.

**Writes**: `UpdateDetailsAsync` updates the row with one `ExecuteUpdate`. It sets `Name`,
`Type`, `CityId`, `Description` and **`ModifiedDate`**, the last explicitly (Principle III).

## TeamLink (NEW, `TeamLinks`)

| Field | Type | Rule |
|---|---|---|
| `Id` | `uuid` (UUIDv7, `BaseEntity`) | |
| `CreatedDate` / `ModifiedDate` | `timestamptz` | Audit interceptor (inserted via tracked `AddRange`) |
| `TeamId` | `uuid` → `Teams.Id` | **Cascade**: links go with the team |
| `Label` | `varchar(30)` NOT NULL | Trimmed, 1–30, no control characters |
| `Url` | `varchar(500)` NOT NULL | Normalised absolute `https` URL with a DNS host, no user info. Unique per team (service rule) |
| `Position` | `int` NOT NULL | 0-based order as entered |

- **Index**: unique `(TeamId, Position)`. It is the order's integrity guard and also serves the
  FK lookup.
- **Cardinality**: 0–5 per team, enforced on write (`TeamDetailsPolicy.MaxLinks`).
- **Lifecycle**: replaced **as a whole** on every details save: `ExecuteDelete` of the team's
  rows, then `AddRange` of the new list, inside the save's transaction. No link has an identity a
  client can address. The DTO carries none.
- **Erasure (037)**: not affected. The rows belong to a team, not a user.

## Notification (existing, `Notifications`) — rows REWRITTEN on a rename, no schema change

The nine kinds whose `Payload` (jsonb, camelCase) carries both `teamSlug` and `teamName`:

| Kind | Where written |
|---|---|
| `TeamInvite` | `TeamInvitationService.cs:218` |
| `TeamRoleChanged` | `TeamService.cs:483` (Home's "role changed" entries read these rows) |
| `TeamNews` | `TeamNewsService.cs:116` |
| `TeamJoinRequest` | `TeamJoinRequestService.cs:129` |
| `TeamJoinRequestAnswered` | `TeamJoinRequestService.cs:453` |
| `PartyRequest` (request) | `PartyService.cs:153` |
| `PartyRequest` (nudge, **null `DedupeKey`**) | `PartyRosterService.cs:286` |
| `PartyNews` | `PartyNewsService.cs:261` |
| `MarketInvite` | `MarketRequestService.cs:265` |

**Rename transition** (only when the name actually changed):

- **Selected by** `Payload->>'teamSlug' = <team slug>` and `teamName` being a JSON string that
  differs from the new name. Rows are found by the team, **never** through the roster, so former
  members' rows are included.
- **Changes** `Payload.teamName` → the new name, and `ModifiedDate` → now.
- **Leaves alone** `IsRead`, `CreatedDate`, `DedupeKey`, `Type`, `RecipientUserId` and every other
  payload key (for example `eventName`). The alert is not re-delivered: there is no realtime
  event, no badge change, and no email or push.

## TournamentPlacement (existing, feature 050) — rows REWRITTEN on a rename, no schema change

**Rename transition** (only when the name changed): every row with `TeamId == team.Id` gets
`Name` → the new name, and `ModifiedDate` → now.

- Rows with a null `TeamId` and other teams' rows are untouched.
- `SourceName` is never touched (050 R9).
- The parent `TournamentResult`'s last-changed date is **not** touched: a rename is not a results
  change.
- `TournamentMatch` needs nothing of its own. A linked side is rendered from
  `FirstPlacement.Name`/`SecondPlacement.Name` at read time.

This extends 050's data-model rule 3 ("Connecting sets `Name = Team.Name`") with "renaming the
connected team sets `Name = Team.Name`". A note is added to `specs/050-tournament-results/data-model.md`.

## Validation summary (server; the client mirrors the cheap ones for UX)

| Input | Rule | Refusal `code` |
|---|---|---|
| `name` | trim; 2–50 | `nameInvalid` |
| `type` + `location` | `CityTeam` ⇒ a city; `Mixteam` ⇒ none | `cityRequired` / `mixteamHasCity` |
| `location.cityExternalId` | a city already held, or resolvable in the local city reference | `cityNotFound` |
| `description` | trim; blank ⇒ null; ≤ 1000 | `descriptionTooLong` |
| `links` | ≤ 5 | `tooManyLinks` |
| `links[i].label` | trim; 1–30; no control characters | `linkLabelInvalid` (+ `link: i`) |
| `links[i].url` | trim; default scheme `https`; absolute; `https` only; DNS host containing `.`; no user info; normalised ≤ 500 | `linkUrlInvalid` (+ `link: i`) |
| `links` | no two with the same normalised URL | `linkDuplicate` (+ `link: i`, the later one) |

Validation stops at the first failure, checked in the table's order. A refused save writes
nothing: the city resolution is the only step before validation completes that can write, and
it runs only once everything else has passed (R2).
