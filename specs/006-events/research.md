# Phase 0 — Research: Events

Decisions that resolve the one item `/speckit-clarify` deferred to planning (Event model reconciliation) plus the modeling/authorization choices the feature needs. Each: **Decision → Rationale → Alternatives considered**. Product decisions already locked live in [spec.md](./spec.md) `## Clarifications`; this file is engineering strategy.

## §1 — Reconcile the rich event with the existing minimal `Event`

**Decision**: **Extend the existing `Event` entity in place** into the full event; do **not** introduce a parallel event entity. Add: `Type` (`EventType`) + `CustomTypeLabel?`, `Description`, `StartsAt`/`EndsAt` (`DateTime`, UTC) **replacing** the single `Date` (`DateOnly`), `LocationKind` + address parts + `VirtualLink?`, `ParticipantMode`, `ParticipationLimit`, fee columns, `Status` + `CancelledDate?`. Update the two activity readers (`EventActivityService`, `TeamActivityService`) to order/derive from `StartsAt`; the activity DTO shape (`ActivityItemDto`) is **unchanged** — the start day is produced by mapping `StartsAt` → `DateOnly` in memory after materialization (avoids relying on EF translating `DateOnly.FromDateTime`).

**Rationale**: `Event.cs`'s own XML doc says it is "the foundation for a later events feature" — this is that feature. One model keeps activity and the live event referring to the same row, avoids a duplicate/confusing second event table, and reuses existing indexes. No Prod event data exists (events are Dev/local-seeded), so replacing `Date` with `StartsAt`/`EndsAt` in the `AddEvents` migration is safe; the seeder is updated in the same change.

**Alternatives considered**: (a) *New `TournamentEvent`/`HostedEvent` entity separate from `Event`* — rejected: two "event" concepts confuse the model and the activity join, and the existing comment explicitly anticipates extending this one. (b) *Keep `Date` and add `StartsAt`/`EndsAt` alongside* — rejected: redundant, two sources of truth for "when". (c) *Change `ActivityItemDto.Date` to `DateTime`* — rejected: ripples into the 003/005 profile/team activity frontend for no benefit; in-memory `DateOnly.FromDateTime(StartsAt)` keeps the contract stable.

## §2 — `EventSignup` (live registration) is distinct from `EventParticipation` (activity)

**Decision**: Add a **new** `EventSignup` entity for the sign-up/joined/waitlist workflow. Leave the existing `EventParticipation` (a `PlayerProfile` "played event X with team-label Y", the basis for recent-activity, seeded) **untouched**. They are different concepts and do not share a table.

**Rationale**: `EventParticipation` is a profile-centric historical attendance snapshot with a `TeamLabel`/`TeamId` and a unique `(ProfileId, EventId)`; it has no lifecycle, no waitlist, no payment, and its subject is always a profile. `EventSignup` is a live registration with a status lifecycle (joined/awaiting/waitlisted), a polymorphic subject (user **or** team), payment confirmation, and capacity semantics. Overloading one table would force nullable-everything and muddy both activity reads and sign-up logic. Bridging a concluded event's joined sign-ups into `EventParticipation` (so they show up as activity afterwards) is **out of scope** and noted as a future follow-up.

**Alternatives considered**: (a) *Reuse `EventParticipation` for sign-ups* — rejected: subject is profile-only, no status/waitlist/payment, unique key collides with re-signup-after-withdraw. (b) *Name the new entity `EventParticipant`* — rejected: one letter from `EventParticipation`, high confusion; `EventSignup` reads as the action users take ("sign up").

## §3 — Polymorphic sign-up subject (user XOR team)

**Decision**: `EventSignup` carries **nullable `UserId`** and **nullable `TeamId`**; exactly one is set, matching the event's `ParticipantMode` (individuals-only → `UserId`; teams-only → `TeamId`). Enforce with a DB `CHECK ((UserId IS NULL) <> (TeamId IS NULL))` and two **partial unique** indexes — unique `(EventId, UserId) WHERE UserId IS NOT NULL` and unique `(EventId, TeamId) WHERE TeamId IS NOT NULL` — so a user/team appears at most once per event. The service also validates the subject against the event's mode before insert.

