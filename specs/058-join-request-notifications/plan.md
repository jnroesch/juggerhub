# Implementation Plan: Join Requests Reach the People Who Decide Them

**Branch**: `058-join-request-notifications` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/058-join-request-notifications/spec.md` (GH #360)

## Summary

A join request reaches nobody today: `TeamJoinRequestService` creates and decides requests without
a notification or an email, no `NotificationType` exists for one, and Home's *Needs you* omits it.
This feature announces every new request to **each current admin** (new `TeamJoinRequest` alert —
in-app, push and email, each by the admin's own *Invites & roster* setting), tells **the player the
answer** (new `TeamJoinRequestAnswered`, accepted → the team, declined → the team browser, never
naming the admin), and lists **waiting requests in *Needs you*** for admins with *Approve*/*Decline*
in place. Four owner decisions (spec Clarifications): a **withdrawal removes the admins' alerts**
all or nothing; **email in both directions**; **10 requests per player per hour**; and all **six
*Needs you* kinds become translatable** here.

**The load-bearing decisions, all found by reading:**

1. **The requester is the alert's *actor*, never a copy in its payload** (R1). An admin's row
   survives the requester erasing their account (`EraseOwnedDataAsync` deletes rows by
   *recipient*), and 037's FR-023 forbids a surviving record that identifies them. The name is
   resolved at read time through `n.Actor.Profile` — `null` once banned (query filter) or erased —
   and at push time through the same profile lookup (R2: `IPushFanOut`/`IPushContentComposer` gain
   one optional argument each). The payload is `{ requestId, teamSlug, teamName }`.
2. **One meaning of *waiting*** (R3): `Pending ∧ player not Banned ∧ player not already a member`,
   one expression written with correlated subqueries, used by the queue, Home, the alerts'
   *Resolved* state and the answer statements. It also fixes today's queue, which renders a
   banned requester as a nameless row linking to `/u/null`.
3. **Answered exactly once** (R4): a conditional `ExecuteUpdate … WHERE Id ∧ TeamId ∧ waiting`;
   whoever matches zero rows is told *no longer waiting*. Approve = claim + membership in one
   execution-strategy transaction; the answer notice goes out **after** commit. Today's tracked
   read-then-save lets two admins both win — invisible until the player is told.
4. **Withdrawal = 057's delete shape** (R5): conditional delete of the request + `DeleteManyAsync`
   by dedupe prefix `join-request:{requestId}` in one transaction, badges refreshed after commit.
   The conditional delete also closes a latent defect: today a withdrawal can delete an approved
   request's row by key. Joining by invitation ends a waiting request the same way (R6).
5. **Open reach needs a bound** (R7): anyone can ask any team; once each request emails and pushes
   every admin, request→withdraw loops become a flood. Policy `join-request`, 10/user/hour, on the
   create route only — the existing limiter plus a `window` parameter. Fixed window, stated
   honestly in FR-023/SC-006. Our own `429`: never retried, mapped by status to a translated line.
6. **No enum in a payload** (R8): the notification serializer has no string-enum converter, so an
   enum is stored as a number — which is why **every role-promotion alert reads "member" today**
   (`"newRole": 1` in the database). The answer carries `bool Accepted`; the defect is filed
   separately.
7. ***Needs you* carries names, not English sentences** (R11): `Title`/`Context` give way to
   `NeedsYouParamsDto`, GH #141's pattern. Breaking; front and back ship together.

**Size**: 2 notification types, 2 payload records, 1 *Needs you* kind + a reshaped item DTO, 1
rate-limit policy, 1 new service method, 1 shared predicate, 2 widened internal interfaces, 3 email
templates × 3 languages, ~13 email/push strings × 3, ~30 interface strings × 3. **No entity, no
column, no migration, no endpoint, no dependency, no configuration, no infrastructure change.**

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (zoneless, signals) with Nx (frontend)

**Primary Dependencies**: ASP.NET Core (rate limiting middleware), EF Core + Npgsql (retrying
execution strategy), StackExchange.Redis (the existing fixed-window limiter), SignalR (existing
notifications hub), Transloco (en/de/es). No new package.

**Storage**: PostgreSQL 18 — existing tables only. `Notifications.Type` is an integer, so two
appended `NotificationType` values need no migration; the preference table is sparse.

**Testing**: xUnit integration tests against the real API and a Postgres Testcontainer
(`JuggerHubApiFactory`: `TestEmailSender`, `FakePushDispatcher`, `FakeNotificationRealtime`; the
test host has no Redis, so the limiter runs its in-memory path); pure tests for the category mapping
and the composer; Jest 30 (`--testPathPatterns`) for components; the catalogue guards
(`catalog-parity`, `catalog-punctuation`); a real-browser walk (German, 375px + desktop).

**Target Platform**: Linux containers on AKS (Dev/Prod) and docker compose locally; evergreen
browsers and the installed PWA.

**Project Type**: Web application (`backend/` + `frontend/apps/web`).

**Performance Goals**: Nothing new at scale. A request fans out to one team's admins (a handful);
the *Resolved* derivation is one extra query per Alerts page that holds join-request rows; Home adds
one bounded query.

**Constraints**: The requester's identity is never copied into a stored notification. Every
`ExecuteUpdate` sets `ModifiedDate`. Side effects after commit, never inside a retried delegate. No
browser auto-retry of the new `429` or of any mutation. German at 375px is the binding layout case;
one coral CTA per view.

**Scale/Scope**: 3 backend services changed (join requests, notifications/push, Home) + email +
rate limiting; 4 frontend components (Alerts row, *Needs you* card, team page, onboarding).

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Constitution v1.4.0.*

| Gate | Verdict | Notes |
|------|---------|-------|
| **I. Security-first, never trust the client** | ✅ Pass | Who is notified is decided server-side from the membership table — never from the request. Answering keeps the guard → admin → request-scoped-to-team order, and the claim's `WHERE Id ∧ TeamId` keeps another team's request id inert. The new limit is server-side and fail-closed (R7) — the fix for an **open-reach amplification** this feature would otherwise create. No requester identity in stored payloads (R1, 037 FR-023). The client branches on status only; the server's English `detail` is never shown (R14). The admin's identity never reaches the player (FR-011). |
| **II. Thin controllers, service-centric** | ✅ Pass | The controller gains one attribute (`[EnableRateLimiting]`). Logic stays in `TeamJoinRequestService`, `NotificationService`, `PushFanOut`/`PushContentComposer`, `HomeService` and `TeamEmailService`, all behind interfaces. DTOs by explicit `.Select` (the reshaped *Needs you* item included). Rate limiting is middleware Principle II names as allowed. |
| **III. Disciplined data access** | ⚠️ Pass **with an obligation** | The two claims are `ExecuteUpdateAsync`, so **`ModifiedDate` MUST be set explicitly** on both (approve, decline); a test asserts it moves. Reads projected + `AsNoTracking`. The queue stays paginated; the *Needs you* list stays capped; no unbounded read. No entity or column added. |
| **IV. Auth & sessions** | ✅ Pass | Untouched. |
| **V. Environment parity** | ✅ Pass | No migration, no configuration. The limiter is Redis-backed in Dev/Prod (fail-fast already enforced) and in-memory only in Development, exactly like chat's. |
| **VI. Conventions & tooling** | ✅ Pass | Frontend keeps `.html`/`.css`/`.ts` separate. No script added. |
| **VII. Resilient by default** | ✅ **Not engaged as an integration** | No outbound call is added: email uses the existing sender (028), push the existing dispatcher (055). What it asks of this diff is designed in (R4, R5, R16): multi-step writes run through the execution strategy with all mutation inside the delegate and side effects after commit; the new `429` is **our own** limit and is never retried on either hop. Wrapping anything in `AddJuggerHubResilience` is review-rejectable. |
| **Gate 7 — UI/design compliance** | ✅ **Engaged** | New *Needs you* kind and wording for all six kinds, two new Alerts rows, new notices and error lines on the team page and in onboarding, two changed confirmation strings — ~30 strings × 3 → `checklists/ui-review.md` from the template, answered from screenshots. **Binding case: German at 375px** — the *Needs you* card with one item of every kind (long names wrap, buttons stay ≥44px), both Alerts rows, and the "try again later" line. |
| **Gate 8 — Resilience review** | ✅ Pass | See VII. |

**Post-Phase-1 re-evaluation**: unchanged. The design added no entity, column, endpoint,
dependency or configuration; the Principle III obligation and the open-reach bound are written into
research (R4, R7), the data model, the contracts and the test plan.

**Complexity Tracking**: not required — no violation. The transaction and the rate limit are
constitution-mandated responses, not added complexity.

## Project Structure

### Documentation (this feature)

```text
specs/058-join-request-notifications/
├── plan.md                        # This file
├── spec.md                        # FR-001…FR-026 (+FR-019a), SC-001…SC-009, owner clarifications
├── research.md                    # R1–R17
├── data-model.md                  # waiting, transitions, types/payloads, Needs-you shape, limit, email
├── quickstart.md                  # automated checks + the German 375px/desktop walk
├── contracts/
│   ├── join-requests-api.md       # the four routes' new behaviour + internal interface changes
│   ├── notifications.md           # the two types, payloads, push and email
│   └── home-needs-you.md          # the reshaped item (breaking)
├── checklists/
│   ├── requirements.md            # spec quality (done)
│   └── ui-review.md               # Gate 7, created during implementation
└── tasks.md                       # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/NotificationEnums.cs                       # + TeamJoinRequest = 9, TeamJoinRequestAnswered = 10; category mapping
├── Dtos/Notifications/NotificationDtos.cs              # + 2 payload records; Resolved doc widened
├── Dtos/Home/HomeDtos.cs                               # NeedsYouKind.JoinRequest; NeedsYouItemDto reshaped; NeedsYouParamsDto
├── Security/RateLimitPolicies.cs                       # + JoinRequest (10/hour); window parameter
├── Controllers/TeamsController.cs                      # [EnableRateLimiting] on RequestToJoin; doc comments
├── Services/Teams/JoinRequestWaiting.cs                # NEW — the shared "waiting" predicate
├── Services/Teams/ITeamJoinRequestService.cs           # + EndForMemberAsync
├── Services/Teams/TeamJoinRequestService.cs            # announce, answer once, withdraw with alerts, end-for-member, queue predicate
├── Services/Teams/TeamInvitationService.cs             # AcceptAsync → EndForMemberAsync (best-effort)
├── Services/Notifications/IPushFanOut.cs               # + actorUserId
├── Services/Notifications/NotificationService.cs       # pass actor to fan-out; Resolved for join requests
├── Services/Notifications/Push/PushFanOut.cs           # resolve the actor's name once
├── Services/Notifications/Push/PushContentComposer.cs  # + actorName; arms for both types
├── Services/Notifications/Push/PushLocalizer.cs        # + 4 keys × 3
├── Services/Home/HomeService.cs                        # Needs you: params for all kinds; join requests
├── Services/Email/TeamEmailService.cs                  # + IEmailLocalizer; 3 localized sends
├── Services/Email/EmailLocalizer.cs                    # + subject/title/footer × 3 kinds × 3
├── Services/EmailTemplateService/{I,}EmailTemplateService.cs  # + 3 generators
├── EmailTemplates/{en,de,es}/join-request{,-accepted,-declined}.html   # NEW × 9
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Teams/JoinRequestNotificationTests.cs           # NEW — US1–US3, exactly-once, ban/erasure, join-by-invite
    ├── Teams/JoinRequestRateLimitTests.cs              # NEW — the 11th request
    ├── Home/NeedsYouTests.cs                           # + join-request items; params for every kind
    ├── Notifications/NotificationCategoryMappingTests.cs  # + the two types
    ├── Push/PushComposerTests.cs                       # + arms; NEW exhaustive every-type guard
    ├── Push/NotificationChannelIndependenceTests.cs    # + the four-way matrix for TeamJoinRequest
    └── Email/Template{Parity,RenderMatrix}Tests.cs     # + the three templates

