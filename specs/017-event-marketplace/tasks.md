# Tasks: Event Marketplace (Mercenaries)

**Feature**: 017-event-marketplace | **Branch**: `017-event-marketplace`
**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/marketplace-api.md](./contracts/marketplace-api.md),
[quickstart.md](./quickstart.md)

Tests are included (the spec's Independent Tests + Success Criteria demand server-side, concurrency, and
scoping coverage). `[P]` = parallelizable (different files, no incomplete-task dependency). Paths are
repo-relative. Mirrors the feature-016 parties slice and feature-006 events machinery throughout.

---

## Phase 1: Setup

- [ ] T001 Confirm branch `017-event-marketplace` is checked out and the 016 migration is applied locally (`dotnet ef database update` from `backend/`); bring the stack up (`docker compose up -d`).
- [ ] T002 [P] Instantiate the UI review checklist: copy `.specify/templates/ui-review-checklist-template.md` to `specs/017-event-marketplace/checklists/ui-review.md` (filled during the frontend phases).

---

## Phase 2: Foundational (blocking prerequisites — MUST complete before any user story)

### Data layer

- [ ] T003 [P] Add `backend/Entities/MarketEnums.cs` with `MarketRequestDirection { Application, Invite }` and `MarketRequestStatus { Pending, Accepted, Declined, Revoked }` (XML docs; serialized as name).
- [ ] T004 [P] Add `backend/Entities/MercenaryListing.cs` (`BaseEntity`: `EventId`, `UserId`, `List<Pompfe> Positions`, `Pitch`, nav `Event`/`User`) per data-model.md.
- [ ] T005 [P] Add `backend/Entities/MarketRequest.cs` (`BaseEntity`: `PartyId`, `UserId`, `Direction`, `List<Pompfe> Positions`, `Status`, `CreatedByUserId`, nav `Party`/`User`) per data-model.md.
- [ ] T006 Edit `backend/Entities/Party.cs` — add recruiting columns `IsRecruiting` (default false), `SpotsAdvertised`, `RecruitBlurb`, `List<Pompfe> PositionsNeeded`; add `ICollection<MarketRequest> MarketRequests`.
- [ ] T007 Edit `backend/Entities/PartyMember.cs` — add `bool ViaMarket` (default false, "guest · via market") with XML doc.
- [ ] T008 Edit `backend/Entities/NotificationEnums.cs` — append `NotificationType.MarketInvite = 5` and map it to `NotificationCategory.InvitesAndRoster` in `NotificationCategories.For`.
- [ ] T009 Edit `backend/Data/AppDbContext.cs` — add `DbSet<MercenaryListing>`/`DbSet<MarketRequest>`; configure: MercenaryListing unique `(UserId,EventId)` + index `(EventId)` + FKs cascade + `Pitch` maxlen 280; MarketRequest filtered-unique `(PartyId,UserId) WHERE "Status"=0` + indexes `(PartyId,Status)`/`(UserId,Status)` + FK `Party` cascade, `User` cascade, `CreatedBy` restrict; Party recruiting columns (`RecruitBlurb` maxlen 500, `PositionsNeeded` default `{}`), `PartyMember.ViaMarket` default false.
- [ ] T010 Create the EF migration `AddEventMarketplace`: `dotnet ef migrations add AddEventMarketplace` (from `backend/`); verify the generated Up/Down match data-model.md (two tables, filtered-unique index, four Party columns, one PartyMember column, `integer[]` defaults `'{}'`).

### Guest reconciliation of the 016 read paths

- [ ] T011 Edit `backend/Dtos/Parties/PartyDtos.cs` — add `bool ViaMarket` to `PartyMemberDto` (last positional field).
- [ ] T012 Edit `backend/Services/Parties/PartyService.cs` `ProjectAsync` — count guests: change In/Declined predicates to `(m.ViaMarket || x.Team.Memberships.Any(tm => tm.UserId == m.UserId))`.
- [ ] T013 Edit `backend/Services/Parties/PartyRosterService.cs` — In/Declined `ListGroupAsync` branches and `LoadMineAsync` include `m.ViaMarket` in the predicate and project `m.ViaMarket` into `PartyMemberDto`; `NoResponse` branch unchanged (project `ViaMarket=false`).
- [ ] T014 Allow guests (crew, not team members) to read the party hub: in `backend/Services/Parties/PartyService.cs` `GetDetailAsync` and `backend/Services/Parties/PartyNewsService.cs` list, change the member gate from `!IsTeamMember` to `!(IsTeamMember || IsCrew)` so an `In` guest can view detail + news (still 404 for outsiders).

