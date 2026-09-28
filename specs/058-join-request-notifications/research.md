# Research: Join Requests Reach the People Who Decide Them

**Feature**: 058 · **Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

Every decision below was taken by reading the code named in it. Where the code contradicted the
issue, the code won and the spec says so.

---

## R1 — The requester is the alert's actor, never a copy in its payload

**Decision**: the admins' `TeamJoinRequest` rows carry `ActorUserId = requester` and a payload of
`{ requestId, teamSlug, teamName }` — **no name, no handle**. The requester's name is resolved at
read time by the path every row already uses: `NotificationService.ListAsync` projects
`n.Actor.Profile.DisplayName`, which is `null` when the requester is banned (the `PlayerProfile`
query filter hides the profile) or erased (037 deletes the profile row). The client renders `null`
as the neutral placeholder (FR-005).

**Rationale**: an alert is the *recipient's* data. `AccountDeletionService.EraseOwnedDataAsync`
deletes notifications where the erased member is the **recipient**
(`Notifications.Where(n => n.RecipientUserId == userId)`), never where they are the actor — so an
admin's alert about Jonas survives Jonas erasing his account. Feature 037's FR-023 requires that
"it MUST NOT be possible to recover the member's identity from a surviving record", and its
data-model records `Notification.Actor` as the mechanism that already satisfies it (the reference
resolves to a neutralised account). A copied name would sit outside that mechanism for ever.

**Alternatives rejected**:
- *Name and handle in the payload*, as the issue proposed — violates 037 FR-023 the moment the
  requester erases their account; the JSON would have to be rewritten in other people's rows.
- *Scrub payloads during erasure* — adds JSON surgery on other members' data to
  `EraseOwnedDataAsync`, the most regression-sensitive method in the codebase, to repair a copy
  that need not exist.

**Found while reading**: `TeamInvitePayload.InviterName` already copies the inviter's name into the
invitee's row. The UI hides it once the invite is resolved (erasure deletes the invite), but the API
still returns it. A pre-existing FR-023 gap → follow-up issue (R17).

---

## R2 — Push names the requester by resolving the actor at dispatch time

**Decision**: widen the 055 seam by one optional argument each —
`IPushFanOut.FanOutAsync(…, Guid? actorUserId, …)` and
`IPushContentComposer.Compose(…, string? actorName = null)`. `NotificationService` already holds
`actorUserId` at both call sites and passes it through. `PushFanOut` resolves the name **once per
fan-out** (not per recipient) through `PlayerProfiles` — so a banned actor resolves to `null` — and
hands it to the composer. The `TeamJoinRequest` body uses it: *"{0} wants to join the team"*, with a
name-less variant (*"Someone wants to join the team"*) when it is `null`.

**Rationale**: the lock screen is where "who is asking" matters most, and 055's owner decision is
that notifications name their subject (the TeamInvite push already names the inviter). The push is
composed and handed off at the moment of the request, when the requester is live; nothing about it
is stored, so R1 is untouched. Only one implementation of each interface exists
(`PushFanOut`, `PushContentComposer`) and no test fakes either, so the change is contained.

**Alternatives rejected**: a name in the payload (R1); a name-less body for every request (drops the
single most useful word); a separate push-only payload on `CreateManyAsync` (a second payload
channel through the engine for one type).

**Cost**: one indexed `PlayerProfiles` lookup per fan-out that has push recipients
(`FanOutAsync` returns before querying when the recipient list is empty). Other types ignore the
name.

---

## R3 — One meaning of "waiting", shared by every surface

**Decision**: a single predicate, `JoinRequestWaiting.Predicate(AppDbContext db)`, written with
**correlated subqueries only** (no navigations):

```text
r.Status == Pending
∧ NOT EXISTS (Users u WHERE u.Id = r.UserId ∧ u.Status = Banned)
∧ NOT EXISTS (TeamMemberships m WHERE m.TeamId = r.TeamId ∧ m.UserId = r.UserId)
```

