# Feature Specification: Wizard drafts survive leaving the page

**Feature Branch**: `fix/training-wizard-draft`

**Created**: 2026-08-11

**Status**: Draft

**Input**: GH #182 — "Create-training wizard loses all input when you leave the page or the tab is reloaded". User description: "Wizard drafts survive leaving the page (GH #182). The create-training wizard and the create-event wizard keep every answer in component memory, so navigating away in-app, hitting back, reloading, or having a backgrounded mobile tab evicted throws the user back to a blank step 1. Persist the in-progress wizard state to sessionStorage as a draft, restore it when the wizard is opened again, and clear it on successful create or explicit cancel. Owner decisions already taken: (1) draft persistence only — no step-in-route encoding and no canDeactivate/beforeunload warning, because a restored draft leaves nothing to warn about; (2) both wizards, and every field is persisted including the event wizard's fee recipient name and IBAN; (3) the training draft is keyed per team slug. Because the event draft now holds a bank account number, and because this is the first non-trivial device-storage write in the product, the privacy policy's 'Cookies and what's kept in your browser' section and its 'Ours stores nothing there — no cookie, no local storage, nothing' sentence in the no-cookie-banner section must be corrected in all three locales (en/de/es, German authoritative) — the draft never leaves the device and never reaches the server, and that is what the text should say. Frontend only: no backend, no endpoint, no entity, no migration."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A half-filled training survives a detour (Priority: P1)

A team admin is four steps into setting up a weekly training. Something interrupts: they tap through to the team page to check the usual venue, a link takes them elsewhere, they hit back, or — the reported case — they switch apps on their phone for a minute and the browser throws the tab away. They come back to the create-training screen and everything they typed is still there, on the step they left it on.

**Why this priority**: This is the reported defect. The address and description steps are the expensive ones to retype, and a mobile admin loses them to nothing more than answering a message. It is also the only story that fixes what the tester actually hit.

**Independent Test**: Fill steps 1–4 of the create-training wizard, navigate to the team page and back (and separately, reload the browser), and confirm every answer and the current step are intact. Delivers the whole reported fix on its own.

**Acceptance Scenarios**:

1. **Given** an admin has filled the name, schedule, address and description steps of the create-training wizard, **When** they navigate to another page in the app and return to the create-training screen for the same team, **Then** every answer is restored and the wizard opens on the step they left.
2. **Given** the same half-filled wizard, **When** the browser reloads the page (a manual reload, or the tab being evicted while backgrounded and re-opened), **Then** every answer is restored and the wizard opens on the step they left.
3. **Given** a restored draft, **When** the admin completes the wizard and the training is created, **Then** opening the create-training wizard again presents a blank step 1.
4. **Given** a restored draft, **When** the admin presses Cancel, **Then** the draft is discarded and opening the wizard again presents a blank step 1.
5. **Given** an admin has a draft for team A, **When** they open the create-training wizard for team B, **Then** team B's wizard is blank and team A's draft is untouched.
6. **Given** a completed wizard whose creation is rejected by the server, **When** the error is shown, **Then** the answers remain both on screen and in the draft, so a reload does not compound the failure.

---

### User Story 2 - A half-filled event survives the same detour (Priority: P2)

Someone setting up a tournament is six steps deep — type, dates, address, participation, and the payment details including the account the fee is paid into. The same interruptions apply, and the event wizard is longer and its description step is mandatory, so the loss is larger. Coming back restores the whole thing.

**Why this priority**: The same defect in the same shape, and the issue asks for it to be decided once and applied to both. It is second only because #182 was reported against trainings.

**Independent Test**: Fill the type, when, where, who and fee steps of the create-event wizard, leave and return (and separately reload), and confirm every answer and the current step are intact.

**Acceptance Scenarios**:

1. **Given** someone has filled the create-event wizard through the fee step, **When** they leave the page and return, **Then** every answer — including the fee recipient name and account number — is restored on the step they left.
2. **Given** the same half-filled wizard, **When** the browser reloads the page, **Then** every answer is restored on the step they left.
3. **Given** a restored draft, **When** the event is published successfully, **Then** opening the create-event wizard again presents a blank first step.
4. **Given** a restored draft the person no longer wants, **When** they abandon it and open the create-event wizard again in the same tab, **Then** the abandoned draft is restored again — there is no in-wizard way to throw it away, and this is the accepted consequence of the decision recorded below. Closing the tab or signing out clears it.
5. **Given** a restored draft, **When** the wizard opens, **Then** nothing announces the restoration: the person sees their own answers where they left them, with no banner or prompt.

---

### User Story 3 - The privacy policy says what is now kept in the browser (Priority: P3)

A reader of the privacy policy wants to know what this site puts on their device. Today the policy says, in the section explaining why there is no cookie banner, that the site stores nothing there — "no cookie, no local storage, nothing". Once an unfinished wizard is kept in the browser, that sentence is false, and the draft can contain a bank account number. The policy names it, says it never reaches the server, and says when it disappears.

