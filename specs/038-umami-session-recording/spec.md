# Feature Specification: Umami Session Recording

**Feature Branch**: `038-umami-session-recording`

**Created**: 2026-08-01

**Status**: Draft

**Input**: User description: "I want to enable umami session recording" — extend the self-hosted analytics of feature 033 with session replay, delivered through the same same-origin, environment-configured path as the existing tracker, with the owner's decision that there is no consent banner.

## What this feature changes about the platform's privacy position

Feature 033 measures page views without knowing who is browsing. That is a genuinely
different activity from what this feature adds, and the difference should be stated
before the requirements rather than discovered inside them.

Session recording captures what was on the screen and what the person did with it —
the page as rendered, pointer movement, clicks, scrolling, and typing. On a platform
that is authenticated-only (feature 026), the screen almost always carries the
member's own name, and on some screens their email address or their private messages.
A recording is therefore **personal data about the viewer**, which 033's page views
deliberately were not.

**Nothing is stored on the visitor's device.** This was checked against the actual
recorder and tracker served by the Dev environment rather than assumed: neither writes a
cookie, local storage, session storage, or any other client-side identifier, and session
continuity comes from a server-side value the tracker holds in memory for the page's
lifetime. That matters more than it looks: the consent rule that governs storing or
reading information on a device stays unengaged, so 033's position on it survives intact
and the published policy's "no cookie banner" explanation remains **true** (FR-021,
FR-023).

The change is therefore narrower than it first appeared, and sharper. Exactly one of
033's requirements does not survive — FR-005, that nothing identifying the viewer is
stored — because a recording depicts the screen (FR-022). One published legal statement
becomes untrue on the day this ships, and correcting it is part of this feature rather
than a follow-up (FR-016 to FR-020).

The owner has decided this proceeds **without a consent banner**, under legitimate
interest, with Do Not Track / Global Privacy Control as the objection route. The
trade-off is recorded in Assumptions → "No consent banner is an owner decision, taken
against advice" so that it reads as a decision with known risk rather than an
oversight.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The owner can watch where a flow actually breaks down (Priority: P1)

The owner suspects members abandon a multi-step flow — registration, onboarding, or
creating an event — but the page-view figures only show that people leave, never why.
The owner opens the analytics dashboard, finds recent sessions that reached that flow,
and watches them back: where the pointer hesitated, which field was retyped, which
control was clicked twice because nothing appeared to happen.

**Why this priority**: This is the entire point of the feature. Without it there is no
reason to accept any of the privacy cost the rest of the spec manages.

**Independent Test**: Complete a multi-step flow in a browser, then find and play that
session back in the dashboard and confirm the replay shows the steps in the order they
happened.

**Acceptance Scenarios**:

1. **Given** a visitor has used the application, **When** the owner opens the dashboard
   after the session ends, **Then** a replay of that session is available and plays back
   the visited pages in sequence.
2. **Given** a session that moved through several in-app pages without a full page
   reload, **When** it is replayed, **Then** all of those pages appear in the same replay
   rather than being split into unrelated fragments.
3. **Given** a replay is playing, **When** the owner watches it, **Then** pointer
   movement, clicks, and scrolling are visible, so hesitation and repeated attempts can
   be seen.

---

### User Story 2 - Nothing a member types is captured (Priority: P1)

A member signs in and fills in forms across the platform — their password, their email
address in settings, a message they are composing. None of what they type is
reconstructable from any recording, even by the owner, and none of it ever leaves their
browser in readable form.

**Why this priority**: Equal-highest with the feature's purpose. Typed values are the
sharpest category of capture — they include credentials and anything a member is in the
middle of writing — and masking them in the browser is the one protection that holds
even if the recording store is later compromised.

**Independent Test**: Type into a password field, an email field, and a message
composer, then replay that session and inspect the stored recording for any of the typed
values — expect none. Verify by inspecting stored data, not only by watching the replay.

**Acceptance Scenarios**:

1. **Given** a member types a password, **When** the session is replayed and the stored
   recording is inspected, **Then** the password appears nowhere, in any form.
2. **Given** a member types into any form field anywhere in the application, **When** the
   recording is inspected, **Then** the typed value is absent — masking is not limited to
   fields judged sensitive one by one.
