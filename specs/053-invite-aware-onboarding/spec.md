# Feature Specification: Invite-Aware Onboarding

**Feature Branch**: `053-invite-aware-onboarding`

**Created**: 2026-09-19

**Status**: Draft

**Input**: User description: "Invite-aware onboarding — GH #324. A person who arrives through a team's shared invite link with no account yet registers, verifies their email, signs in and walks the onboarding wizard, and by then the product has forgotten they were invited: the verification email drops the returnUrl, and the onboarding team step asks them to search for a team they already chose by clicking the link. Two linked problems: (1) the invite context must survive the email-verification hop (server-carried invite token vs client-side remembered invite); (2) the onboarding team step must lead with "{inviter} invited you to join {team} — accept?" when an invite is pending (shared-link invite carried from /join, or targeted invitations already addressed to the account), with the existing team search still available underneath. Amends 029 (onboarding team step) and touches auth/email (registration + verification link)."

## Context: what this amends

Two earlier decisions are amended, neither reversed.

- **Feature 005, FR-031** promises that "an unauthenticated visitor MUST be able to sign in or
  register and return to the same invitation to act on it." That promise holds for *sign in* and
  is broken for *register*: a new account must verify its email first, the verification message
  knows nothing about the invitation, and the trail ends there. This feature makes FR-031 true for
  the register path as well.
- **Feature 029** built the onboarding team step as a search, and listed "team invitations or
  invite links in the step" as out of scope. That was right when nothing could tell the step an
  invite existed. This feature supersedes that out-of-scope line: when the product knows a person
  was invited, the step leads with the invitation and keeps the search underneath. Every other
  029 requirement stands — above all FR-017/FR-018, that no state of the team step may trap a
  player who registered thirty seconds ago. This feature strengthens that guarantee; it never
  weakens it.

Feature 004's onboarding order, skippability, and completion rule are untouched.

## The gap, concretely

A person who follows a team's shared invite link today, with no account, walks this chain:

invite link → sign in → register → **verification email** → verify → sign in → onboarding →
dashboard.

The invitation is carried carefully from the invite link through sign-in and registration, and
from sign-in through onboarding to the app — and dropped at exactly one point: the email. The
verification message is built without it, and the page it lands on links to a bare sign-in. From
then on the product has forgotten why the person came. The onboarding team step then asks them to
*find* a team, which is the one question they already answered by clicking the link, and their
only way back to the team is to dig the original link out of a chat.

Two problems, one blocking the other: the invitation has to survive the email hop before the
team step can know about it.

## Clarifications

### Session 2026-09-19

- Q: Which shape carries the invitation across the email hop — through the server, remembered on
  the account, or in the browser? → A: **Through the server.** Registration carries the
  invitation's opaque reference (its identity, never an address); the server validates its shape
  and places it in the verification message it builds; the verification page and sign-in carry it
  into onboarding through the return-destination mechanism sign-in already has. It survives
  opening the mail on another device whenever the person follows the product's own links.
  Nothing is stored on the account and no stored field is added. Remembering it on the account
  (survives any cold sign-in from a third device, at the cost of a stored reference on every
  account) and browser-only (no server change, useless across devices) were both declined.
- Q: After accepting from inside the wizard, does the player stay or leave for the team's page?
  → A: **Stay in the wizard.** The step confirms membership in place, the remaining steps still
  run, and finishing onboarding by any exit lands the player on the joined team's page instead of
  the dashboard. Leaving immediately would leave onboarding incomplete and re-enter the wizard on
  the next sign-in — a worse loop than the one being fixed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The invitation survives registration and verification (Priority: P1)

Someone opens a team's shared invite link and has no account. They are asked to sign in or
register, they register, they open the verification message, verify, and sign in. When the
onboarding wizard reaches its team step, the team that invited them is already there, waiting,
by name.

**Why this priority**: This is the blocking half. Without it the team step cannot know an
invitation exists, and the person's only route back to the team is to search their chat history
for the link. A team admin who shares a link to a beginner should be able to trust that the
beginner arrives; today they cannot.