### DTOs, services scaffolding, DI

- [ ] T015 [P] Add `backend/Dtos/Marketplace/MarketDtos.cs` with all response/request records from contracts/marketplace-api.md (`MarketListingCardDto`, `RecruitingPartyCardDto`, `MyMarketDto`, `MarketListingDto`, `MarketRequestDto`, `MyMarketRequestDto`, `RecruitingSettingsDto`, `MarketInvitableUserDto`, `PostListingRequest`, `ApplyRequest`, `InviteRequest`, `SetRecruitingRequest`, relation enums).
- [ ] T016 [P] Add `backend/Services/Marketplace/MarketEligibility.cs` — `Task<bool> IsInAPartyAsync(Guid eventId, Guid userId, ct)` (any `PartyMember` with `Status=In` on a party whose `EventId=eventId`); reused by every post/apply/invite/accept path.
- [ ] T017 [P] Add service interfaces `IMarketListingService`, `IMarketRecruitingService`, `IMarketRequestService` in `backend/Services/Marketplace/` (method signatures returning `PartyResult`/`PartyResult<T>` and `PagedResult<T>`, per contracts).
- [ ] T018 [P] Add `backend/Services/Email/MarketEmailService.cs` + `backend/EmailTemplates/market-invite.html` (extends the base header/footer; "{{inviter}} invited you to {{team}}'s crew for {{event}}"), mirroring `PartyEmailService.SendCoAdminInviteEmailAsync`.
- [ ] T019 Register the three marketplace services + `MarketEligibility` + `MarketEmailService` in DI (`backend/Program.cs` or the services extension where `PartyService`/`PartyEmailService` are registered).

### Frontend foundation

- [ ] T020 [P] Add `frontend/apps/web/src/app/core/models/market.models.ts` — TS types matching the DTOs (positions as `Pompfe` string unions reused from the existing pompfe model).
- [ ] T021 [P] Add `frontend/apps/web/src/app/core/services/market.service.ts` — typed HTTP methods for every endpoint in contracts/marketplace-api.md (`withCredentials`, `PagedResult<T>` envelope), mirroring `party.service.ts`.

**Checkpoint**: builds green (`dotnet build`, `npx nx build web`), migration applies, 016 party detail/roster still pass with guests counted.

---

## Phase 3: User Story 1 — Post yourself as a mercenary (P1) 🎯 MVP

**Goal**: The board renders on a teams event; an eligible individual posts/edits/takes down a listing.
**Independent test**: quickstart Scenario A.

- [ ] T022 [US1] Implement `MarketListingService` in `backend/Services/Marketplace/MarketListingService.cs`: `GetMyMarketAsync` (mode/eligibility/my-listing), `PostAsync` (eligible + no existing listing + teams event + event open → create; else 409), `EditAsync`, `TakeDownAsync` (hard delete), and `ListFreeAgentsAsync` (board free-agents side, optional position overlap filter, paginated, public). Reuse `MarketEligibility`.
- [ ] T023 [US1] Add `backend/Controllers/MarketController.cs` (route `api/v1/events/{eventId:guid}/market`): `GET /free-agents` **[AllowAnonymous]**, `GET /me`, `POST/PUT/DELETE /listing`. Thin; map `PartyOutcome`→HTTP via a shared `Fail` helper (copy the `PartiesController` pattern).
- [ ] T024 [P] [US1] Backend tests `backend/tests/JuggerHub.Api.IntegrationTests/Marketplace/ListingTests.cs`: post as eligible → on board; ineligible (In a party) → 409; one-listing-per-user (second POST 409, PUT edits); take-down 204; board hidden on individuals-only event; board readable anonymously.
- [ ] T025 [US1] Build `frontend/.../features/marketplace/market-board/` (`.ts/.html/.css`): two-sided board with a segmented control (free agents / parties) and a position filter chip row; renders `MarketListingCardDto` cards (avatar, name, positions, pitch) with viewer-aware affordance slot. (Parties side wired in US2.)
- [ ] T026 [US1] Build `frontend/.../features/marketplace/listing-editor/` sheet (`.ts/.html/.css`): positions multi-select (pompfen chips) + pitch textarea; post/edit/take-down via `market.service`.
- [ ] T027 [US1] Edit `frontend/.../features/events/event-detail/event-detail.component.*` — below `EventParticipantGroupsComponent`, embed `MarketBoardComponent` for teams events plus the "post yourself / your listing" entry (from `GET /me`); keep the board browsable when ineligible with the reason shown.
- [ ] T028 [P] [US1] Specs (zoneless): `market-board.component.spec.ts`, `listing-editor.component.spec.ts` (render, filter, post/edit/take-down, ineligible state).