3. **Given** a masked field, **When** the session is replayed, **Then** the field's
   position, size, and the member's interaction with it stay observable — masking hides
   the value, not the fact that the field was used.
4. **Given** masking is applied, **When** network traffic leaving the browser is
   inspected, **Then** the unmasked values are not present in it — masking happens before
   transmission, not on arrival.

---

### User Story 3 - The privacy policy is true on the day recording starts (Priority: P1)

Someone reads the privacy policy the week recording is switched on, wanting to know
what is collected. What they read matches what actually happens: it tells them
recordings exist, what is in them, what is masked, how long they are kept, and how to
stop being recorded.

**Why this priority**: The policy currently states the opposite of what this feature
does — it says nothing is stored on the device and nothing identifies the viewer. A
published policy that is false is a worse position than having no policy, and this
feature is what makes it false. The correction cannot trail the release.

**Independent Test**: Read the published policy in all three languages after this
feature ships and confirm no statement in it is contradicted by the running system.

**Acceptance Scenarios**:

1. **Given** session recording is enabled in an environment, **When** the privacy policy
   is read in that environment, **Then** it describes session recording, what it
   captures, what is masked, the retention period, and the objection route.
2. **Given** the policy's explanation of why there is no cookie banner, **When** it is
   read after this feature ships, **Then** it no longer rests on the claim that nothing
   is stored on the visitor's device, because that claim is no longer true.
3. **Given** the German policy is the authoritative version, **When** the English and
   Spanish versions are compared against it, **Then** the same facts about recording
   appear in all three.

---

### User Story 4 - Saying no still means nothing at all is recorded (Priority: P2)

A visitor who has turned on Do Not Track or Global Privacy Control browses the platform.
No recording is made, and their browser is never asked to load the recording component
at all.

**Why this priority**: Objection is the only control offered in place of a consent
banner, so it has to be absolute. It is P2 only because it is inherited behaviour from
033 rather than new behaviour — but it must be re-verified here, not assumed, because it
is now carrying far more weight than it was designed to carry.

**Independent Test**: Browse with Global Privacy Control enabled, then with Do Not Track
enabled, and confirm in each case that no recording request leaves the browser and no
recording exists afterwards.

**Acceptance Scenarios**:

1. **Given** a visitor signalling Do Not Track or Global Privacy Control, **When** they
   browse any page, **Then** no recording component is requested and no recording is
   stored.
2. **Given** that same visitor, **When** they browse, **Then** nothing is written to
   their device by measurement or recording.

---

### User Story 5 - Recording can be turned off without losing the numbers (Priority: P2)

The owner decides recording is not worth its cost — or needs it off immediately for any
reason. They switch it off. Page-view analytics keeps working exactly as before.

**Why this priority**: Recording carries risk that page views do not, so it must be
retractable on its own. Coupling the two would mean the only way to stop recording is to
go blind on measurement, which guarantees hesitation at the moment speed matters.

**Independent Test**: Switch recording off, browse the application, and confirm page
views are still recorded while no new recordings appear.

**Acceptance Scenarios**:

1. **Given** recording is switched off and page-view analytics is on, **When** a visitor
   browses, **Then** page views are recorded as before and no recording is made or
   requested.
2. **Given** recording was on and is switched off, **When** the change takes effect,
   **Then** it does so without rebuilding or re-releasing the application.
3. **Given** the local stack started in the ordinary way, **When** it comes up, **Then**
   neither analytics nor recording is running.

---

### User Story 6 - Recording never becomes the platform's problem (Priority: P3)

Recording is slow, failing, or full. Members notice nothing: pages render at the same
speed, nothing blocks, and the application's own database keeps working.

**Why this priority**: Constitution Principle VII. Recording sends far more data than
page-view measurement does, so the ways it can hurt the platform are real rather than
theoretical — but it is P3 because it is a constraint on the other stories rather than a
story of its own.

**Independent Test**: Make the recording service unavailable and slow in turn, and
confirm in both cases that pages render normally and nothing user-visible changes.

**Acceptance Scenarios**:

1. **Given** the recording service is unavailable, **When** a visitor loads a page,
   **Then** the page renders normally and no error is shown.
2. **Given** the recording service is slow, **When** a visitor uses the application,
   **Then** interaction is not delayed and nothing is queued for retry.
3. **Given** recordings accumulate over time, **When** storage is examined, **Then**
   growth is bounded and the application's own data is unaffected.

