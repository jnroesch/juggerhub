# Implementation Plan: Team Details — Editable Name, Type and City, a Description, and Links

**Branch**: `061-team-details` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/061-team-details/spec.md` (GH #359 + GH #321)

## Summary

This feature makes a team's name, type and home city editable by its admins, and gives every team
a plain-text **description** (≤1000) and up to **5 labelled links** (https only). All of it is
saved through ONE new endpoint, `PUT /teams/{slug}/details`, as a full replace. It is edited in a
new first section of Manage team and shown in an **About** card on the team page. The creation
wizard gains an optional **about** step for the description.

The existing `PATCH /teams/{slug}` (beginners flag) is untouched, so FR-007 is structural.

**Two defects-in-waiting, found by reading, are the load-bearing part.** They exist because
nothing could rename a team before this feature:

1. **Nine alert kinds copy the team's name** into their jsonb payload. Home's "role changed"
   entries are built from those rows. The owner chose that delivered alerts show the **new** name.
   They are found by `Payload->>'teamSlug'`, never by roster, so former members are included, and
   **by key, not by a type list**, so a future kind is covered automatically. One parameterised
   statement in a new engine method, `INotificationService.ReplaceTeamNameAsync`, rewrites them.
2. **Tournament placements snapshot the name** (050 promised "a renamed team's placements show its
   current name"). Connected placements are refreshed. Matches derive from them. The result's
   "last changed" date is not touched.

Both run **only when the name changed** (FR-011), inside the same strategy transaction as the
team row. That transaction takes a `FOR UPDATE` lock on the team row first, so the name
comparison and the rewrites see committed truth.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 (zoneless) + Nx + Tailwind (frontend)

**Primary Dependencies**: ASP.NET Core, EF Core 10 + Npgsql, Transloco; nothing new

**Storage**: PostgreSQL. +1 column (`Teams.Description varchar(1000) NULL`), +1 table (`TeamLinks`), 1 migration `AddTeamDescriptionAndLinks` (generated **with** a build)

**Testing**: xUnit integration tests (Testcontainers Postgres, `JuggerHubApiFactory`); Jest (`npx nx test web`); owner-mandated browser walk (Playwright script, German, 375px + desktop)

**Target Platform**: Linux containers (AKS), local Docker Compose

**Project Type**: web application (`backend/` + `frontend/apps/web`)

**Performance Goals**: The team page takes no extra request (SC-006). A rename costs one sequential scan of `Notifications` (rare admin action; 057 precedent)

**Constraints**:
- Atomic save (FR-006).
- A silent alert rewrite (FR-009): no realtime event, no email, no push.
- No server-side fetch of link URLs (no SSRF surface).
- Client renders refusal `code`s, never the server's English (#179).

**Scale/Scope**:
- 1 endpoint, 2 DTO extensions, 1 entity, 1 engine method, 1 policy class.
- 3 frontend surfaces: settings section, About card, wizard step.
- About 30 i18n keys × 3 catalogues.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Verdict |
|---|---|
| **I. Security-first / never trust the client** | PASS. All rules are server-side in `TeamDetailsPolicy` + `UpdateDetailsAsync`; the client mirrors only lengths for UX. The admin gate is in the service; outsiders get the same 404 as a missing team. Links: `https` only, no user info, DNS host, normalised; rendered via `[href]` (Angular sanitiser as defence in depth) with `rel="noopener noreferrer nofollow ugc"`; host shown punycode-encoded (homograph). Description and labels are interpolation-only, never `innerHTML`. The raw SQL is `ExecuteSqlInterpolatedAsync`, i.e. parameterised. The server never fetches a link. |
| **II. Thin controllers, services, DTO projections** | PASS. The controller maps status → ProblemDetails (+ `code`/`link` extensions); logic sits in `TeamService`, and the row rewrite in `NotificationService` (057: the engine owns row mutations). Explicit `.Select` projections. |
| **III. Data access** | PASS. `TeamLink : BaseEntity` (UUIDv7). Every `ExecuteUpdate` sets `ModifiedDate`: team row, placements, notifications. Reads use `AsNoTracking` projections. **Deviation recorded** below: links are an embedded bounded list (≤5), not a paginated endpoint. |
| **IV. Auth/session** | N/A. No auth change. |
| **V. Environment parity** | PASS. One migration; no config, no secret. |
| **VI. Conventions** | PASS. Separate `.html`/`.css`/`.ts`; no scripts. |
| **VII. Resilience** | **Not engaged as an integration**: no outbound call. What it requires is designed in. The multi-step write is ONE execution-strategy unit with every mutation inside the delegate, replay-safe by construction (`ChangeTracker.Clear`, fixed-value statements, entities created inside). City resolution (it owns its save) runs before. The browser `PUT` is never auto-retried (the interceptor retries only GET/HEAD). A retry is a press. |
| **Gate 7 (UI/DESIGN.md)** | **ENGAGED** → `checklists/ui-review.md`. Binding case: German at 375px (settings section with 5 link rows; About card with a long label/host; wizard step). |
| **Gate 8 (Resilience)** | PASS (see VII). |

**Post-design re-check (after Phase 1)**: unchanged. The design adds no dependency, no outbound
call, no anonymous surface and no new list endpoint.

## Project Structure

### Documentation (this feature)

```text
specs/061-team-details/
├── spec.md
├── plan.md                        # this file
├── research.md                    # R1–R14
├── data-model.md
├── quickstart.md
├── contracts/team-details-api.md
├── checklists/requirements.md
├── checklists/ui-review.md        # created during implementation (Gate 7)
└── tasks.md                       # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/Team.cs                                  # + Description, + Links
├── Entities/TeamLink.cs                              # NEW
├── Data/AppDbContext.cs                              # Team.Description(1000); TeamLink config (Cascade, unique (TeamId, Position))
├── Data/Migrations/*_AddTeamDescriptionAndLinks.cs   # NEW (generated with a build)
├── Dtos/Teams/TeamDtos.cs                            # UpdateTeamDetailsRequest, TeamLinkInput, TeamLinkDto; Detail/PublicDetail +Description +Links
├── Services/Teams/TeamDetailsPolicy.cs               # NEW: pure rules + codes (R6)
├── Services/Teams/ITeamService.cs                    # + UpdateDetailsAsync, TeamDetailsResult/Status/Code
├── Services/Teams/TeamService.cs                     # UpdateDetailsAsync (R2); shared name rule with CreateAsync; projections +Description +Links
├── Services/Notifications/INotificationService.cs    # + ReplaceTeamNameAsync
├── Services/Notifications/NotificationService.cs     # ReplaceTeamNameAsync (R3)
├── Controllers/TeamsController.cs                    # PUT {slug}/details → 200 / coded 400 / 403 / 404
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Teams/TeamDetailsTests.cs                     # NEW: edit, rules, guard, independence, DTOs
    ├── Teams/TeamRenameRewriteTests.cs               # NEW: 9 alert kinds, former member, other team untouched, silent; placements
    └── Teams/TeamDetailsPolicyTests.cs               # NEW: pure unit tests of the URL/label rules

frontend/apps/web/src/app/
├── core/models/team.models.ts                        # TeamLink, UpdateTeamDetails, error codes; TeamDetail/TeamPublicDetail +2
├── core/services/team.service.ts                     # updateDetails()
├── core/utils/link-host.ts (+ .spec.ts)              # NEW: punycode host, "www." dropped
├── features/teams/team-settings/*                    # "Team details" section (first); form + links editor; coded errors
├── features/teams/team-detail/*                      # About card
├── features/teams/team-create/*                      # 'about' step between logo and invite
└── public/i18n/{en,de,es}.json                       # ~30 keys, one commit

specs/050-tournament-results/data-model.md            # amendment note: rename refreshes connected placements
specs/052-team-creation-wizard/spec.md                # amendment note: sixth step (about)
```

**Structure Decision**: the existing web-application layout. The feature lives in the team
domain (`Services/Teams`, `features/teams`) plus one engine method in `Services/Notifications`.

## Phases (implementation order)

1. **Backend model + migration**: entity, config, DTO shapes, migration (build first; inspect
   that the migration is non-empty).
2. **Policy + service + endpoint**: `TeamDetailsPolicy` (unit-tested), `UpdateDetailsAsync`
   (without the rewrites), controller, read projections. Tests: rules, guard, independence,
   DTOs.
3. **Rename rewrites**: `ReplaceTeamNameAsync` + placements refresh inside the transaction.
   Tests: all nine kinds, former member, other team untouched, `IsRead`/`CreatedDate`/`DedupeKey`
   unchanged, no realtime/email/push, FR-011 no-op, placements + matches, result date unchanged.
4. **Frontend settings section**: model, service, form, links editor, error mapping, i18n ×3.
5. **Team page About card** + `link-host` util.
6. **Wizard about step**.
7. **Verification**: the full suites, lint, build; the Gate 7 checklist; the browser walk
   (German, 375px + desktop); the amendment notes; the follow-up issue (profile links).

## Traps (read before implementing)

- **`ModifiedDate` on all three `ExecuteUpdate`s**: team row, placements, notifications. The
  likeliest Gate-2 failure.
- **The rewrite is by `teamSlug`, never by roster, and never by a list of types.** A roster join
  misses former members (057). A type list misses the null-dedupe party nudge rows and any future
  kind.
- **Don't touch `IsRead`/`CreatedDate`/`DedupeKey`**, and push no realtime event (FR-009 is
  silent).
- **City resolution before the transaction** (`ResolveAndUpsertAsync` saves). An existing city is
  reused without a reference lookup (R5).
- **`TeamPublicDetailDto` is positional with no defaults**: append the new members at the end.
- **The migration must be generated with a build** (056: `--no-build` gave an empty migration).
- **Refusals are coded**: the client maps `code` and never renders `detail`. The model-binding
  guards are looser than the rules on purpose (R8).
- **Zoneless**: anything the template reads for an enable/disable decision is a signal. The link
  rows' add/remove count, `saving`, and the wizard's `aboutText` are signals (045's lesson).
- **Every TestBed setup for the settings page and the wizard** needs the new service method
  stubbed where it is called. Adding a field to `TeamDetail` touches every fixture that builds one
  (TypeScript will list them).
- **The About card's links need `rel="noopener noreferrer nofollow ugc"` and `target="_blank"`**.
  A test asserts the exact attributes.

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| `links` returned as an embedded list inside two DTOs, not a paginated endpoint (Principle III "lists paginate") | The list is bounded at 5 on write. The team page must not make an extra request (SC-006) | A `PagedResult` endpoint would add a round trip and a `totalCount` promising paging that cannot exist. Precedent: `Roster` (48) and `RecentActivity` (6) on the same payload (044 recorded the same deviation) |
| One raw SQL statement (`ReplaceTeamNameAsync`) | EF Core cannot `jsonb_set`/concatenate a jsonb column in `ExecuteUpdate` | Per-row load-modify-save would be N round trips in the transaction. It is parameterised via `ExecuteSqlInterpolatedAsync`, never concatenated |

## Residuals (accepted, recorded)

- Emails and push notifications already sent keep the old name (the spec says so).
- An alert **produced concurrently** with a rename can keep the old name: the producer read the
  name before the commit and inserted after the rewrite. The window is small and self-limiting.
  Fixing it would mean locking the team row in nine producers.
- The rename's alert lookup is a sequential scan. The recorded fix is an expression index on
  `("Payload" ->> 'teamSlug')`.
- Pages already open elsewhere show the old details until reloaded.
- Links cannot be reordered except by removing and re-adding (≤5, spec assumption).