**Why this priority**: It ships with the change rather than after it — the policy must not be wrong in the window between. It is P3 because it carries no user-facing function; it is a correctness obligation on the document.

**Independent Test**: Read `/privacy` in each of the three languages and confirm the browser-storage section names the unfinished-form draft, states that it stays on the device, and that the no-cookie-banner reasoning no longer rests on a claim that nothing is stored.

**Acceptance Scenarios**:

1. **Given** a reader on `/privacy` in German (the authoritative version), **When** they read the section on what is kept in the browser, **Then** it names the unfinished create-form draft, says it never reaches the server, and says it goes when the tab is closed.
2. **Given** a reader on `/privacy` in any of the three languages, **When** they read the reasoning for there being no cookie banner, **Then** it no longer claims nothing at all is stored on the device, and the reasoning it gives instead is sound.
3. **Given** the three locale documents, **When** their key sets are compared, **Then** they are identical — no language silently falls back to English inside the legally binding text.

---

### Edge Cases

- **A draft written by an older version of the app.** Field shapes change between releases. A stored draft that no longer matches what the wizard expects is discarded and the wizard opens blank, rather than restoring half of it or breaking on the way in.
- **Someone signs out in the same tab.** Drafts are cleared on sign-out, so a second person signing in on the same device does not inherit an unfinished form — which for the event wizard would mean someone else's bank account number.
- **Browser storage is unavailable or full.** Private-browsing quotas and storage-disabled configurations exist. The wizard must work exactly as it does today when the draft cannot be written or read; the persistence is an enhancement, never a precondition.
- **Closing the tab.** A draft deliberately does not survive it. Closing the tab, or quitting the browser, is the user's own "throw this away", and the storage is chosen to honour that. This bounds the fix: a draft survives leaving, backgrounding, and reloading, not a browser restart.
- **Two tabs on the same wizard.** Each tab carries its own draft, and neither overwrites the other.
- **A draft that is empty.** Opening the wizard, touching nothing, and leaving must not leave a draft behind that later looks like restored input.
- **A created training or event whose navigation away fails.** The draft is cleared once the server has accepted the creation, not before — an accepted create must never leave a stale draft that reappears as a phantom second training.

## Requirements *(mandatory)*

### Functional Requirements

**Drafting and restoring**

- **FR-001**: The create-training wizard MUST retain every answer it holds — the series-or-one-off choice, name, weekday, interval, start and end times, start and end dates, location kind, venue name, street, postal code, city, virtual link, description, and visibility (16 answers) — plus the step the user is on, across the screen being left and re-entered within the same browser tab.
- **FR-002**: The create-event wizard MUST retain every answer it holds — type, custom type label, name, description, start and end date-times, location kind, venue name, street, postal code, city, virtual link, participant mode, participation limit, roster cap, paid-or-free, fee amount, currency, recipient name, account number, and payment deadline (21 answers) — plus the step the user is on, across the same.
- **FR-003**: Both wizards MUST restore a retained draft when the wizard is opened again, presenting the step the user left rather than the first step.
- **FR-004**: Retention MUST survive the page being reloaded, including the case where a backgrounded mobile tab was discarded by the operating system and re-created from scratch on return.
- **FR-005**: A retained draft MUST be written often enough that no completed step can be lost — at minimum whenever the user moves between steps, and it MUST NOT depend on the user reaching a later step for earlier answers to be safe.
- **FR-006**: The create-training draft MUST be scoped to the team it was started for, so a draft for one team never appears in another team's wizard.

**Discarding**

- **FR-007**: A retained draft MUST be discarded once the server has accepted the creation, so that reopening the wizard presents a blank first step.
- **FR-008**: The create-training wizard's existing Cancel action MUST discard the draft.
- **FR-009**: Restoring a draft MUST be silent. No notice, banner or prompt announces that the wizard was pre-filled, and **no "start over" control is added to either wizard** (owner decision; see the Decision on restore surfacing below). The ways to be rid of a draft are therefore exactly: completing the wizard (FR-007), the create-training wizard's existing Cancel (FR-008), signing out (FR-011), and closing the tab (FR-010).
- **FR-010**: A retained draft MUST NOT survive the browser tab being closed.
- **FR-011**: Retained drafts MUST be discarded when the user signs out.
- **FR-012**: A retained draft that cannot be read, or that does not match the shape the wizard expects, MUST be discarded and the wizard MUST open blank.
- **FR-013**: A wizard that was opened and left without any answer being given MUST NOT leave a draft behind.

**Boundaries**

- **FR-014**: A retained draft MUST NOT leave the user's device: it is never sent to the server, never included in any request, and never reaches any third party.
- **FR-015**: If retaining or restoring a draft fails for any reason, both wizards MUST continue to work exactly as they do today. Persistence never blocks creating a training or an event.
- **FR-016**: This feature MUST NOT change any server behaviour, contract, or stored data. No endpoint, entity, or migration is added or altered.
- **FR-017**: The onboarding wizard MUST be left unchanged. It already writes to the server as it goes and is out of scope.
- **FR-018**: No warning-before-leaving prompt and no step-in-the-address behaviour is added. The restored draft is the whole of the remedy (owner decision; see Assumptions).