---

### Edge Cases

- **A visitor is on a screen showing someone else's personal data** — a member profile,
  a team roster, a participant list. The recording captures a third party's information
  that the recorded visitor cannot consent for. This is the same subject-side exposure
  033 accepted in its FR-008, but a recording shows far more of it than a page path did.
- **A member reads a conversation.** Under FR-006a the message history on screen is
  captured, including what the *other* member wrote. The author of those messages is not
  the recorded visitor: they cannot object through their own browser, and their Do Not
  Track setting has no effect, because the recording is made by the reader's browser.
  The sharpest case in this feature.
- **Two members read the same conversation.** The same message content is then stored in
  two separate recordings, with two separate 30-day clocks.
- **A member's session spans signing out and signing in as someone else.** Whether that
  is one recording or two decides whether two members appear in a single replay.
- **A visitor turns Global Privacy Control on mid-session**, after recording has already
  started. The signal is only read when a page loads, so recording continues until the
  next load.
- **A recording is in progress when the recording service becomes unavailable.** The
  partial recording is lost; nothing is retried and nothing is queued (Principle VII).
- **A single session runs for hours.** Bounded by the recorder's maximum-recording-length
  setting, five minutes by default — so the storage risk is contained, but a long session
  is truncated rather than captured, and the owner must not read a replay's end as the
  member having left.
- **A visitor uses assistive technology or an unusual viewport.** Replay must not be
  the reason such a session is stored differently or more completely than any other.
- **Someone opens a page containing an error state carrying diagnostic text.** Anything
  rendered on screen is captured, including content never intended to be durable.
- **Recording is switched on in an environment whose privacy policy has not yet been
  updated** — the failure mode this feature exists to prevent (FR-019).

## Requirements *(mandatory)*

### Functional Requirements

#### What is recorded

- **FR-001**: The system MUST record visitor sessions so that the pages as rendered, and
  the visitor's pointer movement, clicks, scrolling, and typing, can be replayed
  afterwards in the order they occurred.
- **FR-002**: A session that moves between pages without a full page reload MUST be
  replayable as one continuous session rather than as disconnected fragments.
- **FR-003**: Recordings MUST be attributable to the environment they came from, so that
  local and Dev sessions never appear alongside Prod ones (inherits 033 FR-018).
- **FR-004**: The owner MUST be able to find a recording by when it happened and which
  pages it covered, so that a specific flow can be investigated without watching
  unrelated sessions.

#### What is not recorded

- **FR-005**: Password entry MUST NOT be captured, in any form, anywhere in a recording.
- **FR-006**: The value of **every input field in the application** MUST be masked — not
  only fields judged sensitive. Masking MUST take effect in the visitor's browser
  **before anything is transmitted**, so unmasked values never leave the device. A field
  added in future work is masked by default, without anyone having to remember it.
- **FR-006a**: **Text already rendered on the page is captured.** By owner decision,
  masking covers typed input only; no screen is excluded from recording and no displayed
  text is masked. The consequence, stated plainly because it is the widest exposure this
  feature creates: **a recording of a member reading a conversation contains that
  conversation's message history**, and a recording of a settings page contains the email
  address displayed on it. See Assumptions → "Displayed text is captured, and chat is the
  hard case".
- **FR-007**: Masking MUST preserve the shape of the page — a masked field remains
  visibly present and its position, size, and the visitor's interaction with it stay
  observable, so replay remains useful.
- **FR-008**: The system MUST NOT attach a member's account identifier, username, email
  address, or display name to a recording as a data field. (This limits what is
  *labelled*; it does not undo the fact that a recording shows the screen — see FR-022.)

#### Objection, and the absence of a consent banner

- **FR-009**: The system MUST NOT record anything, and MUST NOT request any recording
  component, for a visitor whose browser signals Do Not Track or Global Privacy Control
  (inherits 033 FR-007). This MUST be re-verified against the running system for
  recording specifically, not inherited on the strength of 033's verification.
- **FR-010**: For a visitor who has objected, nothing MUST be written to their device by
  either measurement or recording.
- **FR-011**: The objection route MUST be described in the privacy policy in terms a
  non-technical reader can act on (inherits 036).

#### Retention and access