Used by: the team page's queue (`ListPendingAsync`), Home's *Needs you*, the *no longer waiting*
state of admins' alerts (R10), and the answer statements (R4).

**Rationale** (FR-015, SC-003): four surfaces that each decide "waiting" for themselves will drift —
019 watched exactly that happen to chat membership. The two extra clauses each close a real gap:

- **Banned** — `TeamJoinRequestService.ListPendingAsync` projects `r.User.Profile!.Handle`; for a
  banned requester the filtered profile makes that `null`, so the queue today renders a nameless row
  linking to `/u/null`. Excluding them fixes the queue and keeps Home consistent. A ban is
  reversible (`AdminUserService.UnbanAsync`), and nothing is deleted, so an unbanned requester's
  request waits again (FR-021).
- **Already a member** — `TeamInvitationService.AcceptAsync` creates memberships without touching
  the player's pending request, so a player who joins by invitation leaves a request behind asking
  admins to decide on a member. R6 ends it at the source; this clause is the safety net for R6's
  best-effort cleanup and for rows written before this feature.

Correlated subqueries rather than navigations so the same expression is safe inside
`ExecuteUpdate`/`ExecuteDelete` (R4): the target row's own conditions stay in the outer `WHERE`,
which is what PostgreSQL re-checks after waiting for a row lock.

---

## R4 — A request is answered exactly once

**Decision**: an answer is a **conditional claim** —
`ExecuteUpdate(Status, DecidedByUserId, DecidedDate, ModifiedDate) WHERE Id ∧ TeamId ∧ waiting` —
and whoever matches **zero rows** is told *no longer waiting*.

- **Approve** = claim + membership insert, one transaction through the execution strategy
  (`ChangeTracker.Clear()` first, the `TeamService.MutateMembershipAsync` shape). A membership
  created concurrently by an invitation raises a unique violation, which rolls the whole unit back
  and is mapped to *no longer waiting* — the player joined another way.
- **Decline** = the claim alone. One statement is atomic on its own, and the retrying strategy
  retries a single statement without a user transaction.
- The answer notice (R8) is sent **after** commit, outside the retried delegate, so a replay can
  never send it twice.

**Rationale** (FR-012): today both methods read the request tracked and save it — two admins who
read *Pending* at the same moment both save, and last-writer-wins can even leave a *Declined*
request beside an approval's membership. Once each answer is sent to the player, that becomes
"accepted" and "declined" in one inbox. A conditional `UPDATE … WHERE "Status" = 0` makes the
database the arbiter: the second statement waits on the first's row lock, then PostgreSQL re-checks
the `WHERE` against the committed row (READ COMMITTED) and matches nothing. The same holds for an
answer racing a withdrawal (R5).

**No team-row lock**: `MutateMembershipAsync` locks the team row to protect the *admin count*
invariant; approval always adds a `Member`, so it cannot break that invariant, and the request row's
own lock is the right grain.

**Residual (recorded)**: if the connection drops *during* a commit that actually succeeded, the
strategy replays the delegate, the claim matches nothing, and the deciding admin is told the request
was already answered — while the player is in fact a member and is not told. Rare, truthful to the
admin, and the same class every strategy block in the codebase accepts.

---

## R5 — A withdrawal takes its alerts with it, all or nothing

**Decision** (FR-022, owner decision): `CancelAsync` reads the caller's waiting request id
(`AsNoTracking`), then inside the execution strategy: begin → `ExecuteDelete WHERE Id ∧ Status =
Pending` → **0 rows ⇒ stop** (it was answered meanwhile; still a 204 to the caller) →
`INotificationService.DeleteManyAsync(TeamJoinRequest, "join-request:{id}")` → commit. After
commit: `RefreshUnreadBadgesAsync(recipients)`.

**Rationale**: this is 057's delete shape exactly, reusing its engine methods — rows found by
**dedupe-key prefix**, never by the roster (an admin demoted since still holds the row), deletion in
the same transaction so a failure cannot leave alerts pointing at a request that no longer exists,
the badge refreshed only after commit so a rollback cannot contradict it.