**Checkpoint**: US1 independently demonstrable — an eligible user posts and appears on the board.

---

## Phase 4: User Story 2 — Put your party on the board (P1)

**Goal**: Party admin opts the party into recruiting; it appears on the parties board side.
**Independent test**: quickstart Scenario B.

- [ ] T029 [US2] Implement `MarketRecruitingService` in `backend/Services/Marketplace/MarketRecruitingService.cs`: `GetAsync` (party admin → `RecruitingSettingsDto` incl. `InCount`/`OpenSpots`), `SetAsync` (party admin; validate `SpotsAdvertised` 0..RosterCap, positions subset of `Pompfe`, blurb ≤500; refuse on cancelled/ended event), and `ListRecruitingPartiesAsync` (board parties side for an event: `IsRecruiting` + `OpenSpots>0`… include full parties as closed per research §12; optional position-overlap filter; paginated; public). Use `PartyGuard`.
- [ ] T030 [US2] Edit `backend/Controllers/PartiesController.cs` — add `GET /parties/{id}/recruiting` and `PUT /parties/{id}/recruiting` (party-admin gated in the service). Add `GET /free-agents` sibling `GET .../market/parties` on `MarketController` (**[AllowAnonymous]**) calling `ListRecruitingPartiesAsync`.
- [ ] T031 [P] [US2] Backend tests `Marketplace/RecruitingTests.cs`: default off; admin toggles on → on board with correct open spots; non-admin PUT → 403; off retains prior applications; full party auto-closes (no apply state); cancelled/ended event → 409.
- [ ] T032 [US2] Build recruiting block in `frontend/.../features/parties/party-manage/` (new child component `recruiting-block` `.ts/.html/.css`): "looking for players" switch, spots stepper (`− N +` of cap), positions-needed chips, blurb; saves via `market.service`.
- [ ] T033 [US2] Wire the parties side of `MarketBoardComponent` to render `RecruitingPartyCardDto` cards (team logo, open spots, needed positions, blurb) with the Apply affordance slot (wired in US3).
- [ ] T034 [P] [US2] Specs: `recruiting-block.component.spec.ts` (default off, toggle, validation), board parties-side render.

**Checkpoint**: US1+US2 — both board sides real.

---

## Phase 5: User Story 3 — The two-way handshake (P1)

**Goal**: Apply (merc→party) and invite (party→merc) create pending requests; revoke/decline drop them.
**Independent test**: quickstart Scenario C.

- [ ] T035 [US3] Implement create/list in `backend/Services/Marketplace/MarketRequestService.cs`: `ApplyAsync` (caller eligible, party recruiting, event open, open spot, ≤1 active pair → create `Application`), `InviteAsync` (party admin, target eligible, ≤1 active pair, event open → create `Invite`), `RevokeAsync` (initiator only), `DeclineAsync` (recipient only), `ListPartyApplicationsAsync`/`ListPartyInvitesAsync` (party admin), `ListMyInvitesAsync`/`ListMyApplicationsAsync` (event-scoped, caller). Enforce direction→actor per contracts; race-safe on the filtered-unique index.
- [ ] T036 [US3] Edit `backend/Controllers/PartiesController.cs` — add `POST /parties/{id}/market/applications`, `POST /parties/{id}/market/invites`, `GET /parties/{id}/market/applications`, `GET /parties/{id}/market/invites`. Add request-scoped `POST /market/requests/{id}/decline` and `POST /market/requests/{id}/revoke` on `MarketController`; extend `GET .../events/{eventId}/market/me` to include invites-to-answer + my-applications.
- [ ] T037 [P] [US3] Backend tests `Marketplace/HandshakeTests.cs`: apply/invite create pending + appear in both inboxes; duplicate active → 409; non-admin invite/revoke-on-behalf → 403; wrong-actor accept/decline/revoke → 403; ineligible target/applicant → 409; decline/revoke drop; no roster change on any of these.
- [ ] T038 [US3] Build `frontend/.../features/marketplace/apply-sheet/` and `invite-sheet/` (`.ts/.html/.css`): position pick + confirm; call `market.service`. Wire the board's Apply (parties card) and Invite (free-agent card) affordances to open them.
- [ ] T039 [US3] Build `frontend/.../features/marketplace/recruiting-inbox/` (`.ts/.html/.css`): party-admin view — applications (accept/decline) + invites sent (awaiting/declined, revoke) + fill count; add its route under the party path in `app.routes.ts`.
- [ ] T040 [US3] Build `frontend/.../features/marketplace/my-market/` (`.ts/.html/.css`): mercenary view — invites to answer + my applications with status; embed in event-detail below the board.
- [ ] T041 [P] [US3] Specs: `apply-sheet`, `invite-sheet`, `recruiting-inbox`, `my-market` component specs (create, revoke, decline, empty states).