**The privacy policy**

- **FR-019**: The privacy policy's account of what is kept in the browser MUST name the unfinished create-form draft, state that it stays on the device and never reaches the server, and state that it goes when the tab is closed or the form is finished or cancelled.
- **FR-020**: The privacy policy MUST NOT continue to assert that nothing at all is stored on the reader's device. The reasoning given for there being no cookie banner MUST be corrected so that it remains sound alongside the draft and the existing language preference.
- **FR-021**: The policy change MUST be made in all three locales, with the German text authoritative, and the three documents MUST keep identical key sets so no passage of the binding text silently renders in English.

### Key Entities

- **Wizard draft**: one unfinished create-form, as the person filling it in left it — every answer they had given plus the step they were on. Belongs to one browser tab and one signed-in person; for a training, also to one team. Exists only on the device. Created the moment a first answer is given, replaced as the person works, and destroyed on completion, cancellation, sign-out, or the tab closing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user who has filled four steps of the create-training wizard, left the page, and returned retypes **zero** fields, and lands on the step they left rather than step 1.
- **SC-002**: The same holds after a full page reload and after the tab has been discarded and re-created — the case the tester reported ("leave the application while creating a training and come back after a short while").
- **SC-003**: All 16 create-training answers and all 21 create-event answers survive a reload. No answer is silently dropped; each is verified individually.
- **SC-004**: After a training or event is successfully created, opening the same wizard again shows a blank first step — zero answers carried over.
- **SC-005**: After signing out, opening either wizard shows a blank first step, with no answer from the previous session visible.
- **SC-006**: No draft content appears in any request the application makes — verifiable by filling both wizards and inspecting every outbound request until the moment of creation.
- **SC-007**: With browser storage unavailable, both wizards can still be completed end to end; the only difference the user sees is that leaving the page loses the answers, as today.
- **SC-008**: `/privacy` in all three languages describes the draft, and the three documents have identical key sets.

## Assumptions

- **Draft persistence is the whole remedy** (owner decision, taken against the issue's three options). No step-in-the-address encoding and no leave-warning prompt are built: a draft that survives leaves nothing to warn about, and the step travels in the draft. If the draft mechanism later proves unreliable on some browser, a warning becomes worth revisiting.
- **Everything is persisted, including the event fee's recipient name and account number** (owner decision). The alternative considered and rejected was excluding the fee fields so a bank account number never touches browser storage. Consequences accepted here and mitigated by FR-010 (gone when the tab closes) and FR-011 (gone on sign-out), and disclosed by FR-019.
- **A draft belongs to a tab, not to the device.** This follows from FR-010 and is what makes the exposure of the previous point bounded: the draft cannot outlive the browsing session, and closing the tab is a reliable way to be rid of it. The cost is that a draft does not survive quitting the browser entirely — accepted, because the reported failure (backgrounding, eviction, reload, in-app navigation) is inside the tab's lifetime.
- **Drafts are per signed-in person.** Both wizards are behind authentication, so a draft always has an owner; FR-011 keeps that true when the owner changes on a shared device.
- **The three legal documents already have a parity guard** (feature 036), so FR-021's identical-key-set requirement is enforced by an existing test rather than a new one — to be confirmed at plan time, not assumed.
- **`/privacy` needs no new section.** The existing "Cookies and what's kept in your browser" section already accounts for the language preference; the draft is one more entry in the same list. Whether the analytics section's separate "stores nothing on your device" claims need touching is a question of scope for that section — they concern the analytics tool specifically and remain true of it.
- **The tester's report is fully explained by this defect.** No server-side session or timeout is involved; the auth cookie outlives the eviction, which is why the user comes back signed in to a blank wizard rather than to a sign-in screen.

## Decision on restore surfacing

**Asked**: whether returning to a pre-filled wizard is announced, and what control the user gets to start over. The create-training wizard has a Cancel action that can clear the draft; the create-event wizard has no cancel at all — only a "back home" link.

**Decided (owner)**: **silent restore, no discard control.** The wizard simply opens filled in. Nothing is added to either wizard's chrome, and no new copy is written.

**Why it was a real question**: the alternative — a "Picked up where you left off / Start over" line — would explain the pre-filled form and give the event wizard a discard control it otherwise lacks, at the cost of new copy in three languages and a notice on a screen the design keeps deliberately calm.

**Consequences accepted, and where they are handled**:

- A user who abandons an event draft and starts another event in the same tab is handed the abandoned one back, **including the previous fee recipient and account number**. There is no in-wizard remedy; the escape hatches are closing the tab (FR-010) and signing out (FR-011). This is the sharpest edge of combining "persist everything" with "no discard control", and it is recorded here rather than discovered later.
- A user who does not remember leaving a draft may be briefly puzzled by a pre-filled form. Judged acceptable: the answers are their own and visibly theirs, and the wizard is reachable only by a deliberate act.
- Nothing here forecloses adding the notice later. It is additive, and this decision does not build anything that would have to be removed first.