**Independent Test**: Open a valid shared invite link while signed out, register a new account
from the sign-in prompt, verify from the emailed link in a **different browser**, sign in from
the verification page, and confirm the onboarding team step names the inviting team. Repeat with
the verification link opened in the same browser. Both must arrive.

**Acceptance Scenarios**:

1. **Given** a signed-out visitor on a valid shared invite link, **When** they choose to register,
   **Then** the registration knows which invitation they came from, and completing it does not
   lose that knowledge.
1a. **Given** a registration that began from an invitation, **When** the verification message is
   sent, **Then** its verification link carries a reference to that invitation.
2. **Given** the verification link opened in any browser or on any device, **When** the person
   verifies and reaches the "sign in" step the verification page offers, **Then** that step still
   carries the invitation.
3. **Given** a sign-in that carries an invitation, by a verified account that has not completed
   onboarding, **When** sign-in succeeds, **Then** the wizard opens with the invitation known to
   it — the person is not sent to the invite page ahead of onboarding, and not to a wizard that
   has forgotten the invite.
4. **Given** a sign-in that carries an invitation, by an account that has already completed
   onboarding, **When** sign-in succeeds, **Then** they land on the invite page for that invitation
   exactly as they do today — nothing about the existing-member path changes.
5. **Given** a registration that began without an invitation, **When** the flow completes,
   **Then** nothing about it differs from today: no reference, no changed message, no changed page,
   the plain team step.
6. **Given** a verification message that carries an invitation, **When** the person asks for it to
   be sent again from a screen that still knows the invitation, **Then** the re-sent message
   carries it too.

---

### User Story 2 - The team step leads with the invitation (Priority: P1)

A brand-new player reaches the onboarding team step and, instead of an empty search, reads:
**"{inviter} invited you to join {team}"** — with the team's city and player count, and a single
clear Accept. One press and they are on the team; the step says so plainly. The team search is
still there underneath, offered as "or look for another team", because belonging to several teams
is allowed and joining the inviting team and browsing others are not mutually exclusive.

**Why this priority**: Equal to Story 1 because the two are the same story told twice: carrying
the invitation is pointless if the wizard then asks the person to search anyway, and a wizard that
leads with the invitation is useless if the invitation never arrives. Together they are the whole
deliverable.

**Independent Test**: Reach the team step with a usable invitation known to the wizard and confirm
the inviting team is offered by name with an Accept; accept and confirm the account is now a
member of that team and the step confirms it; confirm the team search is still reachable on the
same step.

**Acceptance Scenarios**:

1. **Given** the team step opens with a usable shared-link invitation known to the wizard, **When**
   it renders, **Then** it leads with the inviting team — inviter's name, team name, city where the
   team has one, player count — and an explicit Accept naming the team.
2. **Given** the invitation card, **When** the player presses Accept, **Then** they become a member
   of that team, and the step confirms in plain words, in place, that they are now on the team —
   membership here is immediate, not pending, and the wording must not borrow 029's
   "an admin will let you in".