**Checkpoint**: US1–US3 — the board connects both sides via pending requests.

---

## Phase 6: User Story 4 — Accept lands the mercenary in the party (P1)

**Goal**: Accepting seats a guest atomically; joining cancels other requests + takes down the listing.
**Independent test**: quickstart Scenario D.

- [ ] T042 [US4] Implement `AcceptAsync` in `MarketRequestService`: resolve request + authorize recipient by direction; transaction + `PartyCapacity.LockPartyRowAsync`; re-check eligibility + `InCountAsync < RosterCap` (else 409 Full/Conflict); insert/flip the `PartyMember` (`In`, `ViaMarket=true`, `Role=Member`); set request `Accepted`; `ExecuteUpdateAsync` the joiner's other `Pending` requests for this event → `Revoked` (set `ModifiedDate`); delete their `MercenaryListing` for this event; commit.
- [ ] T043 [US4] Add `POST /market/requests/{id}/accept` to `MarketController` returning `MarketRequestDto`; ensure the `MarketInvite` notification (US5) is marked actioned on accept.
- [ ] T044 [P] [US4] Backend tests `Marketplace/AcceptTests.cs`: accept application → guest In with `ViaMarket`, InCount+1; accept invite → same; **concurrent** last-spot accepts → exactly one seats, other 409 (cap never exceeded); join cancels other pending + deletes listing; accept at cap → 409; guest is not a separate `EventSignup` after apply; guest appears in the In roster group with the tag.
- [ ] T045 [US4] Frontend: wire Accept on `recruiting-inbox` (applications), `my-market` (invites), and the board; on success refresh board/roster/fill. Render the "guest · via market" tag in the party-manage roster row (`PartyMemberDto.viaMarket`).
- [ ] T046 [P] [US4] Specs: accept flows update counts + show guest tag; full-state disables Apply/Accept.

**Checkpoint**: US1–US4 = MVP — a mercenary can be discovered, apply/be-invited, and land in a crew.

---

## Phase 7: User Story 5 — One shared inbox + reach (P2)

**Goal**: Both inboxes usable; mercenary reachable on the dashboard + a notification/email per invite.
**Independent test**: quickstart Scenario E (inbox/dashboard/notification).

- [ ] T047 [US5] In `MarketRequestService.InviteAsync`, send the target an in-app `MarketInvite` notification (`INotificationService.CreateAsync`, inline accept/decline payload per contracts) **and** an email via `MarketEmailService`; respect feature-011 preferences; never throw into the action.
- [ ] T048 [US5] Add `GET api/v1/market/mine` (cross-event, caller-scoped pending invites+applications, newest-first, paginated) to `MarketController` + a `MyMarketRequestDto` projection in `MarketRequestService`.
- [ ] T049 [P] [US5] Backend tests `Marketplace/ReachTests.cs`: invite → recipient notification + email (Mailpit/among captured sends) subject to preferences; `market/mine` returns only the caller's requests across events; inbox lists paginate with empty states.
- [ ] T050 [US5] Build `frontend/.../features/dashboard/modules/market-card.component.*` (NEW) — pending invites/applications summary with accept/decline; add it to `dashboard.component.*`.
- [ ] T051 [US5] Edit `frontend/.../features/alerts/*` — render `MarketInvite` with inline Accept/Decline (posting to `market/requests/{id}/accept|decline`), mirroring `TeamInvite`/`PartyRequest`.
- [ ] T052 [P] [US5] Specs: `market-card.component.spec.ts`; alerts renderer spec for `MarketInvite`.

**Checkpoint**: mercenary reachable in all three places.

---

## Phase 8: User Story 6 — Invite anyone directly (P2)

**Goal**: Party admin searches any eligible user (name/@handle) and invites — no listing required.
**Independent test**: quickstart Scenario E step 3.