- **FR-012**: Recordings MUST be deleted automatically **30 days** after they were made.
  Deletion MUST happen without anyone remembering to run it, and MUST keep working if
  nobody looks at it for a year. 033's indefinite retention does not extend to
  recordings: it was chosen on the explicit basis that no personal data was stored, and
  FR-022 withdraws that basis.
- **FR-012a**: The 30-day period MUST be stated in the privacy policy and MUST match what
  the system actually does. If the deletion mechanism cannot be built, recording MUST NOT
  be enabled — an unenforced retention promise in a published policy is worse than no
  promise.
- **FR-013**: Viewing recordings MUST require authentication to the analytics dashboard
  (inherits 033 FR-021), and MUST NOT be possible from within the application itself.
- **FR-014**: Recordings MUST be stored within the EU and MUST NOT be transmitted to any
  third-party service (inherits 033 FR-009).
- **FR-015**: A member MUST be able to have recordings of their own sessions deleted on
  request, by writing to the operator.
- **FR-015b**: **Deleting an account does NOT delete recordings of that member's sessions**,
  and the policy MUST NOT let a reader believe otherwise. Feature 037 shipped self-service
  account deletion that erases immediately, but recordings live in the analytics store keyed
  by a rotating session identifier with no link to an account — so nothing connects them to
  the person who just deleted themselves, and they persist until the 30-day expiry
  (FR-012). This is a direct consequence of FR-008: the very property that keeps recordings
  from identifying an account is what makes them unreachable by an erasure request. The
  30-day clock is therefore the real guarantee, not the deletion button.
- **FR-015a**: A request MUST also be answerable when the member's data appears in
  *someone else's* recording — the FR-006a case, where a message they wrote was on screen
  while another member was recorded. The policy MUST NOT promise a deletion that the
  system cannot actually perform: if such recordings cannot be located, the policy MUST
  say what can be done instead rather than implying full erasure.

#### Disclosure — this ships with the recorder, not after it

- **FR-016**: The published privacy policy MUST describe session recording: that it
  happens, what it captures, what is masked, that recordings are kept 30 days, who can
  view them, and how to object.
- **FR-016a**: The policy MUST state that **content displayed on screen is captured,
  including message content in a conversation being read** (FR-006a). It MUST NOT
  describe recordings as anonymous, as containing no personal data, or as capturing "how
  the site is used" in a way that a reader would take to exclude what is on the page.
- **FR-017**: Every statement in the published privacy policy that this feature makes
  untrue MUST be corrected. Exactly one is: **"Nothing in it says who was doing the
  browsing"**, which a recording contradicts. The claim that **nothing is stored on the
  visitor's device remains true** (FR-021) and MUST NOT be weakened, hedged, or removed —
  weakening a true statement to feel safer would misdescribe the system in the other
  direction.
- **FR-018**: The policy's explanation of why there is no cookie banner **stands and is
  kept**, because its premise survives (FR-023). It MUST, however, be checked against the
  new analytics text so a reader cannot come away with the impression that recordings are
  anonymous or contain no personal data — the banner section answers the device-storage
  question only, and must not be read as answering the lawful-basis one.
- **FR-019**: The policy changes MUST be published in an environment **before or in the
  same release as** recording is enabled in that environment. Recording MUST NOT run in
  any environment whose published policy does not yet describe it.
- **FR-020**: **No separate notification is sent to existing members.** By owner decision,
  the updated privacy policy is the only disclosure — no in-app notice, no email. This
  places the entire transparency burden on FR-016 to FR-019, so the policy text MUST be
  discoverable and plain enough to carry it: a reader who already read the old policy has
  to be able to tell that something changed. The policy's "last updated" date MUST
  therefore be visible and MUST change with this release. See Assumptions → "The policy
  page is the only notice".

#### Amendments to feature 033

