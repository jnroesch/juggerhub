# Feature Specification: Self-Service Account Deletion

**Feature Branch**: `037-account-deletion`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "Self-service account deletion (GDPR Art. 17 erasure). Split out of GH #105 — that issue covers both export and deletion; this feature is DELETION ONLY, export (Art. 15/20) is deferred to a follow-up issue. A signed-in member must be able to delete their own account from the app, without writing to the contact address."

## Context

GitHub issue [#105](https://github.com/jnroesch/juggerhub/issues/105) raised export *and* deletion together. This feature is **deletion only**; export (Art. 15 access / Art. 20 portability) is deferred to its own issue and is explicitly out of scope here.

The gap is real and already documented against us: feature 036's privacy policy tells readers, in three languages, that there is no self-service delete control and that erasure happens by hand if you write in. That was the honest thing to say at the time. This feature is what lets that sentence be replaced.

The difficulty is not the button. It is that **"delete" already means something else in this codebase**, and that the account is referenced from roughly thirty other tables with deliberate, load-bearing constraints:

- A **ban** (feature 013) is a *soft*-delete: `AccountStatus.Banned` plus a global query filter that hides the profile everywhere. The banned row is retained on purpose, and **its unique email address *is* the re-registration denylist** — there is no separate denylist table. Erasing an email therefore erases a moderation control.
- Chat is **snapshotted, not deleted**: when a team is deleted or an event cancelled, the derived roster is materialised into real participant rows so the history stays readable. Erasure has to decide what a departing member's messages look like inside other people's history.
- Many references to the account are **`Restrict`, not `Cascade`**, by explicit design — authored news posts, invitations sent and received, awards granted, admin action records, blocks, chat messages, party and training ownership. A row-level delete of the account is not merely risky today; the database refuses it.
- Some references are already `SetNull` *in anticipation of account removal* (a notification keeps its text when the actor is gone), and chat's `Restrict` carries a comment stating the intended end state: history preserved, **sender rendered as a neutral placeholder**.
- A team **always keeps at least one admin** (last-admin guard). A departing sole admin is a blocking condition, not an edge case.

So the shape of the work is: decide what erasure means for each category of data, make the shared surfaces survive the absence of an account, and only then put a control in front of it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A member deletes their own account (Priority: P1)

A signed-in member decides to leave JuggerHub. They find a delete-account control in their account settings, prove they are who they say they are, confirm deliberately, and their account is gone. They are signed out and told plainly that it is done and that it cannot be undone. They never have to email anyone.

**Why this priority**: This is the entire point of the feature and the sentence in the privacy policy that this work exists to replace. Without it, nothing else here has value.

**Independent Test**: Sign in as a member with a profile, a team membership, notifications, and chat history. Delete the account through the UI. Verify the member is signed out, cannot sign in again, and that their name, handle, email, and photo are absent from every surface that previously showed them.

**Acceptance Scenarios**:

1. **Given** a signed-in member with no blocking obligations, **When** they open account settings, **Then** a clearly-labelled account deletion control is present and reachable without searching.
2. **Given** the member has opened the deletion flow, **When** they attempt to confirm without re-proving their identity, **Then** deletion does not proceed.
3. **Given** the member has re-proved their identity and given a deliberate confirmation, **When** deletion runs, **Then** it either completes in full or leaves the account exactly as it was — never partially applied.
4. **Given** deletion completed, **When** the member tries to sign in with their former credentials, **Then** sign-in fails and the failure discloses nothing about whether that account ever existed.
5. **Given** deletion completed, **When** the member searches the platform for their own former handle or profile link, **Then** nothing is found and the former profile URL does not resolve to their data.

---

### User Story 2 - A member knows what will happen, and is stopped when they shouldn't proceed (Priority: P2)

Before confirming, the member is told what is erased, what survives and why, and that the action is permanent. If they hold an obligation the platform cannot resolve on their behalf — being the only admin of a team — they are told so, told which ones, and told what to do about it, in one message rather than one refusal at a time.

**Why this priority**: An irreversible action taken without understanding is the failure mode that generates the support load this feature is meant to remove. The last-admin guard is a structural constraint that already exists elsewhere in the product; deletion must not be the one path that violates it or, worse, fails halfway through because of it.

**Independent Test**: As the sole admin of two teams, open the deletion flow. Verify both teams are named in a single blocking message with an actionable remedy. Hand over one team, retry, verify the remaining team is still named. Hand over the second, retry, verify the flow proceeds.

**Acceptance Scenarios**:

1. **Given** a member opens the deletion flow, **When** the confirmation is presented, **Then** it states what is erased, what is retained and on what basis, and that the action cannot be reversed.
2. **Given** a member is the only admin of one or more teams, **When** they open the deletion flow, **Then** deletion is refused and **every** blocking item is named at once.
3. **Given** a blocking condition was reported, **When** the member resolves it and returns, **Then** the flow reflects the current state rather than a cached refusal.
4. **Given** a member is midway through the flow, **When** they abandon or cancel it, **Then** nothing about their account has changed.
5. **Given** deletion has been confirmed, **When** the platform notifies the member, **Then** the notification reaches the address on file — sent before that address stops existing.

---

### User Story 3 - The platform stays coherent for everyone who remains (Priority: P3)

A member who shared a team, an event, or a conversation with the departing member opens those surfaces afterwards. Nothing is broken. Where the departed member used to appear, a neutral, non-identifying placeholder appears instead. Conversations still read in order. No page errors, no blank rows, no half-rendered names.

**Why this priority**: Erasure that leaves other people's screens broken is not shippable, and the shared surfaces are where this feature's real risk lives. It is separated from P1 because it is verified from a *different account's* point of view and can be tested and hardened on its own.

**Independent Test**: With a second account, open a shared conversation, a team roster, an event participant list, and a news feed containing the departed member's traces. Verify each renders without error and shows the neutral placeholder.

**Acceptance Scenarios**:

1. **Given** a conversation the departed member took part in, **When** another participant opens it, **Then** the history renders in the correct order and the departed member is shown as a neutral placeholder that identifies no one.
2. **Given** a team roster, event participant list, or party roster the departed member appeared on, **When** another member views it, **Then** the list renders without error and no longer presents the departed member as a current member.
3. **Given** a notification whose actor was the departed member, **When** the recipient opens their notification list, **Then** the notification still renders and identifies no one.
4. **Given** any surface that previously displayed the departed member, **When** it is opened after deletion, **Then** it does not error and does not display their name, handle, email, or photo.

---

### User Story 4 - The privacy policy stops describing a manual route (Priority: P4)

The rights section of the privacy policy points members at the control in their settings instead of telling them to write in. The German text — which is the authoritative one — and the English and Spanish informational versions all change together.

**Why this priority**: It is the reason issue #105 exists in the shape it does, but it is a documentation change that depends on P1 being live. Shipping it early would make the policy describe a control that does not exist yet — precisely the failure 036 refused to commit.

**Independent Test**: Read the rights section in all three languages and confirm each describes the self-service control, that no language still describes the manual-only route as the sole option, and that the three versions agree on substance.

**Acceptance Scenarios**:

1. **Given** the deletion control is live, **When** a reader opens the privacy policy in German, **Then** the rights section describes the in-product control.
2. **Given** the policy has been updated, **When** the English and Spanish versions are compared against the German, **Then** they describe the same route and the same retained categories.
3. **Given** the policy has been updated, **When** the manual contact route is described, **Then** it remains available as a fallback rather than being removed.

---

### Edge Cases

- **Sole admin of a team.** Blocked, with the teams named. The last-admin guard is structural and deletion must respect it, not bypass it.
- **Sole admin of an event, or creator/admin of an active party.** The same class of problem, on surfaces where the guard may not exist today. The feature must define the outcome rather than discover it at runtime.
- **A platform administrator deletes their own account.** The admin role is synchronised from configuration at startup (feature 013), so an admin account can be recreated by the next boot unless the configuration is changed too. Deleting the last administrator must not lock the platform out of its own moderation tooling.
- **An account under moderation.** Suspended and banned accounts cannot sign in, so they cannot reach the control — but the rule must be enforced server-side, not left as a side effect of the sign-in gate. Deletion must never become a way to shed a ban.
- **The member owns content other people depend on** — a team news post, an event they administer alongside others, an award they granted to someone else. Removing the account must not remove other people's records.
- **Deletion is requested twice**, or requested from two sessions at once. The second request must be harmless.
- **Deletion fails partway** — a network fault, a constraint violation, a storage object that cannot be reached. The account must be left intact and the member told it did not happen, rather than left in a half-erased state.
- **Stored image objects.** A profile photo lives outside the database once feature 035 lands. Erasing the row that points at it does not erase the image; the object must be reclaimed too, and a failure to reclaim must not silently pass as success.
- **Active sessions elsewhere.** A session on another device must not keep working after deletion.
- **Pending invitations the member sent**, and invitations sent *to* them. Both reference the account and must resolve to something sensible rather than dangling.
- **The member returns later** wanting their account back. There is nothing to restore; the flow must have said so before it ran. They may register again with the same address (FR-031), but it is a new account — the platform must not link the two or resurrect anything.
- **The member's own words identify them.** A retained message containing their name, phone number, or address survives under FR-024. This is the known, disclosed limit of the erasure, not a defect — but it must be disclosed *before* confirmation, not explained afterwards.
- **A conversation whose only other participant deletes their account**, leaving a thread with one live member.
- **An archived conversation snapshot** that already froze roster and display names. The frozen copy is a second place the member's identity can persist, and it is detached from the live roster that would otherwise have led anyone to it.

## Requirements *(mandatory)*

### Functional Requirements

#### Initiating the deletion

- **FR-001**: A signed-in member MUST be able to initiate deletion of **their own** account from account settings, without contacting anyone.
- **FR-002**: A member MUST NOT be able to initiate deletion of any account other than their own. This MUST be enforced server-side.
- **FR-003**: The system MUST require the member to re-prove their identity (re-enter their password) immediately before deletion proceeds, so that an unattended session cannot be used to destroy an account.
- **FR-004**: The system MUST require a deliberate confirmation that cannot be given by a single accidental click.
- **FR-005**: An account that is suspended or banned MUST NOT be deletable through this flow, enforced server-side rather than relying on the sign-in gate. Deletion MUST NOT be usable to escape moderation.

#### Telling the member what will happen

- **FR-006**: Before confirmation, the system MUST tell the member, in plain language: what is erased, what is retained and on what lawful basis, and that the action is permanent and cannot be undone.
- **FR-007**: The disclosure MUST describe only what the product actually does. It MUST NOT describe a restore path, a grace-period cancellation, or a retention behaviour that is not implemented.
- **FR-008**: The disclosure MUST be available in all three supported interface languages, consistent with the privacy policy's stated categories.

#### Blocking conditions

- **FR-009**: The system MUST refuse deletion while the member is the only administrator of one or more teams, and MUST name every such team.
- **FR-010**: The system MUST define and enforce the outcome for a member who is the sole administrator of an event, or the creator or sole administrator of an active party, rather than leaving it to a database constraint to decide at runtime.
- **FR-011**: A refusal MUST report **all** blocking items in a single response, so the member can resolve them in one pass rather than discovering them one at a time.
- **FR-012**: A refusal MUST state what the member can do to resolve each blocking item.
- **FR-013**: Blocking conditions MUST be re-evaluated at the moment of confirmation, not only when the flow was opened.

#### What is erased

- **FR-014**: The system MUST erase or irreversibly anonymise the member's identifying and contact data: display name, handle, email address, credentials, and all free-text profile fields.
- **FR-015**: The system MUST remove the member's profile photo, including any stored image object held outside the database, and MUST NOT report deletion as successful if the object could not be reclaimed.
- **FR-016**: The system MUST terminate all of the member's sessions on all devices, and MUST remove all stored session records including the per-session originating IP address retained with them.
- **FR-017**: The system MUST remove records that exist solely to serve the member: notifications addressed to them, notification preferences, and interface preferences.
- **FR-018**: The system MUST remove the member's participation records: team memberships, event signups, party memberships, training responses, marketplace listings and requests, and pending join requests.
- **FR-019**: The system MUST remove pending invitations the member sent and pending invitations addressed to them.
- **FR-020**: The system MUST remove blocks the member created and blocks created against them, since the account they related to no longer exists.

#### What survives, and why

- **FR-021**: Records that belong to **other** members MUST survive the deletion — awards granted to other people, decisions recorded about other people's requests, content other members authored.
- **FR-022**: The system MUST retain the append-only administrative action log, which records moderation history and must not vanish with an account row. Its basis and retention period are stated here rather than left open:
  - **What it is after erasure.** Each entry names an acting administrator and an affected account. Once the affected account is neutralised, the entry no longer identifies the departed member — but it still identifies **the administrator who acted**. The log is therefore retained principally as a record of *administrator conduct*, which is another person's data and survives under FR-021. It is not retained as a history of the departed member, and MUST NOT be used or presented as one.
  - **Basis.** Legitimate interest in the accountability of moderation decisions: an administrator's suspend, ban, or reinstate must remain auditable after the affected account is gone, or the platform cannot answer for its own moderation.
  - **Retention period.** Retained for as long as the platform operates a moderation function. This states the *criterion* rather than inventing a fixed period, because no automated retention process exists anywhere in the platform (see Assumptions) and a stated period nothing enforces would be a claim the product does not honour.
  - **Consistency with FR-023.** Retaining the log does not create a re-identification route: the entry's reference to the departed member resolves to a neutralised account like every other surviving reference.
- **FR-023**: Every surviving record that referenced the deleted account MUST resolve to a neutral, non-identifying placeholder. It MUST NOT be possible to recover the member's identity from a surviving record.
- **FR-024**: Content the member authored in shared spaces — chat messages and team/event/party news posts — MUST be **retained verbatim and re-attributed to a neutral placeholder author**. The text is not cleared; the authorship is severed. This preserves conversations and team records that other participants also have an interest in, and matches the end state the chat data model already anticipates.
- **FR-025**: Because FR-024 retains the member's own words, the privacy policy and the pre-confirmation disclosure MUST both say so plainly: messages and posts stay, attributed to no one. A member MUST NOT be able to read the disclosure and conclude their messages will disappear.
- **FR-026**: Retained content MUST NOT be re-attributable. No surviving field, link, or identifier on a retained message or post may be used to recover who wrote it.
- **FR-027**: Where a member's own words *contain* identifying detail — their name, a phone number, an address typed into a message — that text survives under FR-024. The disclosure MUST make this consequence visible, since it is the one place where erasure is genuinely incomplete and the member is the only person who knows what they wrote.
- **FR-028**: Archived conversation snapshots MUST be covered by FR-024 and FR-026 identically. The frozen roster and frozen display names in a snapshot MUST NOT become a second, unreached home for the member's identity.

#### Permanence and re-registration

- **FR-029**: Deletion MUST be irreversible once complete. There MUST be no restore path and none MUST be implied.
- **FR-030**: The system MUST NOT reuse the word "delete" for both this action and a ban in any member-facing text, in any supported language, since the two mean different things in this product.
- **FR-031**: After deletion, the former email address MUST be free to register a new account. Nothing derived from the address is retained to bar it. A member who returns starts a genuinely new account with no link to the old one, and the system MUST NOT surface any connection between the two.
- **FR-032**: A **ban** MUST continue to bar re-registration with the same address, and a **self-deletion** MUST NOT. These two outcomes are deliberately opposite and the distinction is a requirement, not an implementation artefact:
  - a banned account is *retained*, so a registration attempt with that address still finds it and is refused;
  - a deleted account has *released* the address, so a registration attempt finds nothing and proceeds normally.
- **FR-033**: FR-031 MUST NOT weaken feature 013's moderation controls. Because FR-005 refuses deletion to suspended and banned accounts, an account already under moderation cannot use this flow to free its own address.
- **FR-034**: Re-registration with a released address MUST actually succeed. It MUST NOT fail for any reason arising from the deleted account's residual record, and a failure MUST NOT be reported to the member as success. **Every** uniqueness-constrained identifier retained on the neutralised account — not only the email address — MUST be released or made non-colliding, or the release required by FR-031 is incomplete.
- **FR-035**: A returning member's new account MUST NOT inherit, or be linked to, anything from the deleted one: no content, no history, no memberships, and no re-attribution of retained messages or posts.

#### Timing and completion

- **FR-036**: Erasure MUST be **immediate on confirmation**. There is no cooling-off window, no scheduled job, and no deleted-but-recoverable state. When the confirmation succeeds, the erasure has already happened.
- **FR-037**: Because there is no grace period, the safeguards in FR-003 and FR-004 — re-authentication and deliberate confirmation — are the *only* protection against a regretted action, and MUST be treated as load-bearing rather than as ceremony.
- **FR-038**: Deletion MUST be atomic from the member's point of view: it either completes entirely or leaves the account exactly as it was. A partially deleted account MUST NOT be an observable state.
- **FR-039**: A repeated or concurrent deletion request MUST be harmless and MUST NOT produce a different outcome from a single request.
- **FR-040**: The system MUST notify the member at the address on file that the deletion happened, sent while that address still exists.
- **FR-041**: The system MUST record that a deletion occurred, in a form that demonstrates the obligation was met without retaining who it was about.
- **FR-042**: A failure during deletion MUST tell the member it did not happen, MUST NOT expose internal detail, and MUST leave a record an operator can act on.

#### Documentation

- **FR-043**: The privacy policy's rights section MUST be updated to describe the in-product control, in German (authoritative), English, and Spanish, with the manual contact route retained as a fallback.
- **FR-044**: The policy MUST state that erasure is immediate, that authored messages and posts are retained under a neutral author, and that a former email address may be used to register again. These are the three member-visible consequences of the decisions recorded in *Clarifications*.
- **FR-045**: The three language versions MUST agree on substance, in particular on the list of retained categories.

### Key Entities

- **Account**: the member's identity and credentials. Carries a platform status (active, suspended, banned) whose *banned* state is already a soft delete, and whose unique email currently doubles as the re-registration denylist. This feature introduces a fourth outcome — genuine erasure — that must not be confused with any of the three.
- **Player profile and its attachments**: display data, city, equipment preferences, and a profile photo whose bytes live outside the database. Hidden today by a filter keyed to *banned* status; that filter is not an erasure mechanism and cannot serve as one.
- **Participation records**: team memberships, event signups, party memberships, training responses, marketplace listings and requests, join requests. Owned solely by the member; removable with them.
- **Authored content in shared spaces**: chat messages, team/event/party news posts. Visible to others and positioned within other people's history. **Retained verbatim with authorship severed** (FR-024) — the one category this feature deliberately does not erase.
- **Archived conversation snapshot**: a frozen copy of a conversation's roster and display names, created when a team is deleted or an event cancelled. A second place a member's identity persists, and easy to miss precisely because it was already detached from the live roster.
- **Administrative action record**: append-only moderation history referencing both the acting administrator and the affected member. Deliberately retained across account removal.
- **Records about other members**: awards granted, decisions made on others' requests, invitations issued. These are other people's data and survive.
- **Deletion record**: evidence that an erasure was performed, which must not itself identify the person erased.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member can complete account deletion in under 2 minutes from opening settings, with no email to anyone and no operator involvement.
- **SC-002**: 100% of deletion attempts end in one of exactly two states — fully deleted, or unchanged. No attempt produces a partially deleted account.
- **SC-003**: After deletion, the platform displays the member's name, handle, email address, or photo on **zero** surfaces, including archived conversation history and search. Text the member typed into their own messages is out of scope of this criterion and is covered by SC-005.
- **SC-004**: 100% of surfaces that previously displayed the member render without error afterwards, showing a neutral placeholder.
- **SC-005**: Given a retained message or news post, no sequence of actions available to any other member — including a platform administrator using existing tooling — recovers who wrote it.
- **SC-006**: Erasure is observable as complete the moment the confirmation returns; no surface shows the account in a pending, scheduled, or partially-removed state at any point.
- **SC-007**: A member with blocking obligations sees **all** of them named in a single message, and can act on each without a second refusal cycle.
- **SC-008**: A deleted account's former credentials fail sign-in, and the failure is indistinguishable from any other failed sign-in.
- **SC-009**: The number of manual, by-hand erasure requests handled through the contact address falls to zero for members able to sign in.
- **SC-010**: The privacy policy's rights section describes the self-service control in all three languages, and the three agree on the retained categories.
- **SC-011**: A member who is the sole administrator of a team can never, by any sequence of actions in this flow, leave that team without an administrator.
- **SC-012**: A member who registers again with a previously deleted address **successfully receives** an account — with no content, no history, and no visible relationship to the deleted one. Registration completes rather than reporting success while creating nothing.
- **SC-013**: A **banned** address still cannot register, and a **deleted** address still can, verified as two tests of the same registration path rather than one test and an assumption.

## Assumptions

- **Scope is deletion only.** Data export (Art. 15 / Art. 20) is explicitly out of scope and tracked separately, per the split of issue #105. Nothing here should be shaped around a future export feature beyond not contradicting it.
- **Members reach this signed in.** Deletion is initiated by an authenticated member for their own account. There is no anonymous deletion route, no email-link deletion, and no admin-initiated deletion in this feature — administrators already have ban, which is a different remedy with different semantics.
- **Ban is not renamed.** Feature 013's suspend/ban semantics are unchanged by this work. Erasure is a new, fourth outcome, not a redefinition of an existing one.
- **The last-admin guard is authoritative.** Teams keep at least one administrator. Deletion respects that guard rather than working around it, and the same principle is extended to events and parties where the equivalent guard is defined here.
- **The database will not permit a naive row delete.** Numerous references to the account are `Restrict` by design. Whatever the implementation, the outcome is defined by this spec's data categories, not by which foreign keys happen to cascade.
- **Notifications already anticipate a missing actor.** The existing choice to keep a notification when its actor is gone is treated as the intended pattern for the placeholder behaviour required by FR-023.
- **Stored image objects may or may not be external yet.** Feature 035 moves profile photos to object storage and is not merged. This spec requires the photo to be gone in either arrangement; the plan resolves how.
- **No automated retention process exists.** Nothing in the platform currently expires data on a schedule. This is part of why erasure is immediate (Q1): a cooling-off window would have required building the platform's first scheduled retention process purely to serve this feature. Any retention period this feature states must be enforced by something built here, or stated plainly as manual.
- **The blast radius is bounded by the ban gate.** Two of the three resolved decisions — a freed email address, and immediate irreversible erasure — are safe only because FR-005 refuses this flow to suspended and banned accounts. That refusal is load-bearing, not defensive coding, and must not be relaxed without revisiting Q1 and Q3.
- **German is authoritative for legal text.** Per feature 036, the German privacy policy is the binding version; English and Spanish are informational and must not silently diverge.
- **Three languages ship together.** Member-facing text added by this feature exists in English, German, and Spanish before release, consistent with feature 031.

## Clarifications

### Session 2026-08-01

Three decisions had no safe default and each changed the shape of the feature rather than a detail of it. All three are resolved; the requirements above carry the outcomes.

**Q1 — Timing.** Immediate erasure on confirmation, or a cooling-off window with a cancel option?
**A: Immediate.** → FR-036, FR-037.
No scheduled process, no new account state. This keeps erasure honest — when the member asks, it is done — and avoids introducing a deleted-but-recoverable state that would sit uncomfortably close to the existing ban soft-delete this feature must stay distinguishable from. It also avoids depending on a scheduled retention process, of which the platform has none. The cost is real and is not mitigated elsewhere: a regretted confirmation is unrecoverable, which is why FR-037 promotes re-authentication and deliberate confirmation from ceremony to the actual safeguard.

**Q2 — Member-authored content in shared spaces.** Clear the content and keep the position, or retain it verbatim under a neutral author?
**A: Retain verbatim, neutral author.** → FR-024 through FR-028.
Conversations and team news are records other participants also rely on; hollowing them out damages people who did not leave. This is also the end state the chat data model's own design comment already anticipates ("history is preserved and the sender projects to a neutral placeholder"), so the platform is being made consistent with itself rather than acquiring a second rule.
The tension is recorded rather than smoothed over: **this is the one place erasure is deliberately incomplete.** A member's own words survive, and if those words contain identifying detail the member typed themselves, that detail survives with them. FR-025 and FR-027 exist so this is disclosed rather than discovered.

**Q3 — Re-registration.** May the freed email address register again?
**A: Freed.** → FR-031 through FR-035.
Retaining anything derived from the address purely to refuse it later would contradict the erasure just performed and would need its own lawful basis. Freeing it is the honest consequence of actually erasing the address. The moderation hole this might otherwise open is already closed upstream by FR-005: suspended and banned accounts cannot use this flow at all, so deletion is not a route to a clean slate.

**Owner confirmation, 2026-08-01.** The two outcomes are deliberately opposite and this is the point, not a side effect: **a ban must bar re-registration with the same address; a self-deletion must permit it.** This is now stated as FR-032 rather than left to be inferred from the mechanism.

The mechanism was verified against the source rather than assumed. Registration looks the address up and, on finding any account, returns a neutral acceptance **without creating anything** ([AuthService.cs:79-92](backend/Services/Auth/AuthService.cs#L79-L92)) — the retained banned row is what makes a ban stick. Deletion releases the address, so the same lookup finds nothing and registration proceeds. Both behaviours fall out of one code path; neither needs a new branch.

That verification also exposed a defect in the first draft of this feature's design, now FR-034: registration sets **`UserName` to the email address**, and `NormalizedUserName` carries a *unique* index. Releasing only the email would leave the username colliding, and the resulting failure lands on registration's neutral-acceptance path — telling a returning member they had registered when no account existed. Releasing the address means releasing *every* uniqueness-constrained identifier, not just the one with "email" in its name.
