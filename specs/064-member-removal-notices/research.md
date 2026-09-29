# Research: Removing a Member Is Confirmed, and the People It Concerns Are Told

Feature 064 · GH #385 · all decisions below were made by reading the code on `main` at `f5cf51d`.
Owner decisions are in the spec's Clarifications; this file records how they are built.

## R1 — Where the notices are produced

**Decision**: in `TeamService.MutateMembershipAsync`, after the execution-strategy delegate has
committed, beside the role-change notice that already lives there. The delegate's result grows from
`(failure, previousRole)` to a small record that also carries the removed membership's `Id`.

**Rationale**: leaving (`DELETE /teams/{slug}/members/{self}`) and removal
(`DELETE /teams/{slug}/members/{other}`) both go through `RemoveMemberAsync → MutateMembershipAsync`
with `remove: true`, and that method today returns `MemberOpResult.Ok()` *before* the notification
block (`TeamService.cs:630-633`), which is exactly the defect the issue names. Everything the notices
need (actor, target, team, whether it was self) is in hand there. The method's own comment already
states the rule this follows: side effects stay *outside* the retried delegate, because a replay
would send them twice.

**Alternatives considered**: a separate `ITeamDepartureNotices` service (an interface + DI
registration for two private calls, and a second place that has to know when a departure happened);
producing the notices in the controller (Principle II — controllers stay thin).

## R2 — Two notification types, not one

