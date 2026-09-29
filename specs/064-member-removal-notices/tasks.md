# Tasks: Removing a Member Is Confirmed, and the People It Concerns Are Told

**Input**: Design documents from `specs/064-member-removal-notices/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: included, as in 058–063. Each behaviour gets a test written before the code that makes it
pass. The two notification types and the shared dialog serve several stories, so they are
foundational.

**Organization**: by user story. US2 and US3 share one backend method (`MutateMembershipAsync`) and
one test file, so they run one after the other. US1 and US4 touch different pages and can run
alongside once the dialog exists.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Record the baseline on the branch: `dotnet build backend/JuggerHub.slnx` (gate on the exit code), `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Teams|FullyQualifiedName~Notification|FullyQualifiedName~Push|FullyQualifiedName~Email"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="team-detail|party-manage|notification-row|my-team|onboarding|needs-you|dashboard|catalog-"`. Note any pre-existing failure so it is not later mistaken for a regression.

---

## Phase 2: Foundational (blocking)

**Purpose**: the two notification types end to end (enum, category, payloads, push, email), and the shared confirmation dialog. Nothing sends them yet.

### Backend types

- [X] T002 Append `TeamMemberRemoved = 12` and `TeamMemberDeparted = 13` to `NotificationType` in `backend/Entities/NotificationEnums.cs`, each with an XML doc (Removed: to the player an admin removed; **no actor, ever** — the removing admin must be identifiable nowhere, spec FR-011. Departed: to every current admin but the one who acted; the departing player is the **actor**, never in the payload, 037 FR-023 as for `TeamJoinRequest`; `removed` says which kind of departure). Add both to `NotificationCategories.For` → `InvitesAndRoster`, and extend `NotificationCategory.InvitesAndRoster`'s doc to name them.
- [X] T003 [P] Add `public sealed record TeamMemberRemovedPayload(string TeamSlug, string TeamName);` and `public sealed record TeamMemberDepartedPayload(string TeamSlug, string TeamName, bool Removed);` beside `TeamJoinRequestAnsweredPayload` in `backend/Dtos/Notifications/NotificationDtos.cs`, docs noting: no person in either; `Removed` is a bool because payload enums are stored as numbers (#370).
- [X] T004 [P] Add both kinds to `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/NotificationCategoryMappingTests.cs` (the `InlineData` rows → `InvitesAndRoster`, and the exhaustive list).
- [X] T005 [P] Push: in `backend/Services/Notifications/Push/PushLocalizer.cs` add en/de/es for `teamMemberRemoved.body` ("You're no longer a member of this team" / "Du bist kein Mitglied dieses Teams mehr" / "Ya no eres miembro de este equipo"), `teamMemberLeft.body` ("{0} left the team" / "{0} hat das Team verlassen" / "{0} ha dejado el equipo"), `teamMemberLeft.bodyAnonymous` ("A player left the team" / "Jemand hat das Team verlassen" / "Alguien ha dejado el equipo"), `teamMemberRemovedByAdmin.body` ("{0} was removed from the team" / "{0} wurde aus dem Team entfernt" / "{0} ha sido retirado del equipo"), `teamMemberRemovedByAdmin.bodyAnonymous` ("A player was removed from the team" / "Jemand wurde aus dem Team entfernt" / "Alguien ha sido retirado del equipo"). In `backend/Services/Notifications/Push/PushContentComposer.cs`: title arms (team name) for both types; body arms (Removed → fixed; Departed → `Flag(payload,"removed")` true/false × actor name present/absent; null flag → fallback); URL arms `/t/{slug}` via `Slug(payload,"teamSlug")`.
- [X] T006 [P] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushComposerTests.cs`: the exhaustive-guard sample payloads for both types; URL cases (`/t/hh`, `../evil` → `/`); body cases for Departed left/removed with and without an actor name, in en and de; Removed's body never contains an actor name even when one is passed.

### Backend email

- [X] T007 [P] Create `backend/EmailTemplates/{en,de,es}/removed-from-team.html` (eyebrow team/roster, `<h1>` "You're no longer a member of {{TEAM_NAME}}.", greeting `{{RECIPIENT_NAME}}`, one neutral sentence, button to `{{TEAM_URL}}` "Open the team page", alt-link; **no reason, no rejoin or browse pitch, no admin**), `member-left.html` ("{{PLAYER_NAME}} left {{TEAM_NAME}}.", addressed to an admin, button to the roster) and `member-removed.html` ("{{PLAYER_NAME}} was removed from {{TEAM_NAME}}.", **no admin named**). Copy the structure of `join-request.html` / `join-request-accepted.html`; German and Spanish carry the `<!-- i18n: … draft — native-speaker review pending (#77) -->` header the other drafts carry. German uses `–`, not `—`, in prose.
- [X] T008 [P] Add to `backend/Services/Email/EmailLocalizer.cs` under a `// --- Feature 064 ---` divider: `subject.removedFromTeam` ({0}=team), `subject.memberLeft` / `subject.memberRemoved` ({0}=player, {1}=team), `title.removedFromTeam`, `title.memberLeft`, `title.memberRemoved`, `footer.removedFromTeam` ("…because you were a member of this team…"), and reuse `footer.joinRequest` (admin) for the admins' two — all en/de/es.
- [X] T009 Add `GenerateRemovedFromTeamEmailAsync(recipientName, teamName, teamUrl, culture)`, `GenerateMemberLeftEmailAsync(recipientName, playerName, teamName, teamUrl, culture)`, `GenerateMemberRemovedEmailAsync(…same…)` to `backend/Services/EmailTemplateService/IEmailTemplateService.cs` and `EmailTemplateService.cs`, mirroring `GenerateJoinRequestEmailAsync` (`TEAM_URL` as `RawHtml`).
- [X] T010 Add `SendRemovedFromTeamEmailAsync(toEmail, recipientName, teamName, slug, culture, ct)` and `SendMemberDepartedEmailAsync(toEmail, recipientName, playerName, teamName, slug, removed, culture, ct)` to `backend/Services/Email/TeamEmailService.cs` (team link via `BuildTeamLink`; subject from the localizer), with docs stating that neither names the removing admin.
- [X] T011 [P] Add the three render cases (en/de/es each) to `backend/tests/JuggerHub.Api.IntegrationTests/Email/TemplateRenderMatrixTests.cs`, asserting no leftover `{{`, the team URL present, and for `removed-from-team` that no admin/player placeholder appears. Run `TemplateParityTests` (opt-out guard: all three languages must exist).

### Frontend shared

- [ ] T012 [P] Add Lucide's `user-minus` glyph (verbatim: `<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="22" x2="16" y1="11" y2="11"/>`) in alphabetical position in `frontend/apps/web/src/app/shared/ui/icon/icons.ts`.
- [X] T013 Write `frontend/apps/web/src/app/shared/ui/confirm-dialog/confirm-dialog.component.spec.ts` first: renders heading/body/answers; `role="dialog"`, `aria-modal`, `aria-labelledby`/`describedby` resolve; the keep answer is `document.activeElement` after first render; Escape emits `dismissed`; keep emits `dismissed`; confirm emits `confirmed`; while `busy` both answers are disabled, the confirm answer shows `busyLabel`, and Escape emits nothing; Tab from the last answer wraps to the first and Shift+Tab from the first to the last; `error` renders one `role="alert"` line.
- [X] T014 Create `frontend/apps/web/src/app/shared/ui/confirm-dialog/confirm-dialog.component.{ts,html,css}` per [contracts/ui.md](./contracts/ui.md): signal inputs `heading`, `body`, `keepLabel`, `confirmLabel`, `busyLabel`, `busy`, `error`; outputs `confirmed`, `dismissed`; markup copied from the news-post delete dialog (fixed scrim, `items-end` bottom sheet → `sm:items-center`, `max-w-container-sm`, header/body/footer borders, `flex flex-wrap` answers, keep `secondary`, confirm `danger`, both at the **default** size — 44px touch targets, never `size="sm"`'s 36px, the 063 lesson); focus the keep answer in `afterNextRender` (zoneless — never an effect, GH #344); `trapTab`; `@HostListener('document:keydown.escape')`. `data-testid`s from the contract. Export it from `frontend/apps/web/src/app/shared/ui/index.ts`. Run T013 green.

**Checkpoint**: `dotnet build` (exit code) + the four backend test classes above green; `npx nx test web --testPathPatterns=confirm-dialog` green. Commit `feat(064): the two departure notice kinds and a shared confirmation dialog (#385)`.

---

## Phase 3: User Story 1 — An admin confirms before removing a teammate (P1) 🎯 MVP

**Goal**: Remove on the team roster asks first (FR-001–FR-005).

**Independent Test**: as an admin, Remove → dialog; Escape and Keep change nothing; confirm removes.

- [ ] T015 [US1] Add keys ×3 (`en.json`, `de.json`, `es.json` in `frontend/apps/web/public/i18n/`, one change): `teams.detail.removeTitle` ("Remove {{name}} from {{team}}?"), `removeBody` ("They lose access to the team's members-only pages and chat. We'll let them know."), `removeKeep` ("Keep {{name}}"), `removeConfirm` ("Remove from team"), `removing` ("Removing…"), `removeFailed`, `removeGone` ("{{name}} is no longer on the team."), `removeForbidden` ("You're no longer an admin of this team."). German *entfernen*, *Mitglieder*, `–`.
- [ ] T016 [US1] Extend `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.spec.ts` (existing 057/058/060 tests must pass **unedited**): Remove in the menu opens `confirm-dialog` and does **not** call `removeMember`; keep/Escape close it and call nothing; confirm calls `removeMember(slug, userId)` once, the dialog closes and the page reloads; while pending a second confirm is ignored; 404 → dialog closed, page-level note `removeGone`, reload; 403 → `removeForbidden`, reload; 500 → dialog stays open with `removeFailed`, confirming again retries; the server's `detail` is never rendered.
- [ ] T017 [US1] In `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts`: signals `removing` (the `TeamMember | null` being confirmed), `removeBusy`, `removeError`, `removeNotice` (page-level key + params); `askRemove(m)` closes the menu and opens the dialog; `dismissRemove()` refocuses that member's ⋯ button (`[data-member-menu="<userId>"]`, `afterNextRender`); `confirmRemove()` calls `removeMember` and branches on status (research R11), focusing the roster heading after success. The Escape `HostListener` must not also close `confirmIntent` for this dialog (the component handles its own Escape; keep them independent).
- [ ] T018 [US1] In `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html`: the menu's Remove calls `askRemove(m)`; the ⋯ button gets `data-member-menu`; the roster `h2` gets an `id` + `tabindex="-1"`; render `<jh-confirm-dialog>` at page level beside the join confirmation (outside every `jh-card`) inside `@if (removing(); as m)`; render `removeNotice` as a `role="status"` line at the top of the roster card's column (page level, survives `load()`). Run T016 green.

**Checkpoint**: commit `feat(064): removing a teammate asks first (#385)`.

---

## Phase 4: User Story 2 — The removed player is told (P1)

**Goal**: FR-010–FR-013, FR-018–FR-021 for the removed player.

**Independent Test**: remove a player; they have one alert, one email, one push naming the team and no admin.

- [ ] T019 [US2] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamDepartureNoticeTests.cs` (`[Collection("Teams")]`, class doc naming GH #385; helpers copied from `JoinRequestNotificationTests`: `NewUserAsync`, a team of admins A + B and members, `AlertsAsync(user, type)`, `EmailsTo`, push recipients from `_factory.PushDispatcher`, `WithDbAsync`, `SetPreferenceAsync` via `PUT /api/v1/notification-preferences/InvitesAndRoster/{channel}`). US2 facts:
  - A removes M → M has exactly one `TeamMemberRemoved` alert; payload keys exactly `teamName, teamSlug`; `ActorUserId == null`; `DedupeKey == "team-removed:{membershipId}"` (read the membership id before removing);
  - M gets exactly one email and one push; neither contains A's display name or handle; push URL `/t/{slug}`;
  - M's language German → the email subject/body are German;
  - each channel off in turn (in-app / email / push) → only that channel is missing (independence);
  - M leaves on their own → M gets no `TeamMemberRemoved` (FR-013);
  - A removes M twice (second is 404) → still exactly one alert/email/push;
  - M rejoins (link invite) and A removes M again → a second alert with a different dedupe key (FR-018);
  - response codes unchanged: 204, 404 twice, 403 for a member removing another, 409 for the last admin (FR-021);
  - the team is deleted / M's account is banned → no `TeamMemberRemoved` for anyone (FR-019);
  - the team is renamed after the alert → the alert's `teamName` is the new name (FR-023; or cover in T025).
  Build and run: the notice facts fail.
- [ ] T020 [US2] In `backend/Services/Teams/TeamService.cs` `MutateMembershipAsync`: the delegate returns a private record `MembershipChange(MemberOpResult? Failure, TeamRole? PreviousRole, Guid MembershipId)` (capture `target.Id` before `Remove`); on `remove` call `await AnnounceDepartureAsync(a.TeamId, actorUserId, targetUserId, membershipId, removedBySomeoneElse: !isSelf, ct)` then `return MemberOpResult.Ok()`. Keep the existing comment about side effects living outside the delegate.
- [ ] T021 [US2] Add `private async Task AnnounceDepartureAsync(...)` to `TeamService.cs` with a doc comment (feature 064; after commit; best-effort; who is told; no admin named). This task implements the **removed-player half**: if `removedBySomeoneElse`, `_notifications.CreateAsync(targetUserId, TeamMemberRemoved, new TeamMemberRemovedPayload(slug, name), actorUserId: null, dedupeKey: $"team-removed:{membershipId}")` in its own `try`, then the email under `IsEnabledAsync(target, InvitesAndRoster, Email)` in its own `try` (recipient projection with `_db.PlayerProfiles` name → `MemberPlaceholder.For(culture)` fallback; culture from `PreferredLanguage`). Logs carry ids only, never names or addresses. Run T019's US2 facts green.

**Checkpoint**: commit `feat(064): a removed player is told, naming the team only (#385)`.

---

## Phase 5: User Story 3 — The other admins learn about departures (P2)

**Goal**: FR-014–FR-017 (+ FR-018–FR-020 for admins).

**Independent Test**: three admins; one removes a member → the other two are told; a member leaves → all three.

- [ ] T022 [US3] Add US3 facts to `TeamDepartureNoticeTests.cs`:
  - A removes M → B and C each get one `TeamMemberDeparted` (`removed: true`), A gets none, M gets none of this type;
  - L leaves → A, B, C each get one (`removed: false`), L none;
  - admin B leaves → A and C are told, B is not;
  - payload keys exactly `removed, teamName, teamSlug`, no display name or handle anywhere in `Payload`, `ActorUserId == departing player`, `DedupeKey == "team-departure:{membershipId}:{recipient}"`;
  - `GET /api/v1/notifications` for B shows `actorDisplayName` = the player's **current** name (rename the player's display name after the alert);
  - player banned / account deleted (`POST` the deletion route as 058 did) → `actorDisplayName` null;
  - a sole admin removing someone → no `TeamMemberDeparted` exists for that team (US3-6);
  - a banned admin is not told;
  - emails: B gets one "… wurde aus … entfernt"-style mail in B's language naming M and not A; an email failure for one admin (make `TestEmailSender` throw for one address if it supports it — otherwise assert per-recipient `try` by code review and note it) does not stop the other admin's mail;
  - each channel off for B → only that channel missing.
- [ ] T023 [US3] Complete `AnnounceDepartureAsync` in `TeamService.cs`: read admins after commit (`TeamMemberships` of the team, `Role == Admin`, `User.Status != Banned`, `UserId != actorUserId`, projecting `UserId`, `Email`, `PreferredLanguage`, name via `_db.PlayerProfiles`); `CreateManyAsync(adminIds, TeamMemberDeparted, new TeamMemberDepartedPayload(slug, name, removedBySomeoneElse), actorUserId: targetUserId, dedupeKeyPrefix: $"team-departure:{membershipId}")` in its own `try`; then email: `GetEnabledRecipientsAsync(adminIds, InvitesAndRoster, Email)`, the departing player's name read once through `_db.PlayerProfiles` (placeholder per recipient culture when null), **one `try` per recipient**. Read the team `{Slug, Name}` once for both halves. Run T022 green.
- [ ] T024 [P] [US3] Add `TeamMemberRemoved` and `TeamMemberDeparted` to the kinds list in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamRenameRewriteTests.cs` (seed rows with `teamSlug` payloads like its siblings) and run it (FR-023).
- [ ] T025 [P] [US3] Add a fact to `backend/tests/JuggerHub.Api.IntegrationTests/Parties/` (the existing party roster test file or a new `PartyRemovalSendsNothingTests.cs` on `PartyTestSupport`): a party admin removes an In crew member and a Declined player → no new `Notifications` rows, no email, no push for them (FR-009).

**Checkpoint**: full backend suite for `Teams|Parties|Notification|Push|Email` green; commit `feat(064): admins hear when a member leaves or is removed (#385)`.

---

## Phase 6: User Story 4 — A party admin confirms before removing crew or disbanding (P2)

**Goal**: FR-006–FR-009 on the party page.

**Independent Test**: Remove on *In* and *Declined*, and Disband, each ask in the in-page dialog; `window.confirm` never called.

- [ ] T026 [P] [US4] Add keys ×3 to the three catalogues: `parties.manage.removeInTitle` ("Take {{name}} out of the party?"), `removeInBody`, `removeInConfirm` ("Remove from party"), `removeDeclinedTitle` ("Clear {{name}}'s answer?"), `removeDeclinedBody` ("They'll count as not having answered and can be asked again."), `removeDeclinedConfirm` ("Clear answer"), `keepMember` ("Keep {{name}}"), `keepAnswer`, `removing`, `removeFailed`, `removeGone`, `disbandTitle` ("Disband this party?"), `disbandBody` (today's `confirmDisband` meaning: cannot be undone), `disbandKeep` ("Keep party"), `disbanding`, `disbandFailed`. Remove `parties.manage.confirmDisband` from all three if nothing else uses it (grep first).
- [ ] T027 [US4] Extend `frontend/apps/web/src/app/features/parties/party-manage/party-manage.component.spec.ts` (063's tests pass **unedited**): spy `window.confirm` and assert it is never called; In-tab Remove opens the dialog with the In title and calls nothing; Declined-tab Remove shows the Declined title; confirm calls `removeMember` once and reloads; 404 → dialog closed, page-level note, reload; 500 → error in the dialog, retry works; Disband opens the dialog; confirm calls `disband` and navigates to the team; disband failure → error in the dialog.
- [ ] T028 [US4] In `frontend/apps/web/src/app/features/parties/party-manage/party-manage.component.ts`: one `pendingConfirm` signal (`{ kind: 'remove', member, tab } | { kind: 'disband' } | null`), `confirmBusy`, `confirmError`, `removeNotice`; `askRemove(member)` / `askDisband()`; `dismissConfirm()` returns focus to the pressed button; `confirmPending()` runs the call with status branches (research R11) — not through `run()`/`fail()` (which render the server's `detail`, #179). Delete the `confirm(...)` call.
- [ ] T029 [US4] In `party-manage.component.html`: Remove buttons call `askRemove(m)` (carry `data-party-remove="<userId>"` for focus return), Disband calls `askDisband()` (`data-party-disband`); render one `<jh-confirm-dialog>` at the end of `<main>` (outside every card) with inputs chosen by `pendingConfirm().kind`/tab; the `removeNotice` line at page level beside `partyChatNotice`. Run T027 green.

**Checkpoint**: commit `feat(064): the party page asks before removing or disbanding (#385)`.

---

## Phase 7: The new Alerts rows (serves US2 and US3)

- [ ] T030 [P] [US2] Add keys ×3: `alerts.row.memberRemovedTitle` ("You're no longer a member of {{team}}"), `alerts.row.memberLeftTitle` ("{{name}} left {{team}}"), `alerts.row.memberRemovedByAdminTitle` ("{{name}} was removed from {{team}}"). Reuse the placeholder key the `TeamJoinRequest` row uses for a missing actor name.
- [ ] T031 [US2] In `frontend/apps/web/src/app/core/models/notification.models.ts`: add both types to the union, payload interfaces (`removed: boolean`), and `isTeamMemberRemoved` / `isTeamMemberDeparted` guards, mirroring `TeamJoinRequestAnswered`.
- [ ] T032 [US2] Extend `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.spec.ts`: Removed → title with team, no supporting line, link `/t/{slug}`, no actor rendered even if `actorDisplayName` were set; Departed left/removed → the right title with the actor name, placeholder when `actorDisplayName` is null, link `/t/{slug}`; both use the `user-minus` icon in the info tone.
- [ ] T033 [US2] In `notification-row.component.ts`/`.html`: `link()`, `title()` arms (supporting `''`), the icon `@case` for both → `user-minus`, and the tone class list (info, like `TeamRoleChanged`). Run T032 green.

**Checkpoint**: commit `feat(064): the Alerts inbox shows removals and departures (#385)`.

---

## Phase 8: User Story 5 — Joining by invitation is bounded (P3)

**Goal**: FR-024, FR-025.

**Independent Test**: the eleventh accept in an hour is refused with a translated "try again later"; nothing is retried.

- [ ] T034 [US5] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/InviteAcceptRateLimitTests.cs` (`[Collection("Teams")]`, shape of `JoinRequestRateLimitTests`): a player joins by the team's shared link (`GET /api/v1/teams/{slug}/invitations/link` as admin) and leaves, ten times; the eleventh accept → 429, the player is not a member, the link is still usable by someone else; the limit is per player (another player → 200); decline is not limited.
- [ ] T035 [US5] Add `TeamInviteAccept = "team-invite-accept"` + `TeamInviteAcceptsPerHour = 10` with a doc block in the shape of `JoinRequest`'s (why: a shared link stays usable, every leave now reaches every admin since 064; fixed window, ≤20 across an hour boundary; our own 429, never retried) and register it with `PartitionByUser(..., TimeSpan.FromHours(1))` in `backend/Security/RateLimitPolicies.cs`; add `[EnableRateLimiting(RateLimitPolicies.TeamInviteAccept)]` to `Accept` only in `backend/Controllers/InvitationsController.cs`. Run T034 green.
- [ ] T036 [P] [US5] Add `teams.inviteLimited` ×3 ("You've joined a lot of teams in a short time. Try again in a while." / German / Spanish). Convert *My team*'s English literal notice to a key `myTeam.inviteGone` ("That invitation to {{team}} is no longer available.") ×3.
- [ ] T037 [US5] Alerts inbox: in `frontend/apps/web/src/app/features/alerts/alerts.component.ts` `accept()`, on `429` do **not** mark the row resolved; set a page-level `inviteNotice` key rendered in `alerts.component.html` (`role="status"`). Add `frontend/apps/web/src/app/features/alerts/alerts.component.spec.ts` covering 429 (row stays actionable, notice shown) and 410/404 (row resolved as today).
- [ ] T038 [P] [US5] *My team*: in `frontend/apps/web/src/app/features/my-team/my-team.component.ts` `accept()`, 429 → keep the invitation, notice `teams.inviteLimited`; other errors → remove + `myTeam.inviteGone` (key, not literal; the template translates). Extend `my-team.component.spec.ts`.
- [ ] T039 [P] [US5] Onboarding: in `frontend/apps/web/src/app/features/onboarding/onboarding.component.ts` `acceptInvite()`, 429 → `inviteError` = the translated `teams.inviteLimited`; else today's sentence. Extend `onboarding.component.spec.ts`.
- [ ] T040 [P] [US5] Invite page: in `frontend/apps/web/src/app/features/teams/invite-accept/invite-accept.component.ts` the accept error branches on status: 429 → `teams.inviteLimited` (translated key, not `problemDetail`). Keep the other branches as they are (record the pre-existing `problemDetail` use as #179, do not widen scope). Add a focused spec file if none exists (`invite-accept.component.spec.ts`) for the 429 branch.
- [ ] T041 [P] [US5] Home: `frontend/apps/web/src/app/features/dashboard/modules/needs-you-card.component.ts` emits a new `limited` output on a `TeamInvite` accept `429` (item stays); `dashboard.component.ts`/`.html` set `needsYouNotice` to `teams.inviteLimited` without refreshing. Extend both specs.

**Checkpoint**: commit `feat(064): accepting team invitations is limited to ten an hour (#385)`.

---

## Phase 9: Polish & verification

- [ ] T042 Instantiate `specs/064-member-removal-notices/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and answer each item against the diff and the screenshots (DESIGN.md wins). Record: the dialogs' answers at the default 44px size; one coral CTA per view unaffected (danger answers are not coral); the news-post and join-confirm dialogs not moved onto `jh-confirm-dialog` (follow-up).
- [ ] T043 Run everything: `dotnet build backend/JuggerHub.slnx` (exit code), `dotnet test backend/JuggerHub.slnx`, and in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Fix what fails; never skip a failing test.
- [ ] T044 Browser walk per [quickstart.md](./quickstart.md) scenarios 1–8: `docker compose up -d --build backend frontend`, a standalone Playwright script inside `frontend/` (one context per actor, `locale: 'de-DE'`), screenshots at 375px and 1280px of: the team Remove dialog, the party In/Declined/Disband dialogs, J's and T's Meldungen rows, the German emails in Mailpit, the invite-limit message. Read the driver's output and look at every screenshot. Delete the script after.
- [ ] T045 File follow-ups: move the news-post delete dialog and the team page's join confirmation onto `jh-confirm-dialog`; telling admins when someone joins by accepting an invitation. Update memory with the feature's decisions and traps.
- [ ] T046 Push the branch and open the PR (`Closes #385`), body: summary, owner decisions, verification run, screenshots, residuals (sent emails/pushes are not recalled; fixed-window limit; addressed invitations share the limit).

---

## Dependencies & Execution Order

- **Phase 1** → **Phase 2** (T002 before T004–T006, T020+; T007–T010 before T021/T023; T012–T014 before T017/T028/T033).
- **US1 (Phase 3)** needs T014 only. **US4 (Phase 6)** needs T014 only. They can run alongside.
- **US2 (Phase 4)** → **US3 (Phase 5)**: same method and test file, sequential.
- **Phase 7** needs T002/T003's type names; independent of the backend sending them.
- **US5 (Phase 8)** is independent of US1–US4 (backend T034–T035, frontend T036–T041).
- **Phase 9** last.

## Parallel Example

```text
# After T002:
T003 payloads | T004 mapping test | T005 push arms | T007 templates | T008 localizer | T012 icon
# After T014:
Phase 3 (team page)  ||  Phase 6 (party page)  ||  Phase 7 (alerts rows)
# Phase 8 frontend:
T038 my-team | T039 onboarding | T040 invite page | T041 home
```

## Implementation Strategy

MVP = Phase 2 + **US1** (the confirmation prevents harm on its own) + **US2**. Then US3, US4, the
Alerts rows, US5. Each checkpoint is a green, committable increment.