- [ ] T053 [US6] Add `SearchInvitableUsersAsync` to `MarketRequestService` (or a small `MarketDirectoryService`): all-user ILike over `PlayerProfile.DisplayName`/`Handle` (mirror `EventInvitationService.SearchUsersAsync`), paginated, annotate each with `Relation` (Invitable / Invited / Ineligible-in-a-party). Reuse `InviteAsync` for the actual invite (no listing needed).
- [ ] T054 [US6] Add `GET /parties/{id}/market/user-search` to `PartiesController` (party-admin gated).
- [ ] T055 [P] [US6] Backend tests `Marketplace/DirectInviteTests.cs`: search returns users by name/@handle; ineligible annotated + invite refused (409); direct invite delivers as notification + inbox; revoke works; non-admin → 403.
- [ ] T056 [US6] Build `frontend/.../features/marketplace/direct-invite/` (`.ts/.html/.css`): search box + results with Invite buttons + a pending list with Revoke; reachable from the recruiting inbox / manage party.
- [ ] T057 [P] [US6] Spec: `direct-invite.component.spec.ts` (search, invite, ineligible, revoke).

**Checkpoint**: US1–US6 — full public + direct recruiting.

---

## Phase 9: User Story 7 — The mercenary is a normal party member (P3)

**Goal**: Guest shows in the roster with a tag, can view the hub, and is cleaned up on removal/disband.
**Independent test**: quickstart Scenario F.

- [ ] T058 [US7] Verify/adjust `PartyRosterService.RemoveAsync` + `LeaveAsync` for guests: a guest removed/leaving frees the spot with no team/badge side-effects; ensure the guest-read gate from T014 lets a guest hit `GET /parties/{id}` and news.
- [ ] T059 [US7] Extend `PartyService.DisbandAsync` (and confirm the `MarketRequest.PartyId` cascade) so disband removes the party's guests, pending market requests, and clears recruiting; withdraw/event-close leave listings/requests inert (event-open checks already refuse writes).
- [ ] T060 [P] [US7] Backend tests `Marketplace/GuestLifecycleTests.cs`: guest in In roster with tag + counted; guest can read party detail/news; remove/leave frees spot, no team membership/badge created; disband removes guests + requests + recruiting; guest never becomes co-admin (co-admin invite to a non-team-member refused).
- [ ] T061 [US7] Frontend: mercenary "you're in · via market" state on the event page (from `GET /me`) linking to the party hub; ensure the guest can open party-manage/news as a member (read-only compose for non-admins).
- [ ] T062 [P] [US7] Spec: guest viewer state renders the "you're in via market" card and hub link.

**Checkpoint**: all user stories complete.

---

## Phase 10: Polish & Cross-Cutting

- [ ] T063 [P] Seed data: edit `backend/Data/DevDataSeeder.cs` to add a couple of `MercenaryListing`s, flip one party to recruiting, and add a sample pending application + invite (idempotent, guarded like existing seeds).
- [ ] T064 [P] Run the UI review checklist `specs/017-event-marketplace/checklists/ui-review.md` against the diff (DESIGN.md tokens/components: cards, segmented control, position chips, sheets, inbox rows, empty/loading/error states, responsive phone+desktop, sentence case, one coral CTA/view).
- [ ] T065 Walk `quickstart.md` Scenarios A–F end-to-end locally (Mailpit for email) and fix any gaps.
- [ ] T066 Verify: `dotnet build` + `dotnet test` (backend), `npx nx build web` + `npx nx lint web` + `npx nx test web` (frontend); record results for the report.
- [ ] T067 [P] Update memory + open a PR referencing the spec; note any spec/design drift.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, T003–T021)** block everything. Within Foundational: T003–T008 before T009; T009 before T010; T011 before T012–T013; T015–T021 [P] after enums exist.
- **User stories** in priority order: US1 (T022–T028) → US2 (T029–T034) → US3 (T035–T041) → US4 (T042–T046) are all **P1** and build the MVP loop. US5 (T047–T052) and US6 (T053–T057) are **P2**; US7 (T058–T062) is **P3**.
- Cross-story service dependency: US4 accept and US5/US6 reuse `MarketRequestService` created in US3; US2's board parties-side feeds US3's Apply affordance. US7 depends on US4 (guests exist).
- **Polish (T063–T067)** last.

## Parallel Opportunities

- Foundational: T003/T004/T005 (entities) and T015/T016/T017/T018/T020/T021 (DTOs, helper, interfaces, email, client models/service) run in parallel.
- Every `[P]` test task runs alongside its story's implementation once the service/endpoint exists.
- Frontend component builds within a story ([P] specs) parallelize against backend tests.

## MVP Scope

**US1–US4 (P1)**: post a listing, recruit, handshake, and accept into a crew — the complete two-sided
loop. US5–US7 add reach (dashboard/notification/email), direct invites, and guest-lifecycle polish.

## Format validation

All tasks use `- [ ] Txxx [P?] [USn?] description + path`. Setup/Foundational/Polish carry no story
label; every user-story task carries its `[USn]` label and a concrete file path.