**Decision**: append `TeamMemberRemoved = 12` (to the removed player) and
`TeamMemberDeparted = 13` (to the team's admins). Both map to `InvitesAndRoster`.

**Rationale**: different recipients, different sentences, different actors, different dedupe keys.
The Alerts row, the push composer and the email all branch by type already; one type with a
"who is reading this" flag would push that branch into every renderer. `Departed` carries
`removed: bool` because "left" and "was removed" are the same event for the admins with one word
changed.

**Why a bool and not an enum**: `NotificationService.PayloadJson` has no string-enum converter, so an
enum in a payload is stored as a number (#370: every role-promotion alert reads "member"). A bool
serialises as `true`/`false` and cannot go wrong that way (058 made the same choice for
`accepted`).

## R3 — Who is named, and how (037 FR-023)

**Decision**:

| Type | Actor | Payload |
|------|-------|---------|
| `TeamMemberRemoved` | **none** | `{ teamSlug, teamName }` |
| `TeamMemberDeparted` | **the departing player** | `{ teamSlug, teamName, removed }` |

**Rationale**: an admin's alert outlives the departed player's account (erasure deletes rows by
*recipient*), and feature 037 FR-023 forbids a surviving record that identifies an erased member. So
the player is the row's actor and is never copied into the payload. Their name resolves at read time
through `n.Actor.Profile` (null once banned or erased → the client's placeholder) and at push time
through the existing `actorUserId` → `actorName` path of `IPushFanOut` (058). The removed player's
own notice has **no actor** because it must never identify the admin (FR-011) — not even through a
field the client happens not to render.

`teamSlug` in both payloads is what feature 061's rename rewrite finds rows by, so FR-023 (current
team name) is met with no new code.

## R4 — Once per departure, and a new departure is new (FR-018)

**Decision**: dedupe keys derive from the **removed `TeamMembership` row's `Id`**:
`team-removed:{membershipId}` for the player, and prefix `team-departure:{membershipId}` for the
admins (`CreateManyAsync` writes `{prefix}:{recipient}`).

**Rationale**: the unique index `(RecipientUserId, DedupeKey)` is permanent, so a key per *player and
team* would silence every later departure of the same player for ever. A membership row is deleted on
departure and a rejoin inserts a new row with a new UUIDv7, so the row id is exactly "this stay on the
team". Two admins removing the same player at once cannot both get that far: the team-row
`SELECT … FOR UPDATE` serialises them and the second finds no row (`MemberNotFound` → 404), so it
never reaches the notices.

## R5 — Who the admins are

**Decision**: read after commit — current memberships of the team with `Role == Admin`, the
recipient's account not `Banned`, and `UserId != actorUserId`. The departing player is already gone
from the table, so no extra exclusion is needed for them; the acting admin is excluded by the actor
check. Names and email addresses come in the same projection through `_db.PlayerProfiles` (never
`m.User.Profile!` — the ban filter makes that navigation misbehave, 044), exactly as 058's
`AnnounceAsync`.

**Consequence**: a team whose only admin removes a player tells no admin (spec US3 scenario 6). A
leaving admin is excluded because they are the actor *and* no longer a member.

## R6 — Email

**Decision**: three templates × en/de/es, rendered by new `IEmailTemplateService` methods and sent by
new `TeamEmailService` methods, in the recipient's saved language:

| Template | To | Says | Button |
|----------|----|------|--------|
| `removed-from-team` | removed player | "You're no longer a member of {team}." — nothing else | team page |
| `member-left` | admins | "{player} left {team}." | team page |
| `member-removed` | admins | "{player} was removed from {team}." — no admin named | team page |

Subjects, titles and footers go into `EmailLocalizer` (its reflection test asserts every key has all
three languages). Best-effort: each channel in its own `try`, and the admins' email loop wraps **each
recipient** in its own `try` so one bad address cannot stop the rest (FR-020 — 058's loop has one
`try` around the whole loop; not copied). An unresolvable player name falls back to
`MemberPlaceholder.For(culture)`, never an English literal (GH #379).

**Alternatives considered**: one admins' template with localizer-supplied headline and body
variables — rejected because the template parity guard and every other template keep prose in the
per-language HTML, and a template whose sentences live in C# is the GH #141 class this repo keeps
removing.

## R7 — Device notifications

**Decision**: explicit arms in `PushContentComposer` for both types (the exhaustive guard test fails
otherwise): title = the team name; body = `teamMemberRemoved.body` / `teamMemberLeft.body` (named) /
`teamMemberRemovedByAdmin.body` (named) with `…Anonymous` variants when the actor has no name; URL
`/t/{slug}`. Keys ×3 in `PushLocalizer`.

## R8 — Bounding joins by invitation (owner decision)

**Decision**: a new named policy `RateLimitPolicies.TeamInviteAccept = "team-invite-accept"`,
**10 per player per clock hour**, `PartitionByUser(..., TimeSpan.FromHours(1))`, applied to
`POST /api/v1/invitations/{token}/accept` only. Decline and the anonymous preview are not limited.

**Rationale**: a shared link stays `Pending` after use (`TeamInvitationService.AcceptAsync`), so its
holder can join and leave in a loop, and every leave now reaches every admin by inbox, email and
phone. Joining is the only step of the loop the player takes alone, so it is where the bound goes.
Ten matches 058's join-request limit: onboarding accepts one or two invitations; a loop stops at ten.
The route is shared by shared-link and addressed invitations; limiting it as a whole is stated in the
spec (FR-024) because accepting is one action and nobody accepts ten in an hour.

**`429` is our own limit** (Principle VII): never retried on either hop. The browser's
`retryInterceptor` already retries only `GET`/`HEAD`, so this POST is never repeated; each client
branches on the status, never the body.

**Alternatives considered**: collapsing admin notices per player per day (owner declined); limiting
the leave route (it is also the admin's removal route, so it would throttle admins tidying a
roster).

## R9 — Five accept sites must tell a limit from a dead invitation

**Decision**: every team-invitation accept call branches on `status === 429` first and shows one
shared key, `teams.inviteLimited` ("You've joined a lot of teams in a short time. Try again in a
while.") ×3:

| Site | Today on any error | With 429 |
|------|--------------------|----------|
| Alerts inbox inline Accept | marks the invite **resolved** | leave the row actionable, show the limit notice |
| *My team* invitation list | **removes** the invitation, English literal notice | keep it, show the limit notice |
| Onboarding team step | generic `acceptError` | the limit sentence |
| `/join/{slug}/{token}` page | server `detail` via `problemDetail` (#179) | the limit sentence |
| Home *Needs you* | nothing shown | the limit notice at the dashboard |

**Rationale**: two of the five treat *any* failure as "the invitation is gone" and discard a valid
invitation. Without this branch the new limit would silently cost a player their invitation.
*My team*'s existing English literal (`That invitation to … is no longer available.`) is in the same
handler and becomes a key in passing (058 did the same for onboarding's request errors).

## R10 — One confirmation dialog component

**Decision**: a new presentational `jh-confirm-dialog` in `shared/ui/confirm-dialog/`, used for the
team page's Remove and the party page's Remove and Disband (three uses, two pages). It owns what
every instance must get right: `role="dialog"` + `aria-modal` + `aria-labelledby`/`describedby`,
bottom sheet below `sm`, the **safe answer focused on open** (`afterNextRender`, zoneless — GH #344),
Tab kept inside, Escape = dismiss (ignored while busy), the destructive answer `variant="danger"`
showing a busy label and disabled while busy, and an optional error line (`role="alert"`). Inputs are
translated strings; outputs `confirmed` / `dismissed`. The host owns the state, the call and where
focus returns.

**Rationale**: the focus and keyboard rules are accessibility rules, and the product already has two
hand-copied versions (news-post's delete dialog with them, the team page's join confirmation
without). Three more copies would be five. The two existing dialogs are **not** moved in this feature
(they are not in scope and have their own tests); a follow-up issue records moving them.

**Placement**: rendered at page level, outside any `jh-card` (cards are `overflow: hidden`), where the
team page's join confirmation already sits. Neither page has a transformed ancestor (059 verified the
party page; the team page's existing fixed dialog proves it there).

## R11 — Failure handling in the dialogs (FR-004, FR-005)

**Decision**: branch on status, never the server's `detail` (#179):

| Page | Status | Result |
|------|--------|--------|
| Team remove | 404 | close, page-level note `teams.detail.removeGone`, `load()` |
| Team remove | 403 | close, page-level note `teams.detail.removeForbidden`, `load()` |
| Team remove | other | stay open, `teams.detail.removeFailed` in the dialog; confirming again is the retry |
| Party remove | 404 | close, page-level note `parties.manage.removeGone`, `reload()` |
| Party remove / disband | other | stay open, `parties.manage.removeFailed` / `disbandFailed` in the dialog |

The notes are page-level (the 060/063 lesson: a reload replaces what a card-level note would sit in).

## R12 — What does not change

- The leave flow on *Manage team* (GH #361's inline confirmation) — only the server now tells admins.
- Who may remove whom, the last-admin rule, the team-row lock, all response codes (FR-021).
- Party removal on the server (no notice, FR-009).
- `NotificationPreferenceService.CategoryCopy`: the description "Team invites, people joining or
  leaving" becomes true for leaving; no edit (FR-022).
- The privacy policy: its notification paragraph describes notices by "who or what it concerns"
  (verified for 058's requester-naming push); no legal text changes. No terms version bump.

## R13 — Principle VII / Gate 8

No new outbound integration and no new browser→backend call kind. Email and push use the existing
senders and dispatcher. The departure stays one retriable unit inside the execution strategy; notices
run after commit and are never retried here. The new `429` is our own fail-closed limit, never
retried. Wrapping anything here in `AddJuggerHubResilience` would be review-rejectable.