- **FR-021**: 033 FR-006 ("MUST NOT write cookies, local storage, session storage, or any
  other persistent identifier to a visitor's device") **continues to apply in full, and
  is extended to recording.** Recording MUST NOT introduce any client-side storage. This
  MUST be verified against the running system rather than inherited: it is the single
  fact holding up the platform's "no cookie banner" position (FR-018), so if a future
  version of the recorder starts writing to the device, that position fails and recording
  MUST be switched off until it is reassessed.
- **FR-022**: 033 FR-005 ("MUST NOT store or transmit any value that identifies the
  visitor") is **amended for recordings only**: a recording depicts the screen, and on an
  authenticated-only platform the screen carries the viewer's identity. Recordings are
  therefore personal data about the viewer and MUST be treated as such throughout —
  bounded retention (FR-012), restricted access (FR-013), deletion on request (FR-015).
  033 FR-005 continues to apply in full to page-view measurement, which remains
  non-identifying.
- **FR-023**: 033's assumption "No device storage occurs, so the consent rules that
  govern storing or reading information on a visitor's device are not engaged"
  **stands**, verified for the recorder as well as the tracker (FR-021). The open
  question this feature raises is therefore **only** the lawful basis for the processing
  itself, not the device-storage rule — which is a materially better position than the
  one assumed when this feature was requested, and the reason the policy's cookie-banner
  section survives. Residual uncertainty, recorded rather than resolved: recording reads
  viewport and rendered-page information, and a broad reading of "gaining access to
  information stored on terminal equipment" could reach that. Not the mainstream reading,
  and not treated as a blocker.

#### Delivery, control, and not harming the platform

- **FR-024**: Recording MUST be switchable independently of page-view analytics, **without
  a deployment**. Turning recording off MUST leave page-view measurement working unchanged;
  turning analytics off MUST also stop recording.
- **FR-024a**: A switch-off MUST survive subsequent deployments. More generally, deployed
  configuration MAY narrow what is captured or leave it unchanged, but MUST NEVER widen
  it — otherwise a change made during an incident is silently undone by the next release.
- **FR-025**: Recording MUST NOT be fixed at build time and MUST NOT require a different
  build per environment (inherits 033 FR-020). Per-environment values, such as the
  proportion of sessions recorded, MUST be applied when the application is deployed.
- **FR-026**: Starting the local stack in the ordinary way MUST NOT start recording
  (inherits 033 FR-019).
- **FR-027**: Recording MUST be delivered from the platform's own origin under a name no
  mainstream privacy blocklist matches, consistent with how measurement is already
  delivered (inherits 033 FR-016).
- **FR-028**: Recording MUST NOT delay the first render of any page, MUST NOT block or
  visibly slow any interaction, and MUST NOT make any part of the application fail when
  it is slow or unavailable (inherits 033 FR-011/FR-012).
- **FR-029**: When recording data cannot be delivered it MUST be dropped silently, with
  no visible error and no retry behaviour that increases load on a failing service
  (constitution Principle VII; inherits 033 FR-013).
- **FR-030**: Recording MUST NOT be able to exhaust the shared database's capacity to the
  point of degrading the application (inherits 033 FR-014). Because recordings are orders
  of magnitude larger than page views, this requires a stated storage ceiling or an
  equivalent bound, not merely the retention period of FR-012.

### Key Entities

- **Session recording**: one visitor session captured for replay — the rendered pages,
  the interactions, and the times they happened. Belongs to a website (environment), has
  a start and end, and expires 30 days after it was made. Input values appear only in
  masked form; displayed text appears as it was rendered. Never carries a member
  identifier as a field, but depicts whatever the screen showed — including, on a
  conversation screen, message content written by someone other than the recorded
  visitor.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The owner can locate and replay a session that went through a chosen flow
  within 2 minutes of opening the dashboard, without watching unrelated sessions.
- **SC-002**: A replay of a multi-page journey shows every page visited in that session,
  in order, with no gaps where an in-app navigation occurred.
- **SC-003**: An audit of stored recordings finds zero occurrences of any value typed
  into any input field — across a test session that deliberately typed a password, an
  email address, and a message into a composer.
- **SC-003a**: The same audit, run against network traffic leaving the browser rather
  than against the store, also finds zero occurrences of those values.
- **SC-004**: With Do Not Track or Global Privacy Control enabled, a full browsing
  session produces zero recording requests, zero recordings, and zero bytes written to
  device storage.
- **SC-005**: Every factual statement in the published privacy policy, in all three
  languages, is consistent with the running system on the day recording is enabled —
  verified by reading the policy against the system, not by reviewing the diff.
- **SC-006**: No recording older than 30 days exists, verified without anyone having run
  a manual clean-up, and verified again after a period in which nobody touched the
  system.
- **SC-007**: First render timing is unchanged, within normal variation, between
  recording on and recording off.
- **SC-008**: With the recording service stopped entirely, a complete pass through the
  application's main flows shows no error, no delay, and no visible difference.
- **SC-009**: Turning recording off leaves page-view figures being collected without
  interruption.
- **SC-010**: Total recording storage stays within its stated ceiling under the platform's
  expected session volume, and the application's own database performance is unchanged.

## Clarifications

### Session 2026-08-01

- Q: Should session recording be gated behind an explicit consent opt-in?
  → A: **No consent banner.** Recording ships under legitimate interest, with Do Not
  Track / Global Privacy Control as the objection route. The affected sections of the
  published privacy policy are rewritten to match, in the same release.
- Q: Should the raw third-party-style script tag be added directly, as supplied?
  → A: No. Recording follows the delivery pattern established by 033 — same-origin, under
  a non-blockable name, injected inside the existing objection guard, and configured per
  environment rather than fixed at build time (FR-024 to FR-028).
- Q: What is masked or excluded from recordings beyond passwords?
  → A: **Input values only.** Every input field is masked in the browser before
  transmission (FR-006); no screen is excluded and displayed text is not masked, so chat
  message history and displayed email addresses are captured (FR-006a). The alternatives
  offered were masking all text everywhere, or excluding the chat and settings screens
  entirely; both were declined.
- Q: How long are recordings kept?
  → A: **30 days**, deleted automatically (FR-012).
- Q: How are existing members told that recording has begun?
  → A: **The updated policy page only** — no in-app notice and no email (FR-020).

## Open Questions

- **Prod's sample rate** — must be set in the dashboard after the first Prod deploy, or
  Prod records 15% by default. Choose it against the storage figures from FR-030.
- **Where the retention job runs** (FR-012) — the platform has no automated retention
  mechanism anywhere today, so this is new capability rather than configuration. A
  question for `/speckit-plan`, not a gap in the requirement.

## Assumptions

- **No consent banner is an owner decision, taken against advice.** Session recording
  writes to the visitor's device and captures personal data about an identifiable member,
  which is the situation consent rules are written for; the common reading is that
  session replay of identifiable users requires consent, and legitimate interest is a
  weak position for it in the EU. The owner has decided to proceed on legitimate interest
  with Do Not Track / Global Privacy Control as the objection route, and the trade-off is
  recorded here rather than argued in the requirements. The practical consequence: the
  platform is exposed to a challenge it would not face with an opt-in, and the mitigation
  is that the policy states plainly what happens (FR-016 to FR-020) rather than that the
  processing is minimised.
- **Displayed text is captured, and chat is the hard case.** The owner decided masking
  covers typed input only (FR-006a), declining both "mask all text" and "exclude the chat
  and settings screens". Recorded consequence, because it is the widest exposure this
  feature creates: **a recording of a member reading their messages contains that
  conversation's history, including what the other person wrote** — and that other person
  is not the recorded visitor, has no way to object, and their Do Not Track signal does
  not protect them, because it is the *reader's* browser that does the recording. This is
  the point at which analytics reaches member-to-member communication rather than product
  usage, and it is the part of this feature least supported by a legitimate-interest
  argument. The masking decision can be tightened later without redesign — it is
  configuration of what the recorder captures, not structure — so the cheapest mitigation
  remains available if the position is challenged.
- **Recordings are personal data; page views remain non-personal.** The two are kept
  distinguishable throughout so that switching recording off returns the platform to
  033's original position rather than to an unclear one.
- **The policy page is the only notice.** The owner decided against an in-app notice and
  against emailing members (FR-020). The consequence: members who registered under a
  policy saying "stores nothing on your device" and "nothing says who was doing the
  browsing" will be recorded under a changed policy they are not told about, and will
  only find out if they re-read the page. The visible "last updated" date is the sole
  signal that anything changed, which is why FR-020 makes it a requirement rather than a
  detail.
- **30 days is short enough to be defensible and long enough to be useful.** It covers
  investigating a flow noticed this month; it deliberately does not support
  year-over-year comparison, which remains page-view territory where 033's indefinite
  retention still applies.
- **How much is recorded is an operational setting, not a specified one.** Dev records
  every session; Prod should record a fraction, because there the volume is real and
  sampling is data minimisation as much as cost control. Both are set in the analytics
  dashboard rather than in deployed configuration — see the next assumption for why.
- **Recording is on wherever analytics is on, and the runtime switch is the dashboard.**
  Owner decision, and it is why there is no deploy-time on/off flag: turning recording off
  must not require an apply. The consequence for the deployed configuration is that a
  deploy may narrow what is captured or leave it alone, but must never widen it — a
  dashboard switch flipped during an incident has to survive the next deploy.
- **Only the owner views recordings.** No additional role or permission is introduced;
  dashboard authentication (033 FR-021) is the whole access control.
- **The third-party exposure of 033 FR-008 widens here, and is accepted on the same
  basis.** A page path revealed which profile was viewed; a recording shows it rendered.
  The owner already accepted the subject-side exposure in 033; this feature increases its
  detail rather than introducing it.
- **Verified rather than assumed, and it changed the answer.** The recorder and tracker
  served by the Dev environment were read directly. The recorder is an rrweb bundle that
  uses **no client-side storage API at all** — no cookie, no local or session storage, no
  IndexedDB — and both it and the tracker send with credentials omitted, so the platform's
  own sign-in cookie never reaches the analytics service. Session continuity comes from a
  server-issued value the tracker keeps in memory. An earlier reading of the same file
  claimed the opposite; the requirements follow the file, not the summary. The practical
  effect is that the device-storage rule stays unengaged and the policy's cookie-banner
  section survives (FR-021, FR-023).
- **The recorder's behaviour is configured on the server, not in the page.** Masking
  level, sampling rate, maximum recording length, and any blocked page regions are read
  from the analytics service per website; the page only says which website it is. Two
  consequences worth stating in a spec rather than leaving to the plan: **these settings
  can be changed without a release**, and they are therefore part of the deployed
  configuration that has to be reproducible per environment rather than clicked in a
  dashboard.
- **Recording stops after five minutes by default.** The maximum recording length is one
  of those server-side settings. It bounds the "session runs for hours" edge case for
  free, and it also bounds usefulness: a long, meandering session is not fully captured.
- **Retention deletion is automatic.** The platform has no automated retention job
  anywhere today (recorded in 036); FR-012 is therefore new capability, not configuration
  of something existing, and should be sized as such.
- **German remains the authoritative language** for the policy changes, with English and
  Spanish informational, as established in 036.

## Dependencies

- **Feature 033 (Self-hosted Umami analytics)** — deployed in Dev and Prod. This feature
  extends its delivery path, its objection guard, its per-environment configuration, and
  its dashboard, and amends three of its requirements (FR-021 to FR-023).
- **Feature 036 (Privacy policy & imprint)** — deployed in Dev and Prod. Its published
  text is made untrue by this feature and is corrected as part of it (FR-016 to FR-020).
- **Feature 026 (Authenticated-only access)** — the reason recordings are personal data:
  almost every recorded session belongs to a signed-in member.
- **Feature 019 (Chat)** — private message content is on screen in the application and is
  the sharpest case for the masking decision (FR-006).
- **GH #106 (Data retention: no automated deletion runs anywhere)** — open, and now
  blocking rather than background. FR-012's 30-day deletion is the platform's first
  automated retention mechanism; #106 records that none exists. FR-012a makes recording
  contingent on it working, so this feature either builds that mechanism or does not
  ship.
- **Constitution Principle VII** — bounded time limits, no retry amplification, and a
  stop condition; recording sends materially more data than measurement does.

## Out of Scope

- **A consent banner or consent management platform.** Explicitly decided against; see
  Assumptions.
- **Attributing recordings to known members**, or any in-app feature that surfaces a
  recording to anyone other than the owner in the analytics dashboard.
- **Recording as a support or debugging tool** — reproducing a specific member's reported
  problem on request. That is a different purpose with a different legal basis and would
  need its own feature.
- **Heatmaps, funnels, or other aggregate analyses** built on recording data.
- **Making recordings reachable by account deletion.** Feature 037 shipped self-service
  account deletion, but it cannot reach recordings and this feature does not change that —
  see FR-015b for why, and what the policy must therefore not imply. Bridging the two would
  mean linking recordings to accounts, which is precisely what FR-008 forbids.
- **Changing the objection mechanism.** Do Not Track / Global Privacy Control is what 033
  built and what 036 published; this feature re-verifies it rather than replacing it.