**Rationale**: A single table with a discriminated subject keeps capacity counting and the three-group read uniform (one `WHERE EventId = … AND Status = …`), regardless of mode. Partial unique indexes give the duplicate guard at the DB layer (defence in depth alongside the service check), matching how 005 guards duplicate invites. The `CHECK` makes "exactly one subject" a hard invariant.

**Alternatives considered**: (a) *Two tables (`EventUserSignup`, `EventTeamSignup`)* — rejected: doubles every query, capacity count becomes a union, more code for no gain. (b) *A single `SubjectId` + `SubjectKind` enum* — rejected: loses real FKs (no cascade/no referential integrity to Users/Teams), needs manual join fan-out. Two typed nullable FKs keep proper FKs + cascade.

## §4 — Capacity, spot accounting & "no auto-promotion" (atomic)

**Decision**: **Occupied spots = count of signups with `Status ∈ {Joined, AwaitingApproval}`**; `Waitlisted` never counts. Sign-up, promote, and (for completeness) any admission run inside a `Serializable` transaction that first does `SELECT 1 FROM "Events" WHERE "Id" = … FOR UPDATE`, then counts occupied, then decides: free+open → `Joined`; paid+open → `AwaitingApproval`; full → `Waitlisted`. **Promotion** re-checks capacity under the same lock and refuses if it would exceed the limit (free → `Joined`, paid → `AwaitingApproval`). **Withdraw/remove never auto-promotes** — a freed spot simply stays open until an admin acts. Reuse the exact row-lock pattern from `TeamService.MutateMembershipAsync`.

**Rationale**: Directly mirrors the proven 005 last-admin guard, so concurrent last-spot sign-ups (and concurrent promotions) can't exceed the limit — the second waiter blocks, re-reads the committed count, and falls through to waitlist / is refused. Counting `AwaitingApproval` as occupied realizes the wireframe's "holds a provisional spot" and "you're not charged unless a spot opens". Manual-only promotion is a spec invariant (FR-020), so there is deliberately no trigger/auto-fill anywhere.

**Alternatives considered**: (a) *Optimistic concurrency via a `RowVersion` on `Event` + retry* — viable but the pessimistic row lock is already the house pattern (005) and simpler to reason about for a low-write surface. (b) *A denormalized `OccupiedCount` column* — rejected: another invariant to keep in sync; counting an indexed `(EventId, Status)` is cheap.

## §5 — Event admins, last-admin guard & co-admin invitations

**Decision**: A new `EventAdmin` (EventId, UserId, AddedDate) grants **all** admin powers equally (clarified: all admins share powers). The creator gets the first `EventAdmin` row at create time (explicit `DbSet.Add`, same transaction as the event). Removal/step-down runs under the event-row lock and **refuses if it would drop the event to zero admins** (last-admin guard, mirroring teams). Co-admin invites reuse the 005 invitation model as a new `EventAdminInvitation` (same `InvitationKind`/`InvitationStatus` enums, raw capability token, 7-day expiry, revoke, partial-unique active-link + pending-targeted). **Accepting grants an `EventAdmin` row** (not a membership). Preview is anonymous (public event fields + inviter); accept/decline require auth.

**Rationale**: Maximal reuse of a slice that already exists and is tested. Admin parity + last-admin guard is exactly the team role model minus the two-role distinction, so `EventAdmin` needs no `Role` column. Sharing the invitation enums avoids a parallel enum set.

**Alternatives considered**: (a) *Owner + co-admin two-tier* — rejected by clarification (all admins equal). (b) *A generic polymorphic `Invitation` table for teams and events* — rejected this iteration: retrofitting 005's `TeamInvitation` into a shared table is a refactor beyond scope; a sibling `EventAdminInvitation` reusing the enums is lower-risk. A future unification is noted.

## §6 — Fee model & public visibility

**Decision**: Fee lives on `Event`: `IsPaid` (bool), `FeeAmount` (`decimal?`), `FeeCurrency` (`string(3)?`, default `EUR`), `FeeRecipientName`, `FeeIban`, `FeePaymentDeadline` (`DateOnly?`). Paid events **require** `FeeRecipientName` + `FeeIban`; free events store none. The fee block (including recipient + IBAN) is shown **publicly** on the event page as payment instructions. There is **no** in-app payment; `AwaitingApproval → Joined` is a manual admin "payment received" confirmation (`PaymentConfirmedDate` set).