3. **Given** the team step, **When** the invitation card is shown, **Then** the existing team
   search remains available on the same step, presented as a secondary path ("or look for another
   team"), not removed and not hidden behind a leave-the-wizard action.
4. **Given** the player has accepted, **When** they continue, go Back, and return to the step,
   **Then** the team is shown as joined and cannot be accepted twice from the wizard.
4a. **Given** the player has accepted, **When** they finish onboarding (or dismiss it from a
   later step), **Then** they land on the joined team's page rather than the dashboard.
5. **Given** the player does *not* accept, **When** they continue, skip, choose "I'm not on a team
   yet", or go Back, **Then** nothing is sent and nothing about the invitation changes — the
   invitation stays usable for them later exactly as it was.
6. **Given** the team step with no invitation known to the wizard and none addressed to the
   account, **When** it renders, **Then** it is the 029 step, unchanged.

---

### User Story 3 - Invitations already addressed to the account are offered too (Priority: P2)

A player who has an account but has not yet completed onboarding — they registered, verified,
and a team admin invited them by name before they signed in for the first time — reaches the
team step and sees those invitations listed, each with Accept and Decline, without needing to
have followed any link at all.

**Why this priority**: Cheaper than Story 2 and less common: it reuses the invitations list that
the "My team" home already shows, and only players who were invited between registering and
first sign-in reach it. It completes the picture — *every* invitation the product knows about is
offered at the moment the wizard asks about teams — but Stories 1 and 2 stand without it.

**Independent Test**: Create an account, verify it, have a team admin send it a targeted
invitation, then sign in for the first time and confirm the team step lists that invitation with
Accept and Decline; accept and confirm membership.

**Acceptance Scenarios**:

1. **Given** the account has usable invitations addressed to it, **When** the team step renders,
   **Then** each is listed with the team's name, city where present, player count, inviter, and
   Accept/Decline actions.
2. **Given** both a carried shared-link invitation and addressed invitations, **When** the step
   renders, **Then** the carried one leads and the addressed ones follow; none is shown twice.
3. **Given** an addressed invitation, **When** the player declines it, **Then** it disappears from
   the step and they do not join; **When** they accept it, **Then** they join and the step says so.
4. **Given** the invitations list cannot be loaded, **When** the step renders, **Then** it falls back
   quietly to whatever else it has (the carried invitation, or the plain search) — the failure is
   never shown as a blocker and never disables Continue.

---

### User Story 4 - Nothing about the invitation can trap the player (Priority: P1)

Whatever the invitation turns out to be — expired while the verification mail sat unread,
revoked by an admin, pointing at a team that no longer exists, tampered with — the player is
told quietly and the step falls back to the ordinary search. Accept can fail and be retried; it
never blocks Continue, "I'm not on a team yet", or Back.

**Why this priority**: P1 because it guards the first screen after registration, the same reason
029 rated its own escape guarantee P1. An invitation that arrived through an email is more likely
than most things to be stale by the time it is read.

**Independent Test**: Reach the team step with (a) an expired invitation, (b) a revoked one,
(c) a garbage reference, and (d) a valid one whose Accept is made to fail; in every case confirm
the search still works and all three ways out of the step work, and in (d) confirm Accept can be
pressed again.

**Acceptance Scenarios**:

1. **Given** the carried invitation is expired, **When** the step renders, **Then** a short plain
   note says the invite has expired and to ask a team admin for a new one, and the ordinary search
   is offered beneath it.
2. **Given** the carried invitation is revoked, already consumed, or for a team that no longer
   exists, **When** the step renders, **Then** a short plain note says the invite is no longer
   valid, and the ordinary search is offered beneath it.
3. **Given** the carried reference is malformed or refers to nothing, **When** the step renders,
   **Then** it is the plain 029 step — no note, no error, and nothing the player could read as
   their fault.
4. **Given** the invitation is for a team the player already belongs to, **When** the step renders,
   **Then** it says they are already on that team, offers no Accept for it, and offers the search.
5. **Given** Accept fails, **When** the failure is shown, **Then** the player is told the join did
   not go through, may press Accept again, and Continue, "I'm not on a team yet", and Back all
   still work.
6. **Given** any state of the invitation — loading, usable, stale, failed — **When** the player
   presses Continue, **Then** the step advances instantly with no network call, exactly as 029
   FR-018 requires.

---

### Edge Cases

- **The verification mail is opened days later**: invitations live seven days. A link invitation
  that expired in the meantime is reported as expired on the team step (US4-1), never silently
  dropped — the person should know to ask for a fresh one rather than wonder.
- **The person verifies on one device and later signs in from another without following the
  verification page's link**: the invitation is not known to that sign-in. The wizard shows the
  plain step (or any invitations addressed to the account). Accepted limit of the server-carried
  shape; see Clarifications.
- **The person already has an account and follows the link signed out**: sign-in carries the
  invitation as it does today; if they have not finished onboarding, the wizard now leads with it;
  if they have, they land on the invite page as before.
- **The person registers, then dismisses onboarding on the Welcome screen**: they leave for the
  invite page (the carried destination), exactly as today's return handling does. Nothing lost.
- **The shared link is rotated by an admin between the click and the team step**: the old token
  is revoked — reported as no longer valid (US4-2).
- **The same invitation is offered twice** — carried from the link and also addressed to the
  account: it is shown once (US3-2).
- **Several invitations, one accepted**: the others remain listed and actionable; belonging to
  several teams is allowed (005).
- **The reference that survives the email hop is edited by hand**: it is an opaque invite
  reference, not an address. A bad one can only ever produce the plain step; it can never send
  the person anywhere outside the app or to any page other than the wizard's own team step.
- **Registration is refused (handle taken, terms changed)**: the invitation stays with the form
  and is still carried when the corrected registration succeeds.
- **The verification message is re-sent from the sign-in page after a failed sign-in**: where the
  sign-in page still knows the invitation it is carried on the re-sent message (US1-6); where it
  does not (the person typed the sign-in address directly), the re-sent message is plain.
- **The invite is for a team the person is *pending* on** (they asked to join via search earlier
  in the same step, then accepted the invite): accepting by invitation makes them a member; the
  earlier request is moot. No special handling beyond honest wording.

## Requirements *(mandatory)*

### Functional Requirements

#### Carrying the invitation across the email hop

- **FR-001**: When a person registers from a screen that knows which shared-link invitation they
  came from, the registration MUST carry an opaque reference to that invitation — the
  invitation's own identity, never an address or path — and the verification message the system
  sends MUST include that reference in its verification link, so the invitation survives email
  verification on any device and is known to the wizard's team step when that account signs in
  for the first time.
- **FR-001a**: The system MUST NOT record the reference on the account and MUST NOT add any
  stored field for it; the reference lives only in the artefacts that carry it (the registration
  input, the verification message, the pages that follow it).
- **FR-002**: Whatever is carried MUST be the invitation's own opaque identity, never an address
  or path. Every place that turns it into a destination MUST derive that destination itself (the
  invitation page, or the wizard's team step); the system MUST NOT carry, and MUST NOT follow, an
  arbitrary destination through the email hop.
- **FR-003**: Any reference handed to the server MUST be validated for shape before it is used or
  placed anywhere. A reference that fails validation MUST be dropped silently — the registration
  MUST still succeed and the verification message MUST still be sent, without it. Registration
  MUST never be refused because of the reference.
- **FR-004**: The reference MUST NOT change what registration otherwise does or reveals: the
  neutral "if that email can be registered…" response, the enumeration-safe handling of an
  existing address, and every existing validation MUST behave identically with or without it.
- **FR-005**: The invitation itself MUST NOT be created, consumed, accepted, or otherwise changed
  by registration or verification. It is acted on only by an explicit Accept or Decline by the
  signed-in player.
- **FR-006**: After a successful verification, the "sign in" step the verification page offers
  MUST carry the invitation, and sign-in MUST carry it into onboarding for an account that has
  not completed onboarding, or to the invitation page for one that has — using the same carrying
  mechanism sign-in already uses for a return destination today.
- **FR-007**: A verification message re-sent from a screen that still knows the invitation MUST
  carry it as the original did.
- **FR-008**: A registration or verification that began without an invitation MUST be unchanged
  in behaviour and wording: no reference, no altered message, no altered page.

#### The team step, when an invitation is known

- **FR-009**: When the wizard's team step opens with a usable shared-link invitation known to it,
  the step MUST lead with that invitation: the inviter's name, the team's name, its city where it
  has one, its player count, and an explicit Accept action naming the team. The card MUST use the
  same public team information the invitation page shows and nothing more (005 FR-027).
- **FR-010**: Accept MUST make the signed-in player a member of the team using the same
  capability the invitation page uses, and the step MUST confirm the membership in place, in
  words that make it clear membership is immediate — it MUST NOT reuse 029's pending wording.
- **FR-011**: Accept MUST be its own deliberate press. Continue, Skip, "I'm not on a team yet",
  and Back MUST never accept, decline, or otherwise touch the invitation (029 FR-012 extended).
- **FR-012**: The step MUST also list usable invitations addressed to the signed-in account, each
  with the team's name, city where present, player count, inviter, and Accept and Decline actions;
  a carried shared-link invitation leads, addressed ones follow, and no invitation appears twice.
- **FR-013**: Declining an addressed invitation MUST remove it from the step and MUST NOT make the
  player a member. A carried shared-link invitation is not declinable from the step — it is
  simply not accepted.
- **FR-014**: After an Accept, the step MUST show the team as joined and MUST NOT offer Accept for
  it again within the wizard, including after Back and return.
- **FR-015**: The existing 029 team search MUST remain on the step, beneath the invitation(s),
  presented as a secondary path to *another* team. It MUST behave exactly as 029 specifies,
  including asking to join a different team while an invitation is shown.
- **FR-016**: Accepting from inside the wizard MUST keep the player in the wizard: the step
  confirms membership in place and the remaining steps still run. Once the player has joined a
  team through the step, finishing onboarding — by the Done screen or by dismissing from any
  later step — MUST land them on that team's page rather than the dashboard. Where the player
  joined several teams during the step, the first joined team is the destination.
- **FR-017**: When neither a carried invitation nor an addressed invitation exists, the step MUST
  be the 029 step, unchanged in layout, copy, and behaviour.

#### Stale, invalid, and failed invitations

- **FR-018**: A carried invitation that is expired MUST be reported on the team step with a short,
  plain note that the invite has expired and that a team admin can issue a new one; the ordinary
  search MUST be offered beneath it.
- **FR-019**: A carried invitation that is revoked, already consumed, or for a team that no longer
  exists MUST be reported with a short, plain note that the invite is no longer valid; the
  ordinary search MUST be offered beneath it. The note MUST NOT distinguish *why* beyond
  "expired" versus "no longer valid".
- **FR-020**: A carried reference that is malformed or refers to nothing MUST produce the plain
  029 step with no note, no error, and no request beyond the one that established it refers to
  nothing.
- **FR-021**: A carried or addressed invitation for a team the player already belongs to MUST be
  reported as such, MUST NOT offer Accept, and MUST NOT be reported as an error.
- **FR-022**: A failed Accept MUST be reported plainly, MUST be retryable by another press, MUST
  NOT be retried automatically, and MUST NOT disable or remove Continue, "I'm not on a team yet",
  or Back.
- **FR-023**: No state of the invitation — loading, usable, stale, accepted, failed — may disable
  or remove any way out of the step (029 FR-017), and advancing MUST remain instantaneous with no
  network call (029 FR-018). Loading the invitation preview or the addressed list MUST NOT delay
  the step's appearance beyond the standard quiet loading treatment.
- **FR-024**: No note or message on the step may surface system internals, raw failure detail,
  or the reference itself.

#### Security & authorization

- **FR-025**: Whether an invitation is usable, whether the player may accept it, and whether
  they are already a member MUST be decided server-side on every action; nothing the wizard holds
  is a security boundary.
- **FR-026**: The carried reference MUST NOT grant anything by itself: presenting it MUST only
  ever lead to the same preview any holder of the invite link can already see, and accepting
  MUST still require the signed-in account to be one the invitation admits.
- **FR-027**: The carried reference MUST NOT be recorded by the product's analytics beyond what
  the invite link's own address is recorded as today, and MUST NOT appear in logs beyond what
  invitation handling already logs.

### Key Entities *(include if feature involves data)*

- **Team Invitation (existing, feature 005)**: A shared-link invitation (reusable by distinct
  people until it expires or is revoked) or a targeted invitation (bound to one account, consumed
  on accept or decline). Read and acted on here; never created or changed by registration or
  verification. Its seven-day life is what makes stale invitations a first-class state.
- **Carried invitation reference (new, transient)**: An opaque identity for one shared-link
  invitation, carried from the invite page through registration, verification, sign-in, and into
  the wizard's team step. It is never a destination in itself, and never trusted for anything
  the server would not grant any holder of the invite link.
- **Player Profile / Onboarding state (existing, feature 004)**: Untouched. Accepting an
  invitation from the wizard produces a team membership, not a profile change; onboarding
  completion is recorded exactly as before.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person who opens a shared invite link with no account and follows every link the
  product offers — register, verify, sign in — reaches the onboarding team step with the inviting
  team offered by name in 100% of cases, on the same device or a different one, without ever
  re-opening the original invite link.
- **SC-001a**: A player who accepts an invitation in the wizard and finishes onboarding lands on
  that team's page in 100% of cases, by whichever exit.
- **SC-002**: From the team step, joining the inviting team takes one press, and the confirmation
  names the team and states membership as immediate in 100% of cases.
- **SC-003**: Registration, verification, and onboarding that began without an invitation are
  indistinguishable from today's, in every message, page, and outcome.
- **SC-004**: In 100% of stale cases — expired, revoked, consumed, deleted team, malformed
  reference — the team search still works and every way out of the step still works.
- **SC-005**: Advancing past the team step issues zero network requests in every state of the
  invitation (029 SC-009 preserved).
- **SC-006**: Nothing that crosses the email hop is an arbitrary destination; a hand-edited
  reference can only ever produce the plain team step, never a page outside the wizard or the
  product.
- **SC-007**: An account with invitations addressed to it sees every usable one on the team step
  on its first sign-in, with none shown twice.

## Assumptions

- **Server-carried, never stored.** The reference rides in the verification message the server
  already sends, which is the only artefact that reliably reaches the person's other device.
  Storing a pending invitation on the account was considered and declined (see Clarifications):
  it would survive an arbitrary cold sign-in from a third device, at the cost of a stored field
  on every account for a case that following the product's own links already covers. That case
  is an accepted limit and can be revisited without undoing anything here.
- **Stay in the wizard after accepting.** Leaving for the team page mid-wizard would leave
  onboarding incomplete and the next sign-in would re-enter it. The wizard instead remembers the
  joined team and makes it the exit destination.
- **The reference is the invitation's identity, not a path.** Everything that turns it into a
  destination builds the destination itself. This is what makes the "an emailed link that
  redirects" concern moot: the link cannot be pointed anywhere, because it does not contain a
  where.
- **Only the shared-link path needs carrying.** Targeted invitations are addressed to existing
  accounts, so a person with no account can only have arrived by shared link; and a targeted
  invitation is already discoverable on the account (023) without any carrying at all.
- **Membership by invitation is immediate**, unlike 029's join *request*, so the confirmation
  wording is different by design and must not be shared with the pending copy.
- **The search stays.** Belonging to several teams is allowed (005); a person invited to one team
  may still want to find their second, and a beginner invited to a mixed team may also want their
  city team. Leading with the invitation is a change of emphasis, not a removal.
- **Stale invitations are named, garbage is not.** An expired or revoked invitation is something
  the person can act on (ask for a new one); a malformed reference is something they can do
  nothing about and would only worry over.
- **No new server capability beyond carrying the reference.** Preview, accept, decline, the
  addressed-invitations list, and membership all exist. What is new is the carrying and the
  wizard's use of the existing capabilities.
- **Reuses the existing invitation card treatment** from the invite page and the "My team" home,
  and the shared loading primitive, so the step is visually of a piece with the rest of the app.
- **Out of scope**: storing a pending invitation on the account; invitations for events or trainings; inviting others from the wizard;
  changing what the invite page itself shows; changing the "My team" home; a "pending
  invitations" indicator elsewhere; changing the seven-day life of invitations; any change to the
  team creation wizard (052).
