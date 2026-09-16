# Implementation Plan: Team Logos

**Branch**: `claude/team-logo-upload-display-28bzdp` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/051-team-logos/spec.md` (GitHub #305)

## Summary

Give `Team` the fourth media descriptor — `TeamLogo` — and render it on the six surfaces that
picture a team today with a letter or a grey cluster.

**The media plumbing is not the work.** Feature 034 (`IImageProcessor`, named per-context
profiles) and feature 035 (`IMediaStore`, descriptor row + object key, the write ordering, the
`MediaResponse` cache/ETag shaping, the reconciliation sweep) already exist and already serve
three owners. This feature adds a fourth owner to a mechanism explicitly designed to take one,
and then does the display work.

**1 new entity, 1 migration, 3 new endpoints, no new package.** If a task reaches for a new
NuGet or npm dependency, a second storage path, or a polymorphic media table, something is wrong.

**⚠ THE SINGLE MOST DESTRUCTIVE THING THIS FEATURE CAN GET WRONG** is forgetting
`MediaReconciliationService`. Its own comment says it: *"⚠ THIS LIST MUST GROW WITH EVERY NEW KIND
OF STORED MEDIA. The sweep enumerates the whole container and deletes whatever it cannot account
for, so a descriptor table missing from here is not a gap in coverage — it is a table whose
objects get deleted."* A team logo omitted from that list is destroyed one grace period after
upload, silently, in every environment. FR-020 and SC-007 exist for this one line.

**Owner decisions carried from the spec's Clarifications**: all six surfaces including the chat;
**remove is supported** (a first for platform media — profile avatars have no remove); and
**centre-crop**, so the processing profile is `SquareCrop` like `Avatar`, not `Fit` like `Icon`.

**⚠ Principle VII is NOT engaged.** This feature adds no outbound integration and no new kind of
network call. Uploads and reads travel the browser→backend hop that already carries avatars, and
storage access goes through `IMediaStore`, whose implementation already inherits the shared
feature-028 resilience pipeline on its transport — that interface's own documentation forbids a
second one (*"Implementations MUST NOT contain a retry loop… that would stack a second resilience
strategy on the first"*). Adding `AddJuggerHubResilience`, a retry, or a breaker anywhere in this
diff is review-rejectable. **Gate 7 IS engaged** — six surfaces change markup and team settings
gains new copy → `checklists/ui-review.md`.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / Angular 22 (zoneless)

**Primary Dependencies**: EF Core + Npgsql; SixLabors.ImageSharp (via the existing
`IImageProcessor`); Azure Blob Storage via the existing `IMediaStore`. **No new dependency.**

**Storage**: PostgreSQL 18 for the descriptor row; object storage for the bytes

**Testing**: xUnit + Testcontainers (backend integration); Jest (frontend)

**Target Platform**: Linux containers (local compose / AKS Dev + Prod)

**Project Type**: web (backend + frontend)

**Performance Goals**: no extra round trip per browse row (SC-005) — `HasLogo` is a projected
`EXISTS`, read in the query that already builds the row

**Constraints**: stored logo ≤ the configured output ceiling, always WebP, always square; the
object key never leaves the backend (feature 035's standing rule)

**Scale/Scope**: 1 entity, 1 migration, 3 endpoints, 4 DTO fields, 6 rendering surfaces,
~10 i18n keys × 3 catalogues

## Constitution Check

| Gate | Verdict |
|---|---|
| 1. Architecture | New `ITeamLogoService` behind an interface; `TeamsController` stays thin (bind, authorize-by-delegation, shape the response). No repository layer. |
| 2. Data access | `TeamLogo : BaseEntity` (UUIDv7 PK from the base). Reads project (`.Select`) with `AsNoTracking`; `HasLogo` is an `EXISTS` inside the existing row projection, never a second query. No list is returned by this feature, so pagination is not engaged. |
| 3. Security | Admin-only writes enforced in the service through the existing `TeamAccessGuard`; a non-member gets the same answer as an unknown team (no membership oracle). Upload runs the hardened pipeline. The object key is never serialized. Errors carry the processor's non-technical reason only. |
| 4. Auth | Unchanged — cookie-borne JWT, which is also what lets an `<img>` element authenticate a logo read. |
| 5. Conventions | Angular `.html`/`.css`/`.ts` stay separate. No `.sh` added. |
| 6. Environment parity | New configuration is one `ImageProcessing:TeamLogo` section with safe built-in defaults, so local/Dev/Prod behave identically with zero configuration. No new secret. |
| 7. UI/Design | Engaged → `checklists/ui-review.md`. |
| 8. Resilience | **Not engaged** — see Summary. No new outbound integration, no new network hop, no retry/breaker in this diff. |

**Deviations**: none.

## Project Structure

### Documentation (this feature)

```
specs/051-team-logos/
├── spec.md
├── plan.md               # this file
├── tasks.md
├── contracts/
│   └── team-logo-api.md
└── checklists/
    └── ui-review.md