**Rationale**: The IBAN/recipient are the organiser's collection details they intend to publish so participants can pay by bank transfer (wireframe: "Pay to…", "Participants pay by bank transfer") — content, not an app-held secret. Structured amount + currency (rather than free text) renders cleanly and is still optional. Deadline is informational only (see §7).

**Alternatives considered**: (a) *Free-text fee blob* — rejected: harder to render/validate; structured amount+currency is trivial. (b) *Restrict IBAN to authenticated users* — rejected: participants must see how to pay before committing, matching the public-page decision; the organiser chooses to publish it.

## §7 — Payment deadline does not auto-expire holds

**Decision**: `FeePaymentDeadline` is **informational**. An `AwaitingApproval` hold persists until an admin approves or removes it; there is **no** background job dropping unpaid holds at the deadline this iteration.

**Rationale**: Keeps the iteration free of scheduled jobs/infrastructure (none exists yet) and matches the spec assumption. Admins already have remove; a future timed-expiry can layer on without model change.

**Alternatives considered**: *A hosted `BackgroundService` sweeping expired holds* — deferred: adds operational surface (a scheduler) with no current requirement.

## §8 — Cancellation & notifications by email

**Decision**: Cancel sets `Status = Cancelled` + `CancelledDate` (irreversible — no reactivate endpoint). The page stays readable. All sign-up/approve/promote endpoints refuse when `Status = Cancelled` (and when `EndsAt` is in the past). On cancel, send a **transactional email** (`event-cancelled.html`) to everyone `Joined`/`AwaitingApproval`/`Waitlisted`: for **individual** signups, the user's email; for **team** signups, the team's **admins'** emails (a team has no single email). Reuse `IEmailSender`/`IEmailTemplateService` + `EmailOptions.FrontendBaseUrl` for the event link. Best-effort send (failure to email one recipient does not roll back the cancel).

**Rationale**: Email is the clarified channel and the existing infra (Mailpit/Resend) already backs 002/005 — no new infrastructure. Notifying team admins is the sensible target since teams sign up via an admin (FR-013). Cancellation is a state flag, so the page and its history remain intact (spec FR-010/FR-032).

**Alternatives considered**: (a) *In-app notifications* — rejected by clarification (deferred). (b) *Transactional (rollback cancel if email fails)* — rejected: a mail hiccup must not block cancelling; sends are best-effort and logged.

## §9 — Event URL identity: id, not slug

**Decision**: Events are addressed by their **`Id` (UUIDv7)** at `/events/{id}`; **no** human slug. The public detail GET is anonymous.

**Rationale**: Per constitution, UUIDv7 is "unguessable enough to expose safely in URLs". Events are numerous and short-lived; forcing a unique slug per event adds a wizard step and a namespace to police for no user benefit (unlike a team's durable `/t/<slug>` handle). Direct-link-only (clarified) is satisfied by an id URL.

**Alternatives considered**: *Per-event slug like teams* — rejected: extra friction + collision handling; events aren't handles people memorize.

## §10 — Reused patterns (no new research needed)

- **Pagination** — `PaginationRequest`/`PagedResult<T>` on every list (participants ×3, news, contacts, invitations, user-search). Established.
- **Slug/token generation** — `RandomNumberGenerator` base64url token, stored raw (capability), unique index — copied from `TeamInvitationService.NewToken()`.
- **User search to invite** — reuse the `ILike` display-name/handle search + `UserRelation` (Admin/Invited/Invitable) shape from `TeamInvitationService.SearchUsersAsync`.
- **Result-enum → HTTP mapping** — services return typed status enums; the controller `switch`es to `Ok/Created/Problem(...)`, exactly like `TeamsController`.
- **Client-GUID nav-insert gotcha** — event + creator-`EventAdmin` inserted via explicit `DbSet.Add` (not nav-collection), one `SaveChanges` (EF gotcha memory).
- **Enum-name serialization** — global `JsonStringEnumConverter` already registered; new enums need no per-property config.

**Output**: all deferred/unknown items resolved. Ready for Phase 1 design.