**Closes a latent defect**: today `CancelAsync` loads the row tracked and `Remove`s it — EF deletes
**by key**, so a withdrawal that read *Pending* just before an admin approved deletes the approved
request's audit row. The conditional delete cannot.

---

## R6 — Joining another way ends the request

**Decision** (FR-020): `ITeamJoinRequestService.EndForMemberAsync(teamId, userId)` — R5's core,
keyed by (team, player) — called by `TeamInvitationService.AcceptAsync` after a membership is
created (`Joined`) or found (`AlreadyMember`). Best-effort: logged, never thrown.

**Rationale**: the membership is committed and must not be undone because a cleanup failed; R3's
*not a member* clause guarantees a leftover row never reads as waiting anywhere. Nobody is notified
(FR-013) — the player is already in, and `DeleteManyAsync` pushes nothing.

**Alternatives rejected**: folding the cleanup into `AcceptAsync`'s own `SaveChanges` (would mean
restructuring the invite path — 053's onboarding flow — into a strategy transaction for a benefit
R3 already provides); predicate-only (a stale row would resurrect as *waiting* the day the member
leaves the team).

---

## R7 — Ten requests per player per hour

**Decision** (FR-023, owner decision): `RateLimitPolicies.JoinRequest = "join-request"`,
**10 per hour**, partitioned by user, on `POST /teams/{slug}/join-requests` **only** — withdrawing
and answering are never limited. `PartitionByUser`/`Limiter` gain a `window` parameter defaulting to
one minute, so the four existing policies are byte-for-byte unchanged.
`RedisFixedWindowRateLimiter` already takes its window as a constructor argument
(`key = floor(unixSeconds / window)`, TTL = window + 1 s) and needs no change.

**Rationale**: join requests have **open reach** — any signed-in player, any team, no relationship
needed — which is precisely why 019 made chat's limits load-bearing. Until now a request reached no
one, so the reach was harmless; this feature turns each one into an email and a push to every admin.

**Semantics stated honestly**: the limiter is a *fixed* window (the owner chose the existing
limiter), so the bound is 10 per clock hour and a burst straddling the hour can reach 20 — spec
FR-023 and SC-006 say exactly that. Chat's limits carry the same property.

**Principle VII**: this `429` is **our own** fail-closed limit — never retried on either hop (the
browser retry interceptor already skips 429, feature 028), and the client branches on the status to
a translated "try again later". A Redis outage rejects join requests (fail-closed), like chat; the
community is not exposed.

**Tests** run without Redis (`UseEnvironment("Development")`, no Redis configured), so they
exercise the in-memory fixed-window path — correct on the single test host, and partitioned per
user, so other suites' requests never share a bucket.

---

## R8 — One answer type with a boolean — never an enum in a payload

**Decision**: `NotificationType.TeamJoinRequestAnswered` with
`TeamJoinRequestAnsweredPayload(TeamSlug, TeamName, bool Accepted)`, and
`NotificationType.TeamJoinRequest` for the admins' alert. Both in `InvitesAndRoster`, whose
description already reads "Team invites, people joining or leaving". Both **appended** (9, 10).

**Rationale**: one type with a discriminator is the house shape (`TeamRoleChanged` + `newRole`,
`TrainingUpdated` + `kind`); two types would double every exhaustive arm (category mapping, three
composer switches, client narrowing, row title/supporting/link/icon).

**Why a bool, not `JoinRequestStatus`**: `NotificationService.PayloadJson` is
`new(JsonSerializerDefaults.Web)` with **no** string-enum converter — the global
`JsonStringEnumConverter` is registered on MVC's options only (`Program.cs:47`). An enum in a
payload is stored as a **number**. Verified against the local database:

```text
SELECT "Payload" FROM "Notifications" WHERE "Type" = 1   -- TeamRoleChanged
{"newRole": 1, "teamName": "Rheinfeuer Köln", "teamSlug": "w57-msukfx"}
```

