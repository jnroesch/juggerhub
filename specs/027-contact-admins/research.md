# Research: Contact the Admins (027)

Phase 0 decisions. Everything here is grounded in the feature-019 chat implementation
(`backend/Services/Chat/*`, `backend/Entities/Conversation*.cs`, `backend/Data/AppDbContext.cs`) and
the team/event admin models, read at planning time.

## §1 — Conversation shape: new mirrored kinds vs. reused Group

**Decision**: Introduce two new `ConversationKind` values, **`TeamInquiry`** and **`EventInquiry`**,
that mirror their admin roster the same way `Team`/`Party` already do.

**Rationale**: The spec's two hard requirements — admin membership derived live from the roster
(FR-006) and visual distinguishability with a per-kind name/tag (FR-008/009/010) — are *exactly* the
properties the mirrored kinds already have. `ChatGuard` derives Team/Party membership from
`TeamMemberships`/`PartyMembers` on every request so "removed ⇒ loses the chat" is true by
construction; an inquiry thread wants the identical guarantee against the admin roster. A plain
`Group` would store a fixed member list, needing a sync step on every admin change (the failure mode
019 deliberately designed out) and a special-cased name.

**Alternatives considered**:
- *Reuse `Group` with special naming* — rejected: no derived membership; admin churn drifts.
- *A brand-new entity outside `Conversation`* — rejected: throws away the message pipeline, inbox,
  read-state, realtime, archival, and search that already exist and are the whole point of "use the
  chat feature."

## §2 — Naming: why `Inquiry`, not `Contact`

**Decision**: Name the kinds `TeamInquiry` / `EventInquiry`, the requester FK `RequesterUserId`, and
the **user-facing tag "ADMINS"**. Keep "Contact admins" only as the button label.

**Rationale**: A `EventContact` entity already exists (feature 006 — the named contact persons shown
on an event, `backend/Entities/EventContact.cs`) with its own `event-contacts` UI. Reusing "Contact"
for a conversation kind would make `EventContact` (person) and `EventContact` (conversation) ambiguous
across the codebase and in every future grep. "Inquiry" names the thing precisely — a player's inquiry
to the admins — with zero collision.

## §3 — Target columns: reuse `TeamId` + add `EventId`, `RequesterUserId`

**Decision**: A `TeamInquiry` sets the existing `Conversation.TeamId` to the target team; an
`EventInquiry` sets a new `EventId` column; both set a new `RequesterUserId` (the fixed non-admin
player). No polymorphic `TargetType/TargetId`.

**Rationale**: The codebase models relations as explicit columns (`TeamId`, `PartyId`, `EventId`
elsewhere), not a polymorphic pair. Reusing `TeamId` lets the inbox keep deriving the team name/crest
through the existing `c.Team` navigation, and the existing `Restrict` FK + `TeamService.DeleteAsync`
archival hook already understand `TeamId`.

**Consequence that must be handled**: the current index
`IX_Conversations_TeamId … IsUnique().HasFilter("\"TeamId\" IS NOT NULL")` enforces *one conversation
per team*. Inquiry rows also carry `TeamId`, so the filter must be tightened to the team **chat** only:
`HasFilter("\"TeamId\" IS NOT NULL AND \"Kind\" = 2")` (2 = `Team`). See data-model R1.

**Alternatives considered**:
- *Separate `InquiryTeamId`/`InquiryEventId` columns* — rejected: duplicates a relation already
  modeled by `TeamId`, and forks the name/crest derivation and the archival hook for no gain.
- *Polymorphic `(TargetKind, TargetId)`* — rejected: inconsistent with the codebase; loses FK
  integrity and the `c.Team`/`c.Event` navigations projections rely on.

## §4 — Uniqueness per (player, target): unique filtered indexes

**Decision**: One thread per pair, enforced in the database:
- `HasIndex(TeamId, RequesterUserId).IsUnique().HasFilter("\"Kind\" = <TeamInquiry>")`
- `HasIndex(EventId, RequesterUserId).IsUnique().HasFilter("\"Kind\" = <EventInquiry>")`

The service does a check-then-create and catches `DbUpdateException` to resolve the loser of a race to
the winner — the exact pattern `EnsureDirectAsync`/`EnsureAutoAsync` already use behind `DirectPairKey`
and the team/party indexes.

**Rationale**: FR-004 ("at most one, reused") must survive two tabs sending a first message at once.
Only a DB constraint can promise that; a service-level check interleaves. This mirrors FR-008's
`DirectPairKey` design precisely.

