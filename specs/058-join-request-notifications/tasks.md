---

description: "Task list for 058 — join requests reach the people who decide them"
---

# Tasks: Join Requests Reach the People Who Decide Them

**Input**: Design documents from `specs/058-join-request-notifications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included, and not optional. Exactly-once answers (FR-012), "nobody else is notified"
(FR-001), the absent name in a stored alert (FR-005), the channel independence (FR-002) and the
Principle III `ModifiedDate` obligation are only provable by tests.

**Organization**: by user story. US1 = the admins hear (P1, MVP), US2 = the player hears the answer
(P1), US3 = withdrawn and repeated requests do not pile up (P1, **ships with US1**), US4 = waiting
requests on Home (P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 (setup, foundational and polish carry none)

---

## Phase 1: Setup

**Purpose**: know the baseline before touching anything, so a later failure is attributed to the
right change (the cross-PR attribution lesson).

- [X] T001 On the untouched branch run `dotnet build backend/JuggerHub.slnx`, `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~JoinRequest|FullyQualifiedName~NeedsYou|FullyQualifiedName~PushComposer|FullyQualifiedName~NotificationCategoryMapping|FullyQualifiedName~Template"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="notification-row|needs-you-card|team-detail|onboarding|dashboard|catalog-"`; record any pre-existing failure under **Notes** at the end of `specs/058-join-request-notifications/tasks.md`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the two notification types, their payloads, the one meaning of *waiting*, the widened
push seam with every push arm, the three localized emails, the frontend notification model and all
new copy. Every story builds on these.

**⚠️ No user-story work starts before this phase's checkpoint.**