`notification-row.component.ts` compares `n.payload.newRole === 'Admin'`, and
`HomeService.StateChangeEntry` reads `newRole` as a string — so **every promotion alert reads
"You're now a member of …"** and Home's activity entry has no role. A pre-existing defect, out of
scope here → follow-up issue (R17). This feature's payloads carry no enum.

---

## R9 — Dedupe keys

| Row | Key | Why |
|-----|-----|-----|
| Admin alert | prefix `join-request:{requestId}` → `join-request:{requestId}:{adminId}` | Per request: a re-request after a withdrawal or a decline is a new announcement, and R5 finds exactly this request's rows. Also the push tag, so a device replaces rather than stacks. |
| Answer notice | `join-answer:{requestId}` | At most one answer per request even if the producer ran twice. |

Both fit `DedupeKey`'s 200 characters (86 and 48). No index change: the prefix lookup is 057's
sequential scan, run only on a withdrawal or a join-by-invitation.

---

## R10 — "No longer waiting" is derived when the inbox is read

**Decision** (FR-007): extend `NotificationDto.Resolved` — today TeamInvite-only — to
`TeamJoinRequest`. `ListAsync` collects the page's `requestId`s from the payloads and runs **one**
query with R3's predicate; `Resolved = !waiting`. The row's supporting line switches to *no longer
waiting*. Nothing is written when a request is answered.

**Rationale**: the same mechanism, for the same reason, as invitations (010): an alert follows the
state of what it announced without anyone rewriting other people's rows. It is also correct for the
cases nobody announces — the requester banned, erased, or the team deleted. Rows arriving over
SignalR are always fresh requests, so `Resolved: false` there stays right.

**Alternatives rejected**: deleting or rewriting every admin's row on answer (write amplification
across inboxes, and it erases history the owner chose to remove only on withdrawal).

---

## R11 — *Needs you* carries names, not English sentences

**Decision** (FR-018, FR-019, FR-019a, owner decision): `NeedsYouItemDto` loses `Title`/`Context`
and gains `NeedsYouParamsDto Params` — `TeamName`, `TeamSlug`, `EventName`, `PlayerName` — the
exact pattern GH #141 established for *What's going on* (`ActivityEntryDto` + `ActivityParamsDto`).
The client composes each kind's sentence from `home.needsYouItem.*` keys. `NeedsYouKind.JoinRequest`
is appended.

- **Join request** items: every team where the viewer is **currently** admin, R3's predicate, newest
  first, merged and capped with the rest. `Id` = request id, `LinkTarget` = the requester's handle
  (FR-019: open their profile), `Params.TeamSlug` = the team whose endpoints answer it.
  `PlayerName` is projected through `_db.PlayerProfiles.Where(p => p.UserId == r.UserId)` — the
  044/HomeService sub-projection, never `r.User.Profile!` (the ban filter makes the navigation
  misbehave).
- **The five existing kinds** keep their ids, links and actions. Their **English** keys reproduce
  today's server strings verbatim ("{{team}} invited you" / "to join the team", "{{team}} is
  fielding a party", …), so an English reader sees no change; German and Spanish are new.

**Breaking contract**, shipped front and back together (020 and 042 did the same). Nothing else
consumes `NeedsYouItemDto`: the existing backend tests read only `kind`, `id` and `linkTarget`, and
no e2e spec touches *Needs you*.

---

## R12 — Email in both directions, localized (039 pattern)

**Decision** (FR-024, owner decision): three templates × en/de/es — `join-request.html` (to each
admin), `join-request-accepted.html`, `join-request-declined.html` (to the player) — generated by
three new `IEmailTemplateService` methods and sent by `TeamEmailService`, which gains
`IEmailLocalizer` exactly as `PartyEmailService` has it. Subjects, titles and footers are
positional-argument keys in `EmailLocalizer`. Recipient language = stored `PreferredLanguage`
(`SupportedLanguages.ResolveOrDefault`), read in the same projection as the address (039's rule: no
per-recipient culture lookup inside the send loop). Gated by *Invites & roster → Email* per
recipient; wrapped like every existing producer's email so a failed send never fails the request
or the answer.