frontend/apps/web/
├── public/i18n/{en,de,es}.json                         # alerts.row.*, home.needsYouItem.*, teams.detail.*, onboarding.team.*
└── src/app/
    ├── core/models/notification.models.ts              # + 2 types, payloads, narrowing helpers
    ├── core/models/home.models.ts                      # NeedsYouKind + 'JoinRequest'; NeedsYouItem reshaped
    ├── features/alerts/notification-row/notification-row.component.{ts,html,spec.ts}
    ├── features/dashboard/modules/needs-you-card.component.{ts,html,spec.ts}
    ├── features/teams/team-detail/team-detail.component.{ts,html,spec.ts}
    └── features/onboarding/onboarding.component.{ts,html,spec.ts}
```

**Structure Decision**: the existing web-application layout; every change lands in the file that
already owns the concern. New files: one predicate, two test suites, nine email templates.

## Implementation approach

### Backend

1. **Types and payloads.** Append `TeamJoinRequest = 9` and `TeamJoinRequestAnswered = 10` with
   doc comments saying who receives each and what the payload deliberately lacks; map both to
   `InvitesAndRoster` and extend the mapping test's two lists. Add
   `TeamJoinRequestPayload(Guid RequestId, string TeamSlug, string TeamName)` and
   `TeamJoinRequestAnsweredPayload(string TeamSlug, string TeamName, bool Accepted)`, each with a
   comment on what it must never carry (R1, R8).
2. **The waiting predicate** (R3). `JoinRequestWaiting.Predicate(AppDbContext db)` →
   `Expression<Func<TeamJoinRequest, bool>>` over correlated `NOT EXISTS` subqueries, with a doc
   comment listing its four readers and why it has no navigations.
3. **Push seam** (R2). `IPushFanOut.FanOutAsync(…, Guid? actorUserId = null, ct)` (the parameter
   sits before `ct`, so both call sites in `NotificationService` must be touched — a compile error
   rather than a silent miss); `PushFanOut` resolves the display name once via `PlayerProfiles`;
   `IPushContentComposer.Compose(…, string? actorName = null)`; arms in `TitleFor`, `BodyFor`,
   `UrlFor` for both types; four `PushLocalizer` keys × 3.
4. **Notification list** (R10). In `ListAsync`, collect `requestId`s from the page's
   `TeamJoinRequest` payloads (a `TryGetGuid` helper beside `TryGetInvitationId`), one query with
   the predicate, `Resolved = !waiting` for those rows. Widen `NotificationDto`'s doc.
5. **Join-request service.**
   - `RequestAsync` unchanged until `Created`; then `AnnounceAsync` — admins' ids + address + name +
     language in **one** projection, team slug/name, the requester's current name (email only);
     `CreateManyAsync(admins, TeamJoinRequest, payload, actorUserId: player, prefix
     "join-request:{id}")` and the email loop, each in its own `try/catch` (the `TeamNewsService`
     shape). Never on `AlreadyPending`.
   - `CancelAsync` (R5): pre-read the id → strategy { conditional `ExecuteDelete`; if 1 row →
     `DeleteManyAsync`; commit } → `RefreshUnreadBadgesAsync`. Extract the core as a private
     `EndWithoutAnswerAsync(requestId)` shared with `EndForMemberAsync` (R6).
   - `ApproveAsync` (R4): guard → admin → strategy { `ChangeTracker.Clear()`; begin; claim
     (`SetProperty` Status, DecidedByUserId, DecidedDate, **ModifiedDate**) → 0 ⇒ not waiting; add
     the `Member` membership; `SaveChanges`; commit } — a unique violation from a concurrent
     invitation maps to *not waiting*. After commit: `NotifyAnswerAsync(accepted: true)`.
   - `DeclineAsync`: guard → admin → one claim (**ModifiedDate** set) → 0 ⇒ not waiting; then
     `NotifyAnswerAsync(accepted: false)`.
   - `NotifyAnswerAsync`: `CreateAsync(player, TeamJoinRequestAnswered, payload, actorUserId: null,
     dedupeKey "join-answer:{id}")` + the email by the player's setting and language; each
     `try/catch`; logs carry ids, never names.
   - `ListPendingAsync`: apply the predicate. `EndForMemberAsync(teamId, userId)`: find the waiting
     id → shared core.
6. **Invitation accept.** After `Joined` or `AlreadyMember`, `EndForMemberAsync` in a `try/catch`
   (logged). The rest of `AcceptAsync` is untouched.
7. **Rate limit** (R7). `JoinRequest = "join-request"`, `JoinRequestsPerHour = 10`,
   `PartitionByUser(policy, limit, window)` / `Limiter(…, window)` with a one-minute default; one
   `options.AddPolicy`; `[EnableRateLimiting(RateLimitPolicies.JoinRequest)]` on `RequestToJoin`
   only, with the fixed-window and "our own 429" notes written where they apply (Principle VII
   requires the distinction be explicit).
8. **Home** (R11). `NeedsYouParamsDto` (init-only, nullable, like `ActivityParamsDto`),
   `NeedsYouItemDto(Kind, Id, Params, LinkTarget, OccurredAt)`, the five existing projections moved
   to params, and the join-request query: admin team ids → predicate → newest first →
   `Take(cap)` → merged. Update the class and DTO doc comments that describe the old prose.
9. **Email** (R12). Three generator methods, three `TeamEmailService` sends (with
   `IEmailLocalizer`), nine templates cloned from `party-request.html`'s structure (eyebrow, h1,
   greeting, one paragraph, button, alt-link), nine `EmailLocalizer` keys × 3 with positional args.

### Frontend

1. **Models.** Two notification types + payloads + narrowing helpers; `NeedsYouKind` +
   `'JoinRequest'`; `NeedsYouItem` with `params`.
2. **Alerts row.** Links, titles, supporting lines and icons for both types (`user-plus` for the
   request, `users` for the answer). The player's name is `actorDisplayName ??` the translated
   former-player placeholder (same words as the server's `MemberPlaceholder`). `resolved` switches
   the request's supporting line to *no longer waiting*. Link-only — no inline actions.
3. ***Needs you* card.** A `{ key, params }` helper per kind feeding the `transloco` pipe (the pipe
   re-renders on a language switch; nothing is composed in TypeScript strings). Join requests:
   the player's name links to `/u/{handle}`, *Annehmen*/*Ablehnen* call the existing
   `approveJoinRequest`/`declineJoinRequest` with `params.teamSlug`; a `404` puts the id in a
   `stale` signal (item hidden) and sets a card-level `role="status"` notice. All new state is
   signals (zoneless).
4. **Team page.** `requestToJoin`/`cancelRequest`/`approve`/`decline` branch on status to keys
   (R14); approve/decline `404` reloads the queue and sets a neutral `joinNotice` rendered above the
   queue position, independent of the queue's visibility. `confirmJoinBody` gains "you'll be told".
5. **Onboarding.** The team-request error signal holds a key (429 / 409 / other), rendered with
   `| transloco`; `askedConfirmation` gains "you'll be told".
6. **Copy.** Every key in **all three catalogues in one commit** (`catalog-parity` fails
   otherwise); German `–` never `—`; Spanish raya only paired; errors open with *We couldn't* /
   *Wir konnten* / *No pudimos*; English *Needs you* strings verbatim from today's server (R11).

### Specs and follow-ups

- 009's spec line 116 ("Notifications for request/approval are placeholder") gains a one-line
  "Fulfilled by 058" pointer; nothing else in 009/010/025 changes meaning. 025's *Needs you*
  contract gets a pointer to [contracts/home-needs-you.md](./contracts/home-needs-you.md).
- File the three R17 defects as issues.

## Recorded residuals

- **Email and push are not recalled.** After a withdrawal or an answer, an admin's mailbox or lock
  screen may still say someone wants to join. The inbox row is removed (withdrawal) or reads as
  answered.
- **A commit-time connection drop** on approve can report *already answered* to the deciding admin
  and leave the player untold, though they are a member (R4).
- **Fixed window**: up to 20 requests across the turn of an hour (R7, FR-023, SC-006).
- **Former admins keep their alert.** An admin demoted after the request keeps a row that opens a
  team page where they can no longer answer.
- **Other people's open pages don't update live.** An open Alerts inbox keeps a withdrawn request's
  row until reload; only the badge updates.
- **The prefix lookup is a sequential scan** of `Notifications` (057's recorded trade-off), now also
  run on withdrawal and on joining by invitation.
- **Joining by invitation ends the request best-effort.** If that cleanup fails, the predicate hides
  the row everywhere and a log line records it; the admins' alerts then read as no longer waiting
  instead of disappearing.

## Follow-ups

- Role-change payloads store `newRole` as a number — promotion alerts read "member" (R8, R17).
- `TeamInvitePayload.InviterName` survives the inviter's erasure (R1, R17).
- Marketplace applications notify no party admin (spec → Out of scope).