- [X] T002 [P] In `backend/Entities/NotificationEnums.cs` append `TeamJoinRequest = 9` and `TeamJoinRequestAnswered = 10` (appended, never inserted — stored as integers). Doc comments: the first goes to each admin, is link-only, and its requester is the row's **actor** and deliberately not in the payload (an admin's row outlives the requester's account; 037 FR-023); the second goes to the player, is link-only, says accepted or declined and never who answered (FR-011). Map both to `NotificationCategory.InvitesAndRoster` in `NotificationCategories.For` and extend that category's summary to name them
- [X] T003 [P] In `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/NotificationCategoryMappingTests.cs` add `[InlineData]` rows for both types → `InvitesAndRoster` and add both to the `covered` array of `Every_notification_type_has_an_explicit_mapping` (depends on T002)
- [X] T004 [P] In `backend/Dtos/Notifications/NotificationDtos.cs` add `TeamJoinRequestPayload(Guid RequestId, string TeamSlug, string TeamName)` and `TeamJoinRequestAnsweredPayload(string TeamSlug, string TeamName, bool Accepted)`, each documented with what it must never carry (the requester's name or handle — research R1; an enum — `PayloadJson` has no string-enum converter, R8; an admin's identity — FR-011). Widen `NotificationDto`'s summary: `Resolved` now applies to `TeamInvite` **and** `TeamJoinRequest` (true when the request no longer waits)
- [X] T005 [P] Create `backend/Services/Teams/JoinRequestWaiting.cs`: `internal static class JoinRequestWaiting` with `public static Expression<Func<TeamJoinRequest, bool>> Predicate(AppDbContext db)` = `r.Status == JoinRequestStatus.Pending && !db.Users.Any(u => u.Id == r.UserId && u.Status == AccountStatus.Banned) && !db.TeamMemberships.Any(m => m.TeamId == r.TeamId && m.UserId == r.UserId)`. Doc comment (research R3): the one meaning of *waiting* (FR-015); its four readers (queue, Home, the alerts' `Resolved`, the answer claims); why correlated subqueries and **no navigations** (safe inside `ExecuteUpdate`, where PostgreSQL re-checks the target row's own `WHERE` after a row lock); why *banned* (hidden while banned, waits again when lifted — FR-021) and *already a member* (FR-020's safety net)
- [X] T006 In `backend/Services/Teams/TeamJoinRequestService.cs` `ListPendingAsync`, replace the `Status == Pending` filter with `.Where(r => r.TeamId == a.TeamId).Where(JoinRequestWaiting.Predicate(_db))`; add `Queue_lists_only_waiting_requests` to `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestTests.cs`: a request from a player then banned (set `Users.Status = Banned` via `HomeTestSupport.WithDbAsync`) and one from a player who then became a member (insert a `TeamMembership`) are absent from `GET …/join-requests`; setting the banned player back to `Active` returns their request (depends on T005)
- [X] T007 Widen the push seam (research R2): in `backend/Services/Notifications/IPushFanOut.cs` add `Guid? actorUserId = null` **before** `CancellationToken ct` (documented: resolved to a display name once per fan-out; never stored); in `backend/Services/Notifications/Push/PushFanOut.cs`, after the recipient early-return, resolve `actorName` once via `_db.PlayerProfiles.AsNoTracking().Where(p => p.UserId == actorUserId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct)` (so a banned actor resolves to null) and pass it to `Compose`; in `backend/Services/Notifications/NotificationService.cs` pass `actorUserId: actorUserId` at **both** `_push.FanOutAsync` call sites (named arguments)
- [X] T008 Composer arms (research R13, contracts/notifications.md): in `backend/Services/Notifications/Push/PushContentComposer.cs` add `string? actorName = null` to `IPushContentComposer.Compose` and the implementation; `TitleFor`: both new types → `Text(payload, "teamName")`; `BodyFor`: `TeamJoinRequest` → `actorName` non-empty ? `Get("teamJoinRequest.body", culture, actorName)` : `Get("teamJoinRequest.bodyAnonymous", culture)`, `TeamJoinRequestAnswered` → by a new nullable `Flag(payload, "accepted")` reader: true → `teamJoinRequestAccepted.body`, false → `teamJoinRequestDeclined.body`, missing → `fallback.body`; `UrlFor`: `TeamJoinRequest` → `/t/{slug}`, answered → accepted ? `/t/{slug}` : `/browse/teams` (missing flag → `/`). In `backend/Services/Notifications/Push/PushLocalizer.cs` add the four keys × en/de/es with a `{0} = player's display name` comment (EN: "{0} wants to join the team", "Someone wants to join the team", "Your request to join was accepted", "Your request to join was declined") (depends on T002, T007)
- [X] T009 [P] In `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushComposerTests.cs` add: `A_join_request_names_the_team_and_the_player` (en/de/es theory, `actorName: "Jonas"`, URL `/t/hamburg-hammers`), `A_join_request_without_a_name_still_says_what_happened` (null `actorName` → the anonymous body, never the generic fallback), `An_answer_says_accepted_or_declined_and_where_to_go` (accepted → `/t/{slug}`; declined → `/browse/teams`), and the guard `Every_notification_type_has_its_own_title_body_and_url`: a dictionary of one representative payload per `NotificationType` (plus `actorName: "Anna"`), iterating `Enum.GetValues<NotificationType>()`, failing with "add a sample and its composer arms" for a type without a sample, and asserting title ≠ the fallback title, body ≠ the fallback body, URL ≠ `/` (depends on T008)
- [X] T010 [P] Email generators (research R12): in `backend/Services/EmailTemplateService/IEmailTemplateService.cs` and `EmailTemplateService.cs` add `GenerateJoinRequestEmailAsync(recipientName, playerName, teamName, teamUrl, culture)`, `GenerateJoinRequestAcceptedEmailAsync(recipientName, teamName, teamUrl, culture)`, `GenerateJoinRequestDeclinedEmailAsync(recipientName, teamName, browseUrl, culture)` — the `GeneratePartyRequestEmailAsync` shape, URLs as `RawHtml`, `EMAIL_TITLE` from `title.joinRequest` / `title.joinRequestAccepted` / `title.joinRequestDeclined`, `FOOTER_REASON` from `footer.joinRequest` (admin) / `footer.joinRequestAnswer` (player)
- [X] T011 [P] Create the nine templates `backend/EmailTemplates/{en,de,es}/join-request.html`, `join-request-accepted.html`, `join-request-declined.html`, cloned from `party-request.html`'s structure (eyebrow, `h1`, greeting, one paragraph, button, alt-link); placeholders exactly `{{RECIPIENT_NAME}}`, `{{TEAM_NAME}}` plus `{{PLAYER_NAME}}`/`{{TEAM_URL}}` (request), `{{TEAM_URL}}` (accepted), `{{BROWSE_URL}}` (declined) — the **same set in every language**; de/es carry the existing `i18n: … draft — native-speaker review pending (#77)` comment; German body text uses `–`, never `—`; the declined email is kind and points to other teams; no email names the admin who answered
- [X] T012 [P] In `backend/Services/Email/EmailLocalizer.cs` add, × en/de/es, with positional-argument comments: `subject.joinRequest` ({0} player, {1} team), `subject.joinRequestAccepted` ({0} team), `subject.joinRequestDeclined` ({0} team) — ending ` — JuggerHub` like every existing subject — plus `title.joinRequest`, `title.joinRequestAccepted`, `title.joinRequestDeclined`, `footer.joinRequest` ("…because you're an admin of this team on JuggerHub."), `footer.joinRequestAnswer` ("…because you asked to join this team on JuggerHub.")
- [X] T013 In `backend/Services/Email/TeamEmailService.cs` inject `IEmailLocalizer` and add `SendJoinRequestEmailAsync(toEmail, recipientName, playerName, teamName, slug, culture, ct)`, `SendJoinRequestAcceptedEmailAsync(toEmail, recipientName, teamName, slug, culture, ct)`, `SendJoinRequestDeclinedEmailAsync(toEmail, recipientName, teamName, culture, ct)` and `internal static string BuildBrowseTeamsLink(string frontendBaseUrl)` → `{base}/browse/teams`; the three existing English-only methods are untouched (depends on T010, T012)
- [X] T014 [P] In `backend/tests/JuggerHub.Api.IntegrationTests/Email/TemplateParityTests.cs` add the three template names to `FullyTranslatedTemplates`; in `TemplateRenderMatrixTests.cs` add `Join_request_renders`, `Join_request_accepted_renders`, `Join_request_declined_renders` (× `Cultures`), asserting well-formedness, the names and the URL (depends on T010, T011, T012)
- [X] T015 [P] In `frontend/apps/web/src/app/core/models/notification.models.ts` add `'TeamJoinRequest' | 'TeamJoinRequestAnswered'` to `NotificationType`, `TeamJoinRequestPayload { requestId; teamSlug; teamName }`, `TeamJoinRequestAnsweredPayload { teamSlug; teamName; accepted: boolean }` (documented: no name in the first — it is `actorDisplayName`; no admin in the second), both in `NotificationPayload`, and the narrowing helpers `isTeamJoinRequest` / `isTeamJoinRequestAnswered`
- [X] T016 [P] Add **all** new copy to `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json` in one change (`catalog-parity` fails otherwise). `alerts.row`: `joinRequestTitle` ("{{player}} wants to join {{team}}"), `joinRequestSupporting` ("Open the team page to answer"), `joinRequestHandled` ("No longer waiting for an answer"), `formerPlayer` (exactly the server's `MemberPlaceholder`: "A former player" / "Ein ehemaliger Spieler" / "Un jugador anterior"), `joinAcceptedTitle` ("You're in: {{team}} accepted your request"), `joinAcceptedSupporting` ("Say hello to your new team"), `joinDeclinedTitle` ("{{team}} declined your request"), `joinDeclinedSupporting` ("Have a look at other teams"). `home.needsYouItem`: `teamInviteTitle` ("{{team}} invited you"), `teamInviteContext` ("to join the team"), `partyCoAdminInviteTitle` ("Co-admin a party for {{event}}"), `partyRequestTitle` ("{{team}} is fielding a party"), `marketInviteTitle` ("{{team}} want you"), `marketApplicationTitle` ("You applied to {{team}}") — English **verbatim** from today's server strings (research R11) — `joinRequestTitle` ("{{player}} wants to join"), `noLongerWaiting` ("This request was already answered or withdrawn."). `teams.detail`: change `confirmJoinBody` to also say they'll be told the answer (FR-014); add `requestLimited` ("You've sent a lot of join requests in a short time. Try again in a little while."), `alreadyMember`, `requestFailed` ("We couldn't send your request just now."), `cancelFailed`, `answerGone` (= `noLongerWaiting`'s meaning), `answerFailed`. `onboarding.team`: change `askedConfirmation` to add "…, and you'll be told when they answer." (FR-014); add `requestLimited`, `alreadyMember` ("You're already on that team."), `requestFailed` ("We couldn't send that request just now."). Rules: German `–` never `—`, Spanish raya only paired, errors open "We couldn't" / "Wir konnten" / "No pudimos", no emoji, sentence case. Then run `npx nx test web --watch=false --testPathPatterns="catalog-"` in `frontend/`
- [X] T017 Checkpoint: `dotnet build backend/JuggerHub.slnx`; `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~NotificationCategoryMapping|FullyQualifiedName~PushComposer|FullyQualifiedName~Template|FullyQualifiedName~JoinRequestTests|FullyQualifiedName~NotificationChannelIndependence|FullyQualifiedName~NotificationTests"`; commit `feat(058): join-request notification types, waiting predicate, push arms and localized emails (#360)`

**Checkpoint**: foundation ready. US1 and US4 can start; US2 needs nothing from US1; US3 needs US1's alerts.

---

## Phase 3: User Story 1 — The admins hear about a request (Priority: P1) 🎯 MVP

**Goal**: every current admin is told of a new request — Alerts row, device and email, each by
their own *Invites & roster* setting — naming the player (at read time) and the team, once.

**Independent Test**: J requests to join a team with admins A and B and member M → A and B each have
one row naming J, one email in their own language and one push; M and J have nothing; asking again
changes nothing; the stored row holds no name.

### Tests for User Story 1

- [ ] T018 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestNotificationTests.cs` (`[Collection("Teams")]`, users via `AuthTestHelpers`, extra admins/members via `HomeTestSupport.AddMemberAsync`, DB reads via `HomeTestSupport.WithDbAsync`) with the US1 facts: `Every_current_admin_gets_one_alert_and_nobody_else`; `The_stored_alert_carries_no_name_or_handle` (payload is exactly `requestId`/`teamSlug`/`teamName`; neither the player's display name nor handle appears in the JSON; `ActorUserId` = the player); `The_alert_shows_the_players_current_name` (rename the profile → the admin's list shows the new `actorDisplayName`); `Asking_again_while_waiting_announces_nothing` (second `POST` → still one row per admin, no second email, no second push); `Each_admin_is_emailed_in_their_own_language` (A `de`, B `en` via `PUT /api/v1/account/language`; `_factory.EmailSender` subjects in each language; none to M or J); `The_device_notification_names_the_team_and_the_player` (`_factory.PushDispatcher`: title = team name, body contains J's name, URL `/t/{slug}`, tag `join-request:{requestId}`); `A_banned_or_erased_player_is_named_by_no_one` (after a ban, and separately after `DELETE`-account erasure: `actorDisplayName` null and `resolved` true); `An_answered_request_reads_as_no_longer_waiting` (approve through the existing endpoint → A's and B's rows `resolved: true`); `A_failed_push_never_fails_the_request` (`PushDispatcher.ThrowOnDispatch` → `204`, rows exist)
- [ ] T019 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Push/NotificationChannelIndependenceTests.cs` add the four-way matrix for a join request (the admin's *Invites & roster* In-app × Push on/off → row written iff in-app on, dispatched iff push on, independently)

### Implementation for User Story 1

- [ ] T020 [US1] In `backend/Services/Teams/TeamJoinRequestService.cs` inject `INotificationService`, `INotificationPreferenceService`, `TeamEmailService` and `ILogger<TeamJoinRequestService>`; after `RequestAsync` reaches `Created` (never on `AlreadyPending`/unique violation), call a private `AnnounceAsync(requestId, teamId, playerId, ct)`: one projection of the team's **admins** (`UserId`, `Email`, display name via `_db.PlayerProfiles.Where(p => p.UserId == m.UserId)`, `PreferredLanguage`), the team's slug and name, the player's current display name (email only); `CreateManyAsync(adminIds, TeamJoinRequest, new TeamJoinRequestPayload(requestId, slug, name), actorUserId: playerId, dedupeKeyPrefix: $"join-request:{requestId}")` and the email loop (`GetEnabledRecipientsAsync(…, InvitesAndRoster, Email)` → `SendJoinRequestEmailAsync` per admin in `SupportedLanguages.ResolveOrDefault(language)`), **each in its own `try/catch`** logging ids only, never names (the `TeamNewsService.PostAsync` shape). Spell the prefix once: `private static string AlertPrefix(Guid requestId)` (depends on T002, T004, T013)
- [ ] T021 [US1] In `backend/Services/Notifications/NotificationService.cs` `ListAsync`: generalise `TryGetInvitationId` into `TryGetGuid(payloadJson, property)`; collect the page's `TeamJoinRequest` `requestId`s; one query `TeamJoinRequests.Where(ids.Contains(r.Id)).Where(JoinRequestWaiting.Predicate(_db)).Select(r => r.Id)`; set `Resolved` for those rows to `!waiting` (TeamInvite logic unchanged). New realtime rows keep `Resolved: false` (a fresh request always waits) (depends on T005)
- [ ] T022 [P] [US1] In `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.ts` and `.html`: `TeamJoinRequest` → link `/t/{teamSlug}`, title `alerts.row.joinRequestTitle` with `player = actorDisplayName ?? translate('alerts.row.formerPlayer')`, supporting `joinRequestHandled` when `resolved` else `joinRequestSupporting`, icon `user-plus` in the invite (brand) scheme; not `actionable`. Extend `notification-row.component.spec.ts`: title with the name, the former-player fallback, the resolved line, the link, no buttons (depends on T015, T016)
- [ ] T023 [US1] Checkpoint: `dotnet test … --filter "FullyQualifiedName~JoinRequest|FullyQualifiedName~NotificationChannelIndependence|FullyQualifiedName~NotificationTests"` and `npx nx test web --watch=false --testPathPatterns="notification-row"`; commit `feat(058): announce join requests to every current admin (#360)`

**Checkpoint**: US1 works on its own — admins hear about requests.

---

## Phase 4: User Story 2 — The player hears the answer (Priority: P1)

**Goal**: an answer happens exactly once, and the player is told it — accepted → the team,
declined → the team browser — without learning which admin answered.

**Independent Test**: approve → the player has one accepted notice (no actor) opening the team, plus
email/push by their settings; decline → one declined notice opening `/browse/teams`; two
simultaneous answers → one `204`, one `404`, one notice.

### Tests for User Story 2

- [ ] T024 [P] [US2] Add the US2 facts to `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestNotificationTests.cs`: `Approving_tells_the_player_once_and_names_no_admin` (one `TeamJoinRequestAnswered` row, `accepted: true`, `actorDisplayName` null, no admin name or handle anywhere in the JSON, the player is a member); `Declining_tells_the_player_once` (`accepted: false`, not a member); `Two_simultaneous_answers_take_effect_once` (A approves and B declines via `Task.WhenAll` → exactly one `204` and one `404`; the player has exactly one answer row, matching the membership state); `Answering_a_withdrawn_request_is_refused_and_tells_no_one`; `Answering_a_banned_players_request_is_refused`; `The_claim_sets_ModifiedDate_and_the_decision` (DB: `Status`, `DecidedByUserId`, `DecidedDate` set, `ModifiedDate` moved); `The_player_is_emailed_the_answer_in_their_language` (player `es`, accepted and declined subjects); `The_device_notification_says_accepted_or_declined` (title team name; URL `/t/{slug}` vs `/browse/teams`; tag `join-answer:{requestId}`); existing `JoinRequestTests` still pass unchanged

### Implementation for User Story 2

- [ ] T025 [US2] In `backend/Services/Teams/TeamJoinRequestService.cs` rewrite `ApproveAsync` (research R4): guard → admin (unchanged outcomes) → read the request's `UserId` scoped to the team (`AsNoTracking`; absent ⇒ `RequestNotFound`) → `CreateExecutionStrategy().ExecuteAsync`: `_db.ChangeTracker.Clear()`; begin; claim = `_db.TeamJoinRequests.Where(r => r.Id == requestId && r.TeamId == teamId).Where(JoinRequestWaiting.Predicate(_db)).ExecuteUpdateAsync(Status = Approved, DecidedByUserId, DecidedDate = now, **ModifiedDate = now**)`; 0 ⇒ return not-waiting; add the `Member` `TeamMembership` (`JoinedDate = now`); `SaveChangesAsync`; commit. Catch a unique violation **outside** the strategy ⇒ `RequestNotFound` (the player joined another way). After commit only: `NotifyAnswerAsync(requestId, playerId, teamId, accepted: true)`. Rewrite `DeclineAsync` as one claim (`Declined`, same four properties) ⇒ 0 ⇒ `RequestNotFound`, else `NotifyAnswerAsync(…, accepted: false)` (depends on T005, T020)
- [ ] T026 [US2] In the same file add `NotifyAnswerAsync`: team slug/name + the player's email, display name and `PreferredLanguage` in one projection; `CreateAsync(playerId, TeamJoinRequestAnswered, new TeamJoinRequestAnsweredPayload(slug, name, accepted), actorUserId: null, dedupeKey: $"join-answer:{requestId}")`; the email by the player's *Invites & roster → Email* setting (`SendJoinRequestAcceptedEmailAsync` / `SendJoinRequestDeclinedEmailAsync`); each in its own `try/catch`, ids only in logs (depends on T013)
- [ ] T027 [P] [US2] In `notification-row.component.ts`/`.html`: `TeamJoinRequestAnswered` → link `/t/{slug}` when `accepted` else `/browse/teams`; title `joinAcceptedTitle` / `joinDeclinedTitle`; supporting `joinAcceptedSupporting` / `joinDeclinedSupporting`; icon `users` in the info scheme; extend the spec for both outcomes (depends on T015, T016)
- [ ] T028 [P] [US2] In `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts`/`.html`: `approve`/`decline` errors branch on **status** — `404` ⇒ reload the queue and set a new `joinNotice` signal to `teams.detail.answerGone`, rendered as a neutral `role="status"` line at the queue's position **independent of the queue's visibility**; other ⇒ page `error` = translated `teams.detail.answerFailed` (never `problemDetail`); clear `joinNotice` on the next join action and on `load()` of another slug. Extend `team-detail.component.spec.ts`: a `404` shows the notice and reloads; a `500` shows the translated failure (depends on T016)
- [ ] T029 [US2] Checkpoint: `dotnet test … --filter "FullyQualifiedName~JoinRequest"`, `npx nx test web --watch=false --testPathPatterns="notification-row|team-detail"`; commit `feat(058): answer join requests exactly once and tell the player (#360)`

**Checkpoint**: US1 + US2 close the loop.

---

## Phase 5: User Story 3 — Withdrawn and repeated requests do not pile up (Priority: P1, ships with US1)

**Goal**: a withdrawal takes its alerts with it (all or nothing, badges refreshed), joining by
invitation ends a waiting request the same way, and one player can send at most ten requests per
hour.

**Independent Test**: request → withdraw → no admin holds the alert and an unread admin's badge
dropped; ten requests then an eleventh in the same hour → `429`, nothing stored, nobody notified;
accept an invitation while a request waits → the request and its alerts are gone.

### Tests for User Story 3

- [ ] T030 [P] [US3] Add the US3 facts to `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestNotificationTests.cs`: `Withdrawing_removes_every_admins_alert` (including an admin demoted between the request and the withdrawal); `Withdrawing_lowers_an_unread_admins_badge` (`_factory.NotificationRealtime.UnreadCountsFor(admin)` ends at the lowered count); `A_withdrawal_after_an_answer_leaves_the_answer_alone` (approve, then `DELETE …/mine` → `204`, the row is still `Approved` in the database); `Joining_by_invitation_ends_the_waiting_request` (the player requests, an admin sends a targeted invitation, the player accepts via `/invitations/{token}/accept` → request row gone, admins' alerts gone, no answer notice, queue empty)
- [ ] T031 [P] [US3] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/JoinRequestRateLimitTests.cs`: `The_eleventh_request_in_an_hour_is_refused` (ten `POST`/`DELETE …/mine` rounds → the eleventh `POST` → `429`; no waiting request exists afterwards; no admin holds an alert; `DELETE …/mine` still answers `204`); `The_limit_is_per_player` (a second player's request in the same test still succeeds). The test host has no Redis, so this exercises the in-memory fixed window (research R7)

### Implementation for User Story 3

- [ ] T032 [US3] In `backend/Services/Teams/TeamJoinRequestService.cs` extract a private `EndWithoutAnswerAsync(Guid requestId, CancellationToken ct)` (research R5): inside the execution strategy — begin → `_db.TeamJoinRequests.Where(r => r.Id == requestId && r.Status == JoinRequestStatus.Pending).ExecuteDeleteAsync()` → 0 ⇒ return (false, []) → `_notifications.DeleteManyAsync(NotificationType.TeamJoinRequest, AlertPrefix(requestId))` → commit; after commit `RefreshUnreadBadgesAsync(recipients)`. Rewrite `CancelAsync`: pre-read the caller's **Pending** request id (`AsNoTracking`) → none ⇒ `NothingToCancel` → `EndWithoutAnswerAsync` → matched ? `Cancelled` : `NothingToCancel` (depends on T020)
- [ ] T033 [US3] Add `Task EndForMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)` to `backend/Services/Teams/ITeamJoinRequestService.cs` (doc: FR-020, exactly as a withdrawal, nobody notified, never throws for a notification problem) and implement it with the same pre-read + `EndWithoutAnswerAsync`; in `backend/Services/Teams/TeamInvitationService.cs` inject `ITeamJoinRequestService` and, after `AcceptAsync` reaches `Joined` **or** `AlreadyMember`, call it in a `try/catch` that logs ids only (research R6) — nothing else in `AcceptAsync` changes (depends on T032)
- [ ] T034 [US3] Rate limit (research R7): in `backend/Security/RateLimitPolicies.cs` add `JoinRequest = "join-request"` and `JoinRequestsPerHour = 10` with doc comments (open reach; fixed window ⇒ ≤ 20 across the turn of an hour; **our own** fail-closed `429`, never retried — the Principle VII distinction, written where it applies); give `PartitionByUser` and `Limiter` a `TimeSpan window` parameter defaulting to one minute (the four existing policies unchanged) and pass it to both the in-memory `FixedWindowRateLimiterOptions.Window` and `RedisFixedWindowRateLimiter`; register `options.AddPolicy(JoinRequest, PartitionByUser(JoinRequest, JoinRequestsPerHour, TimeSpan.FromHours(1)))`. In `backend/Controllers/TeamsController.cs` add `[EnableRateLimiting(RateLimitPolicies.JoinRequest)]` to `RequestToJoin` **only** and note it in its doc comment
- [ ] T035 [P] [US3] In `team-detail.component.ts`: `requestToJoin` errors branch on status — `429` ⇒ `teams.detail.requestLimited`, `409` ⇒ `teams.detail.alreadyMember`, else ⇒ `teams.detail.requestFailed`; `cancelRequest` errors ⇒ `teams.detail.cancelFailed` — all translated, never `problemDetail`. Extend `team-detail.component.spec.ts` for the three request statuses (depends on T016)
- [ ] T036 [P] [US3] In `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts`/`.html`: `teamRequestError` holds a catalogue **key** (`onboarding.team.requestLimited` for `429`, `alreadyMember` for `409`, `requestFailed` otherwise), rendered with `| transloco` inside the existing `jh-alert` — replacing the two hard-coded English strings (research R14). Extend `onboarding.component.spec.ts`: `429` shows the limit message, `409` the already-a-member message (depends on T016)
- [ ] T037 [US3] Checkpoint: `dotnet test … --filter "FullyQualifiedName~JoinRequest|FullyQualifiedName~MyInvitations|FullyQualifiedName~Onboarding"`, `npx nx test web --watch=false --testPathPatterns="team-detail|onboarding"`; commit `feat(058): withdrawals take their alerts, joining ends a request, ten requests an hour (#360)`

**Checkpoint**: P1 is complete — the flood risk US1 opened is bounded.

---

## Phase 6: User Story 4 — Waiting requests are on Home (Priority: P2)

**Goal**: *Needs you* lists each waiting request for its team's current admins with *Approve* /
*Decline* in place, and every *Needs you* kind is worded in the viewer's language.

**Independent Test**: an admin of two teams with one waiting request each sees two items naming the
player and team; approving from Home equals approving on the team page; members and the player see
none; the card in German contains no English sentence.

### Tests for User Story 4

- [ ] T038 [P] [US4] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Home/NeedsYouTests.cs`: `An_admin_sees_each_waiting_request_once` (`kind` `JoinRequest`, `id` = request id, `linkTarget` = the player's handle, `params.playerName`/`teamName`/`teamSlug`); `Across_every_team_they_administer`; `Members_and_the_player_see_no_join_request`; `An_answered_or_banned_request_leaves_needs_you`; `Every_kind_carries_names_not_sentences` (the TeamInvite item has `params.teamName` and **no** `title`/`context` property; party and market items carry `params.teamName`/`eventName`); existing facts keep passing

### Implementation for User Story 4

- [ ] T039 [US4] In `backend/Dtos/Home/HomeDtos.cs`: append `NeedsYouKind.JoinRequest` (doc: a waiting request to a team the viewer administers; answered via `/teams/{Params.TeamSlug}/join-requests/{Id}/approve|decline`); add `NeedsYouParamsDto` (init-only nullable `TeamName`, `TeamSlug`, `EventName`, `PlayerName`, documented like `ActivityParamsDto` — names only, the client composes the words, GH #141); reshape `NeedsYouItemDto(NeedsYouKind Kind, string Id, NeedsYouParamsDto Params, string? LinkTarget, DateTime OccurredAt)` and rewrite its summary (per-kind `Id`/`LinkTarget` table from [contracts/home-needs-you.md](./contracts/home-needs-you.md))
- [ ] T040 [US4] In `backend/Services/Home/HomeService.cs` `LoadNeedsYouAsync`: move the five existing projections to `Params` (no string concatenation left); add the join-request query — the viewer's admin team ids (`TeamMemberships` where `Role == Admin`), `TeamJoinRequests.Where(adminTeamIds.Contains(r.TeamId)).Where(JoinRequestWaiting.Predicate(_db))`, newest first, `Take(cap)`, projecting `PlayerName`/handle through `_db.PlayerProfiles.Where(p => p.UserId == r.UserId)` (never `r.User.Profile!`); concat and cap as today; update the method and class doc comments (depends on T005, T039)
- [ ] T041 [P] [US4] In `frontend/apps/web/src/app/core/models/home.models.ts`: `NeedsYouKind` + `'JoinRequest'`; `NeedsYouParams { teamName; teamSlug; eventName; playerName }` (all `string | null`); `NeedsYouItem` = `{ kind; id; params; linkTarget; occurredAt }` (no `title`/`context`)
- [ ] T042 [US4] In `frontend/apps/web/src/app/features/dashboard/modules/needs-you-card.component.ts`/`.html`: a per-kind `{ key, params }` helper for the title (+ `teamInviteContext` for TeamInvite; the event or team name as the context for the others; the team name for JoinRequest) rendered with the `transloco` pipe; `link()` → `['/u', linkTarget]` for JoinRequest; JoinRequest buttons `teams.detail.approve` / `common.decline` calling `approveJoinRequest(params.teamSlug, id)` / `declineJoinRequest(…)`; a `404` on a JoinRequest adds its id to a `stale` signal (item hidden) and sets a card-level `notice` (`home.needsYouItem.noLongerWaiting`, `role="status"`), keeping the card visible while the notice shows; other kinds' behaviour unchanged. All new state as signals (zoneless) (depends on T016, T041)
- [ ] T043 [US4] Update `needs-you-card.component.spec.ts` (fixture helper to the new shape; each kind's composed English title; JoinRequest approve/decline POST to `/api/v1/teams/{slug}/join-requests/{id}/approve|decline` and emit `resolved`; a `404` hides the item and shows the notice without emitting) and any `NeedsYouItem` fixture in `frontend/apps/web/src/app/features/dashboard/dashboard.component.spec.ts` (depends on T042)
- [ ] T044 [US4] Checkpoint: `dotnet test … --filter "FullyQualifiedName~NeedsYou|FullyQualifiedName~HomeTests|FullyQualifiedName~NoTeamVariant"`, `npx nx test web --watch=false --testPathPatterns="needs-you-card|dashboard"`; commit `feat(058): waiting join requests on Home; Needs you in the viewer's language (#360)`

**Checkpoint**: all four stories work.

---

## Phase 7: Polish & cross-cutting

- [ ] T045 [P] Pointers, not rewrites: in `specs/009-team-public-page/spec.md` add after the "Notifications for request/approval are **placeholder**" assumption a one-line "Fulfilled by feature 058 (`specs/058-join-request-notifications/`)"; in `specs/025-home-participation-makeover/contracts/home-api.md` add a note at the *Needs you* item that feature 058 replaced `title`/`context` with `params` (link [contracts/home-needs-you.md](./contracts/home-needs-you.md))
- [ ] T046 [P] File the three follow-up issues with `gh issue create --body-file` (research R17): (1) role-change payloads store `newRole` as a number — promotion alerts read "member", Home's activity loses the role (evidence: the `Payload` query in research R8); (2) `TeamInvitePayload.InviterName` survives the inviter's erasure (037 FR-023); (3) marketplace applications notify no party admin (the #360 gap in the other direction). Record the numbers under **Notes**
- [ ] T047 Rebuild the stack (`docker compose up -d --build backend frontend`) and run the [quickstart](./quickstart.md) API spot checks, including reading a stored `TeamJoinRequest` payload back from the database (no name)
- [ ] T048 Gate 7: copy `.specify/templates/ui-review-checklist-template.md` to `specs/058-join-request-notifications/checklists/ui-review.md` and answer each item against the diff **and the screenshots from T049** (DESIGN.md wins on any conflict; record pre-existing failures without fixing them)
- [ ] T049 Browser walk (owner's standing rule): a throwaway Playwright script inside `frontend/` — one browser context per actor, `locale: 'de-DE'`, locale-agnostic waits — covering quickstart scenarios 1, 3, 4, 5, 7, 8, 10 and 11 at **375px** and desktop; screenshots of the *Needs you* card holding every kind, both Alerts rows (waiting and no-longer-waiting; accepted and declined), the team page notice and limit message, and onboarding's limit message. Read the output and look at every screenshot; delete the script afterwards
- [ ] T050 Full verification: `dotnet test backend/JuggerHub.slnx`; in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Record results (and any pre-existing failure from T001) under **Notes**
- [ ] T051 Mark every task in `specs/058-join-request-notifications/tasks.md` `[X]`, fill in **Notes**, and commit `docs(058): verification record, UI review, follow-ups (#360)`

---

## Dependencies & execution order

- **Setup (T001)** → **Foundational (T002–T017)** → stories.
- **US1 (T018–T023)** needs the foundation only.
- **US2 (T024–T029)** needs the foundation; T025 builds on T020's injected services (same file — do US1 first).
- **US3 (T030–T037)** needs US1 (it removes US1's alerts) and US2's file edits (same service file).
- **US4 (T038–T044)** needs the foundation only (T005's predicate); it can run beside US1–US3 on the Home/dashboard files.
- **Polish (T045–T051)** after all stories; T048 needs T049's screenshots.

Within the foundation: T002 → T003; T005 → T006; T007 → T008 → T009; T010 + T011 + T012 → T013 → T014; T015 and T016 are independent.

## Parallel opportunities

- Foundation: T002, T004, T005, T010, T011, T012, T015 and T016 touch different files — run together; then T003, T006, T007, T009, T014.
- US1: T018 and T019 (tests) together, then T020 → T021; T022 in parallel with the backend.
- US2: T027 and T028 (frontend) in parallel with T025 → T026.
- US3: T030, T031, T035 and T036 in parallel; T032 → T033 → T034 in sequence (same files).
- US4: T038 and T041 in parallel; T039 → T040; T042 → T043.

```text
# Example — the foundation's first wave, all different files:
T002 NotificationEnums.cs   T004 NotificationDtos.cs   T005 JoinRequestWaiting.cs
T010 EmailTemplateService   T011 nine templates        T012 EmailLocalizer.cs
T015 notification.models.ts T016 en/de/es.json
```

## Implementation strategy

1. **MVP = Foundation + US1**: admins hear about requests. **Do not ship US1 without US3** — US1
   opens the open-reach amplification that US3's limit bounds (spec: US3 "ships with US1").
2. Then US2 (the other half of the loop), US3, US4 — one commit per story checkpoint.
3. Polish: pointers, follow-up issues, the German 375px walk, Gate 7, full verification.

## Notes

- **T001 baseline (untouched branch, 2026-09-28)**: `dotnet build backend/JuggerHub.slnx` clean (0 warnings, 0 errors); the targeted backend suites (JoinRequest, NeedsYou, PushComposer, NotificationCategoryMapping, Template*) 85/85 green; the targeted frontend suites (notification-row, needs-you-card, team-detail, onboarding, dashboard, catalog-*) 13 suites / 124 tests green. No pre-existing failure.
- **T017 checkpoint**: 119/119 backend tests green on the foundation, including the new every-type composer guard; catalogue guards 20/20.
- **Found during implementation**: `GenerateDocumentationFile` + `TreatWarningsAsErrors` mean a lone `<param>` tag fails the build (CS1573 demands one for every parameter) and a `cref` to a member that does not exist yet fails it too (CS1574) — the actor explanation on `IPushFanOut` lives in `<remarks>`.