**Guards**: the three templates join `TemplateParityTests.FullyTranslatedTemplates` (same
placeholder set in every language, every variant present) and `TemplateRenderMatrixTests`
(each renders in each language). The team emails that stay English-only (`invitation`,
`team-news`, `team-role-changed`; #84/#77) are not touched.

**Copy**: the admin email names the requester — that is its point — and the player's answer never
names the admin (FR-011). No email promises anything the product does not do.

---

## R13 — Push copy, and a guard that every type has its own

**Decision** (FR-025, issue point 4): arms in `TitleFor` (team name), `BodyFor` and `UrlFor` for
both types, four new `PushLocalizer` keys × 3 languages, and a **new exhaustive guard** in
`PushComposerTests`: one representative payload per `NotificationType`, iterating
`Enum.GetValues<NotificationType>()`, asserting a non-fallback title, a non-fallback body and a
non-`/` URL. A type added later without arms fails it — the `NotificationCategoryMappingTests`
pattern applied to the composer's three `_ =>` arms.

| Type | Title | Body | Opens |
|------|-------|------|-------|
| `TeamJoinRequest` | team | "{0} wants to join the team" / "Someone wants to join the team" | `/t/{slug}` |
| `TeamJoinRequestAnswered`, accepted | team | "Your request to join was accepted" | `/t/{slug}` |
| `TeamJoinRequestAnswered`, declined | team | "Your request to join was declined" | `/browse/teams` |

URLs stay app-relative and slug-validated through the composer's existing `Slug()` guard.

---

## R14 — The client branches on status, never on the server's English

**Decision**: every new error path maps a **status** to a catalogue key (the #179 / 052 rule):

| Surface | Status | Shows |
|---------|--------|-------|
| Team page *Request to join*, onboarding *Ask to join* | 429 | "You've sent a lot of requests in a short time. Try again in a little while." |
| same | 409 | already a member (existing meaning, now translated on both surfaces) |
| same | other | "We couldn't send your request just now." |
| Team page *Approve*/*Decline* | 404 | neutral notice "This request was already answered or withdrawn." + queue reload |
| Home *Needs you* join request | 404 | the item leaves the list; the card shows the same notice |

Onboarding's team-request handler holds two hard-coded **English** strings today
(`onboarding.component.ts`, "You're already on that team." / "We couldn't send that request just
now."). The 429 branch lands in the same handler, so all three become keys — fixing an untranslated
message on the path this feature touches. The team page keeps `problemDetail()` for its unrelated
actions.

---

## R15 — The privacy policy needs no change (verified, German authoritative)

`public/i18n/legal/de.json` line 183 describes what a device notification carries: *"worum es geht,
wen oder was es betrifft, und wer dir geschrieben hat"* — a push naming the requester is *wen es
betrifft*. Emails are covered by the processor paragraph (line 181: *"Resend stellt unsere E-Mails
zu und sieht dabei zwangsläufig, an wen sie gehen und was darin steht."*). No sentence in any of the three catalogues states what notifications do *not*
contain, which is the shape that goes false (056's lesson). Nothing to edit; `legal-catalog.spec.ts`
is unaffected.

---

## R16 — Principle VII is not engaged as a new integration

No outbound call is added. Email goes through the existing `IEmailSender` (028's resilience), push
through 055's dispatcher. What the constitution *does* ask of this diff, and the design gives it:
the two multi-step writes (approve; withdraw / end-for-member) run through the execution strategy
with all mutation inside the delegate and every side effect after commit; the new `429` is our own
limit and is never retried. Wrapping anything here in `AddJuggerHubResilience` would be
review-rejectable.

---

## R17 — Defects found while reading, filed rather than fixed

1. **Role-change payloads store `newRole` as a number** (R8) — every promotion alert reads "member",
   and Home's activity entry loses the role.
2. **`TeamInvitePayload.InviterName` survives the inviter's erasure** (R1) — hidden in the UI once
   resolved, still returned by the API; 037 FR-023.
3. **Marketplace applications notify no party admin** — the same gap as #360 in the other
   direction (spec → Out of scope).