```

### Source code (repository root)

```
backend/
├── Entities/TeamLogo.cs                        # NEW — descriptor row
├── Data/AppDbContext.cs                        # DbSet + config + Team navigation
├── Data/Migrations/*_AddTeamLogos.cs           # NEW — one table
├── Common/ImageProcessingOptions.cs            # + TeamLogo profile
├── Services/Media/MediaObjectKey.cs            # + MediaKind.TeamLogo / "team-logos"
├── Services/Media/MediaReconciliationService.cs# ⚠ + TeamLogos to the referenced set
├── Services/Teams/ITeamLogoService.cs          # NEW
├── Services/Teams/TeamLogoService.cs           # NEW
├── Services/Teams/TeamService.cs               # delete the object when the team is deleted
├── Services/Chat/ChatAvatarUrl.cs              # + ForTeam(slug, hasLogo)
├── Services/Chat/ChatConversationService.cs    # project team slug + logo presence
├── Services/Search/TeamSearchService.cs        # + HasLogo in the card projection
├── Services/Home/HomeService.cs                # + HasLogo in MyTeamDto
├── Dtos/{Teams,Search,Home}/…                  # + HasLogo
└── Controllers/TeamsController.cs              # PUT / DELETE / GET {slug}/logo

frontend/apps/web/src/app/
├── core/models/{team,search,profile}.models.ts # + hasLogo
├── core/services/team.service.ts               # upload/remove + logoUrl(slug)
├── features/teams/team-settings/*              # the upload + remove control
├── features/teams/team-detail/*                # header tile
├── features/browse/browse-teams/*              # row tile
├── features/onboarding/*                       # suggestion tile
├── features/my-team/*                          # row tile
├── features/chat/chat-inbox/*                  # team row
├── features/chat/chat-conversation/*           # header (no change needed — see below)
└── public/i18n/{en,de,es}.json                 # ~10 keys × 3
```

## Implementation approach

### Backend

**`TeamLogo` mirrors `ProfileAvatar` deliberately, and stays a separate table.** `AppDbContext`
already carries the standing instruction not to merge the descriptor tables: a polymorphic media
row has no single owner navigation, so `ProfileAvatar`'s ban query filter could not exist. A team
logo has no equivalent filter — a team is not banned — but it joins the same family for the same
reason: one owner navigation, one cascade, one unique index.

- `TeamId` (unique), `ObjectKey`, `ContentType`, `SizeBytes`, and the `BaseEntity` fields.
- `Team.Logo` 1:1 optional, `OnDelete(Cascade)` — same shape as `PlayerProfile.Avatar`.
- No query filter. Anything a signed-in player may see of a team, they may see the logo of.

**Write ordering is copied, not invented**: mint key → put object → save descriptor → delete the
superseded object. Feature 035 chose that ordering because a row and a blob cannot share a
transaction and this is the harmless side of the unavoidable window: a failure after the put
strands an object the sweep reclaims, whereas deleting first would leave a team with no logo.

**Authorization goes through `TeamAccessGuard.ResolveAsync`**, exactly as `UpdateSettingsAsync`
does, so `NotFoundOrNotMember` and `Forbidden` come out with the same meanings as every other
team mutation, and FR-003's no-membership-oracle property is inherited rather than re-argued.

**The read endpoint is authenticated but NOT member-gated.** It lives on `TeamsController`, whose
class-level `[Authorize]` supplies the first half; the service applies no membership check, which
supplies the second. This is deliberate and is the one place where a team logo differs from every
other read on that controller — browse already shows a non-member the team's name, city and size,
and FR-009 requires logos on the browse list, which is mostly non-members by definition. The
endpoint carries `[EnableRateLimiting(RateLimitPolicies.MediaRead)]` like the other two media
reads.

**`GET` returns 404 for every refusal** — no logo, unknown team, store unavailable — mirroring
the avatar endpoint so the endpoint never becomes an existence oracle and FR-014 falls out of it:
the client's `@if` already has a placeholder branch for a failed image.

**Deleting a team deletes the object.** `TeamService.DeleteAsync` currently runs
`ExecuteDeleteAsync` on the team row; the descriptor cascades away inside PostgreSQL with no
application code running, which strands the object (the sweep would reclaim it eventually, and
`IMediaStore` documents that "application code that deletes media must delete the object
explicitly"). Read the key before the delete; delete the object after the row delete succeeds
(FR-019). Order matters the same way it does for an upload: an object deleted before a failed row
delete would leave a live team with a dead logo.

**Chat.** `ChatAvatarUrl` gains `ForTeam(slug, hasLogo)` beside `ForPlayer`, and `BuildAvatar`'s
`Team` and requester-side `TeamInquiry` branches stop hard-coding `null`. That needs two more
projected columns — the team's **slug** and whether a logo row exists — in the inbox projection,
the detail projection, and nowhere else. The 019 comment explaining why the URL was null is
replaced rather than left to contradict the code.

**`HasLogo` on four DTOs**: `TeamDetailDto` (settings + the members' header),
`TeamPublicDetailDto` (the team page), `TeamCardDto` (browse + onboarding), `MyTeamDto` ("My
team" + the home snapshot). Each is an `EXISTS` inside the projection that already runs — SC-005.
`TeamCardDto.LogoInitial` **stays**: it is the fallback, not a competitor, and removing it would
break every team that has no logo.

### Frontend

**One URL helper, one revision map.** `TeamService.logoUrl(slug)` returns
`/api/v1/teams/<slug>/logo`, with `?v=<n>` appended only for a slug whose logo this session has
changed. This is GH #283's lesson applied a second time: the URL alone never changes when the
image does, so an `<img>` element already in the DOM keeps showing the browser's cached copy —
the swap is invisible at the DOM level, not at the HTTP level (`MediaResponse` already sends
`no-cache`, so a *new* request revalidates correctly). A per-slug map rather than 035's single
global counter, so uploading one team's logo does not force every other logo on the page to
re-fetch. `logoUrl` reads a signal, so a `computed` calling it recomputes after an upload —
FR-013.

**Six render sites, one shape.** Every site keeps its existing tile geometry and swaps its
content: `@if (team.hasLogo) { <img …> } @else { <existing placeholder> }`. The chat conversation
**header needs no change at all** — it already renders `detail()?.avatar?.url` with a placeholder
branch, so filling the URL server-side is the entire change there. The chat **inbox** does need
one: its non-`Direct` branch renders the 2×2 cluster unconditionally and never looks at
`avatar.url`.

**The upload control is the avatar control's shape**: a hidden `<input type="file">` behind a
labelled button, `accept="image/png,image/jpeg,image/webp,image/gif"`. It lives in team settings
under a new "Team logo" section above Recruitment, visible only when `isAdmin()`. Remove is a
secondary button shown only when a logo exists; per DESIGN.md it is **not** a danger-zone
control — removing a logo is reversible by uploading another, unlike deleting the team.

**i18n**: every new key goes into `en`, `de` and `es` **in the same commit** — `catalog-parity.spec.ts`
compares key sets and goes red otherwise (it is the guard feature 042 added).

## Recorded residuals

- **A cropped wordmark cannot be recovered** (spec Assumptions). The original is discarded; the
  fix is to upload a squarer file.
- **The upload is not transactional with the object write.** Inherited from feature 035 and
  unchanged: the failure window strands an object, the sweep reclaims it, and no team is ever
  left with a missing image as a result.
- **Two simultaneous uploads**: last save wins; the loser's object is superseded but not deleted
  (its key was never in a row the winner could read), so the sweep reclaims it. No team ends with
  two logos and no request fails.
- **No logo history and no audit entry.** Deliberate (spec Assumptions).
- **The reconciliation sweep stays operator-triggered.** This feature adds a media kind, not a
  schedule; the 035 decision to keep a human in the loop is untouched.
- **One incidental behaviour change in the chat inbox.** Making the non-`Direct` branch render an
  avatar URL when one is present also affects a **team-inquiry row seen by an admin**, which now
  shows the asking player's avatar instead of the grey cluster. The server has always sent that
  URL; only the template ignored it, and the same conversation's header already shows it. Accepted
  as the intended reading of the DTO rather than worked around.
- **`TeamCardDto.LogoInitial` is now computed for rows that will never render it.** It is a
  single-character in-memory operation on an already-materialised page; removing it conditionally
  would make the fallback depend on the very row state the fallback exists to cover.

## Follow-ups

- Party and event crests (`ConversationAvatarDto` still hard-codes `null` for both).
- Showcase galleries (#99) — the 1:N sibling of this 1:1 image.
- A platform-admin control for removing a team's media, if moderation ever needs one; there is no
  content-removal capability anywhere in the product today (feature 041 recorded the same).