## §5 — Creation timing: lazy, on first send

**Decision**: The thread is created on the first sent message via `SendFirstInquiryAsync`
(validate target + not-an-admin → `EnsureInquiryAsync` race-safe → `IChatMessageService.SendAsync`).
Opening the compose view persists nothing.

**Rationale**: Feature 022 established exactly this for DMs to stop empty-thread inbox pollution
(`SendFirstDirectAsync`). Admins should not get a thread in their inbox because a player *opened* a box
and typed nothing (FR-005). Reuse the shape 1:1, including returning the inbox summary + the sent
message so the client can drop the thread into the rail without a reload.

## §6 — Admin visibility without participant rows

**Decision**: Do **not** create participant rows for admins and do **not** add inquiries to
`EnsureAutoChatsForAsync`. Admin inbox visibility comes from `ChatGuard.IsMemberOf` /
`VisibleConversations` resolving the roster live.

**Rationale**: `VisibleConversations` already filters by `ChatGuard.IsMemberOf(_db, callerId)`. Once
`IsMemberOf` gains inquiry branches (member if requester **or** current admin of the target), every
current admin sees the thread automatically the instant it exists — no backfill, no sync. Participant
rows for inquiry kinds are, as with Team/Party, *state only* (mute/hide/read marker), created lazily
by `EnsureParticipantStateAsync`.

## §7 — Join cutoff (FR-019): new admin sees history from their grant

**Decision**: `ResolveJoinCutoffAsync` for inquiries returns, per viewer:
- requester → the requester's participant `JoinedDate` (≈ thread creation), or null;
- admin → their `TeamMembership.JoinedDate` (team) / `EventAdmin.AddedDate` (event).

**Rationale**: Mirrors the Team/Party cutoff logic that already implements FR-051. FR-019 states a
newly-added admin must not see messages predating their grant — the admin roster row's date is exactly
that boundary, and it is the same source `ResolveParticipantUserIds` derives membership from, so it
cannot drift. Batched variant extended the same way for the inbox/badge loops.

## §8 — Archival triggers and the snapshot, generalized to many threads

**Decision**: Generalize the existing single-conversation snapshot archival to operate over a *set* of
conversations:
- **Team deleted** (`TeamService.DeleteAsync`, hard delete, roster cascades): archive the team chat
  **and every `TeamInquiry` thread for that team**.
- **Event cancelled** (`EventService.CancelAsync`, soft status flip): archive every `EventInquiry`
  thread for that event.

Each archived thread: snapshot the derived roster (requester + current admins) into participant rows →
freeze the display name → null `TeamId`/`EventId` → set `State = Archived` — *before* any hard delete
(data-model R3a).

**Rationale**: Team delete hard-deletes `TeamMemberships`; a live inquiry that derives admin
membership from them would become unreadable, so the roster must be frozen into rows first — identical
to why the team chat is snapshotted. Event cancel does **not** delete `EventAdmin` rows, so strictly an
event inquiry could keep deriving; snapshotting anyway keeps one uniform "archived = detached,
read-only, reads from stored participants" invariant and future-proofs against event hard-delete. The
`Restrict` FKs make a forgetful delete path fail loudly.

**Note on event scope**: a past-but-not-cancelled event keeps its thread open (spec Clarification
2026-07-23, Q1) — only cancel/delete archives. No scheduled job is introduced.

## §9 — Not in scope, by decision

- **Blocking** — direct-message-only in chat (019 FR-032); no per-admin block in an inquiry (spec
  FR-016).
- **New notification/alert rows** — chat raises none (019); inquiries ride the existing unread badge
  (FR-018), keeping `ChatDoesNotTouchAlertsTests` green.
- **Leaving** — inquiries are non-leavable like Team/Party; mute/hide only (FR-017). The existing
  `LeaveAsync`/`GetDetailAsync` `IsManualGroup` gate already yields this with no change.

## §10 — Rate limiting & abuse

**Decision**: Apply the existing `RateLimitPolicies.ChatStart` policy to the send-first-inquiry
endpoints.

**Rationale**: Starting an inquiry is a new-conversation reach into admins' inboxes — the same abuse
surface `ChatStart` guards for `Start` and `SendDirect` (019 FR-049a). Reuse, don't reinvent.

## Open questions

None. All Technical Context unknowns resolved; the two spec-level clarifications were captured in
`/speckit-clarify`.
