# Feature Specification: Terms of Use with Community Rules

**Feature Branch**: `041-community-guidelines-terms`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "we need something like terms&conditions or community guidelines. Something that the user has to actively accept during registration and which we can enforce and gives us the right to delete content or ban users etc."

## Context

The platform already has the *machinery* to enforce — feature 013 gave it `Suspended` and
`Banned` account states, admin actions to move an account between them, and an append-only
record of who did what to whom. What it has never had is the *agreement* those powers rest on.
Nobody who signed up was ever shown a rule, and no record exists of anyone agreeing to anything.
`CODE_OF_CONDUCT.md` does not close this gap: it governs the GitHub repository and says so in
its own Scope section.

This feature supplies the missing agreement and the evidence that each member entered into it.
It does **not** build new enforcement capability.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A new player agrees to the rules before getting an account (Priority: P1)

Someone fills in the registration form. Alongside email, handle and password there is a
statement that creating an account means agreeing to the Terms of Use, with the document one
click away. The box is empty until they tick it themselves. If they don't tick it, they don't
get an account — and that refusal holds whether the request came from the form or from anything
else that can reach the registration endpoint. When the account is created, the platform keeps a
durable record of *which version* of the document that person agreed to, *when*, and *in which
language it was shown to them*.

**Why this priority**: This is the entire feature. Without it the document is a page nobody
agreed to, and every later enforcement decision rests on nothing. A ban issued against someone
who was never shown a rule is not enforcement, it is arbitrary.

**Independent Test**: Register through the form without ticking the box — submission is refused.
Send a registration request that omits acceptance entirely — the server refuses it and no
account exists afterwards. Tick the box and register — the account is created and an acceptance
record exists naming the document version, the moment, and the language displayed.

**Acceptance Scenarios**:

1. **Given** the registration form with every other field valid, **When** the acceptance control
   is left unticked, **Then** the form cannot be submitted and the reason is stated in plain
   language.
2. **Given** a registration request that omits acceptance or carries it as false, **When** it
   reaches the server directly (not through the form), **Then** registration is refused and no
   account, profile, or acceptance record is created.
3. **Given** a registration request carrying valid acceptance, **When** the account is
   successfully created, **Then** exactly one acceptance record exists for it, naming the
   document version, the acceptance moment, and the language the document was presented in.
4. **Given** a registration attempt that carries valid acceptance but fails for another reason
   (handle already taken, password rejected), **When** the attempt is refused, **Then** no
   acceptance record is left behind.
5. **Given** the registration form, **When** the member opens the Terms of Use from the
   acceptance control, **Then** they can read the full document and return without losing what
   they had already typed.
6. **Given** the registration form in any supported language, **When** it is first rendered,
   **Then** the acceptance control is unticked, and it is never pre-ticked or ticked on the
   member's behalf.

---

### User Story 2 - Anyone can read the rules, in their language, before deciding (Priority: P2)

A person who has not signed up — and may be deciding whether to — opens the Terms of Use. They
read it without an account, without a sign-in wall, and without the app pushing them to
register. They read it in German, English or Spanish, and if they are reading a translation the
page tells them plainly that the German text is the one that governs. They can reach it from the
same places the privacy policy and imprint are reachable from.

**Why this priority**: Consent to a document nobody can read before agreeing is not consent.
This also carries the document's own legibility burden — it is a text people are being asked to
be bound by, so it has to be findable and readable on its own terms.

**Independent Test**: While signed out, reach the Terms of Use from the footer and from the
registration screen, in each of the three languages; confirm the German text is marked as
authoritative, the translations carry a divergence notice, and the version and last-updated date
are visible on the page.

**Acceptance Scenarios**:

1. **Given** a signed-out visitor, **When** they open the Terms of Use address directly,
   **Then** the document renders in full without requiring an account.
2. **Given** any screen in the product, **When** the visitor looks for the legal links, **Then**
   the Terms of Use is reachable in no more clicks than the privacy policy and imprint are.
3. **Given** the document displayed in English or Spanish, **When** it renders, **Then** a
   visible notice states that the German version is the authoritative one.
4. **Given** the document in any language, **When** it renders, **Then** its version identifier
   and last-updated date are visible.
5. **Given** the document text fails to load, **When** the page renders, **Then** a visible error
   and a retry are shown — never an empty or partial document.
6. **Given** the Terms of Use page, **When** the reader finishes, **Then** they can move directly
   to the privacy policy and imprint, and those documents link back.

---

### User Story 3 - The operator can act on a rule breach and show what was agreed (Priority: P3)

Someone posts something that breaks the rules. The operator suspends or bans the account using
the controls that already exist. What is new is that the account is provably bound: for any
account, the operator can produce the version of the rules that account agreed to and when. The
document itself states plainly what the operator may do — remove content, suspend, ban — and
gives a working address to write to in order to dispute it.

**Why this priority**: This is the payoff, but it depends entirely on US1 having happened, and
it adds no new capability — the suspend/ban controls and their audit trail already exist. It is
listed so the chain from rule to record to action is tested end to end rather than assumed.

**Independent Test**: For an account created through the new flow, retrieve its acceptance record
and confirm it names a version of the document whose text authorises the action being taken;
confirm the existing suspend/ban path still works unchanged and still writes its own action
record.

**Acceptance Scenarios**:

1. **Given** an account created through the new registration flow, **When** its acceptance record
   is retrieved, **Then** it identifies the document version that account is bound by.
2. **Given** an account that has been banned, **When** its acceptance record is retrieved,
   **Then** the record still exists and still evidences the original agreement.
3. **Given** an account that the member has erased themselves, **When** the acceptance record is
   retrieved, **Then** the record still exists as evidence that an agreement was entered into,
   and it identifies nobody.
4. **Given** the Terms of Use, **When** a reader looks for how to dispute a suspension or ban,
   **Then** the document names a contact route that actually reaches the operator.

---

### Edge Cases

- **Acceptance claimed but registration fails downstream** — a handle is taken between the
  availability check and submission. No account is created, so no acceptance record may survive.
- **Registration bypassing the form entirely.** The unticked box is a UX affordance, not a
  boundary; a request that never rendered the form must still be refused without acceptance.
- **A banned member tries to sign up again.** The existing retained-email denylist refuses them;
  no second acceptance record is created for that address.
- **A member erases their own account (feature 037).** The account row survives with its
  identifying columns neutralised. The acceptance record must survive with it and must not
  re-identify the erased person.
- **The German text is missing a paragraph the English text has.** The platform's global
  translation fallback would silently render English inside the legally binding German document.
  This must be caught before release, not by a reader.
- **The document is still carrying an unfilled placeholder** where the operator has to supply a
  particular. It must not be possible to ship the document in that state.
- **The reader's language differs from the authoritative one.** What they agreed to must be
  recorded as the version, not as the translation they happened to see — while still recording
  which translation they saw.
- **The Terms of Use text fails to load while the registration form is open.** The member must
  not be pushed into agreeing to a document they were unable to read.
- **The document changes after someone accepted it.** Their record must continue to name the
  older version rather than silently tracking the current one.

## Requirements *(mandatory)*

### Functional Requirements

#### The document

- **FR-001**: The platform MUST publish a single Terms of Use document at a stable public
  address, readable in full without an account and without signing in.
- **FR-002**: The document MUST be available in German, English and Spanish. The German text is
  authoritative; the English and Spanish texts MUST carry a visible notice saying so.
- **FR-003**: The document MUST display a version identifier and a last-updated date.
- **FR-004**: The document MUST contain a section stating how members are expected to behave and
  what is prohibited, written to cover every surface where members can put text or images —
  profile descriptions, team and event and training descriptions, chat messages, marketplace
  listings, uploaded images, and the names and handles people choose.
- **FR-005**: The document MUST reserve the operator's right to remove member-provided content
  and to suspend or ban an account for breaking the rules.
- **FR-006**: The document MUST state that members keep ownership of what they write and upload,
  and grant only the limited permission needed for the platform to display it. This MUST NOT
  contradict the privacy policy's existing statement that "what you write and upload is yours".
- **FR-007**: The document MUST give a contact route that actually reaches the operator, usable
  both for general disputes and for challenging a suspension or ban.
- **FR-008**: The document MUST NOT describe any moderation process, review timeline, appeal
  procedure, or reporting tool that the platform does not actually provide.
- **FR-009**: The document MUST NOT contradict the privacy policy or the imprint on any point
  the two documents both address — in particular content ownership, retention, account deletion,
  and the record kept of admin actions.
- **FR-010**: The Terms of Use MUST be reachable from the same surfaces the privacy policy and
  imprint are reachable from, and MUST be cross-linked with them in both directions.
- **FR-011**: The document MUST render outside the signed-in application shell and MUST NOT
  require or push sign-in to be read.
- **FR-012**: When the document text cannot be loaded, the page MUST show a visible error and a
  retry rather than an empty, partial, or wrong-language document.
- **FR-013**: The document MUST set no minimum age. It MUST instead state that where the member
  is a minor, their parent or guardian is responsible for their use of the platform and is taken
  to have agreed to these terms on their behalf. This is a statement in the text only: the
  platform MUST NOT ask for an age or date of birth, MUST NOT present an age confirmation, and
  MUST NOT gate registration on age in any way.
- **FR-014**: The document MUST state that the version published on the page, carrying the date
  it shows, is the one that applies. It MUST NOT promise notification of changes, announcements,
  or a re-acceptance step, because none of those exist and none are built here.

#### Acceptance at registration

- **FR-015**: The registration screen MUST present an acceptance control that the person has to
  act on themselves. It MUST be unticked when the screen first renders and MUST NEVER be
  pre-ticked, defaulted to accepted, or ticked on the member's behalf.
- **FR-016**: The acceptance control MUST link to the full Terms of Use, and following that link
  MUST NOT discard data already entered into the registration form.
- **FR-017**: The registration form MUST prevent submission until acceptance is given, and MUST
  say why submission is blocked. This is a usability aid only.
- **FR-018**: The server MUST refuse any registration request that does not carry acceptance,
  regardless of how the request was produced. This is the enforcement boundary; the disabled
  submit button is not.
- **FR-019**: A refused registration MUST leave no account, no profile, and no acceptance record.

#### The record of acceptance

- **FR-020**: On successful account creation the platform MUST record the acceptance, capturing
  which account accepted, which document version was accepted, the moment of acceptance, and the
  language the document was presented in.
- **FR-021**: The accepted version MUST be recorded as a version identifier, not as a
  yes/no flag, so that a later version and a re-acceptance flow become possible without
  restructuring the stored data.
- **FR-022**: An acceptance record MUST be created only when the account is actually created.
  A failed registration MUST NOT leave one behind.
- **FR-023**: Acceptance records MUST be durable evidence: never rewritten to point at a
  different version, and never removed when an account is suspended or banned.
- **FR-024**: When a member erases their own account, the acceptance record MUST survive as
  evidence that an agreement was entered into, in a form that does not re-identify the erased
  person.
- **FR-025**: The platform MUST be able to produce, for any account, the version of the Terms of
  Use that account is bound by and when it agreed.

#### Release guards

- **FR-026**: The German, English and Spanish texts MUST be verified to contain exactly the same
  set of content slots before release, so that no paragraph of the authoritative German document
  can be silently replaced by English.
- **FR-027**: It MUST NOT be possible to release the document while it still contains an
  unfilled placeholder for a particular the operator has to supply.

### Out of Scope

Recorded explicitly, because the document reserves rights the product cannot yet exercise
through any interface:

- **Admin content removal.** No interface exists to delete a chat message, team, event, training,
  marketplace listing, profile description, or uploaded image. Removal remains a manual
  database operation. The document reserves the right; this feature does not build the tool.
- **Member reporting.** There is no way for a member to report content or conduct to the
  operator. Feature 027's "contact admins" reaches the admins of a team or event, not the
  platform operator.
- **Re-acceptance for existing accounts.** Every account in every environment today is test
  data, so no migration, interstitial, or version-comparison gate is built for them.
- **A moderation queue, case tracking, or appeal workflow.** Disputes go to the contact address.
- **Any age verification.** Owner decision: no age field, no date of birth, no age confirmation,
  no gate. FR-013 is satisfied entirely by wording in the document.
- **Notification of terms changes.** Owner decision (FR-014): the published page is the notice.
  No email, no in-app announcement, no re-acceptance prompt is built or promised.

### Key Entities

- **Terms of Use document**: the single binding text, existing in three languages with the
  German one authoritative, carrying a version identifier and a last-updated date. Its
  version identifier is what acceptance records point at.
- **Terms acceptance record**: durable evidence that one account agreed to one version of the
  document at one moment, including the language it was shown in. Created once, at account
  creation; never altered afterwards; outlives suspension, ban, and self-erasure of the account.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of accounts created after this ships have exactly one acceptance record
  naming a document version, a timestamp, and a display language.
- **SC-002**: Zero accounts can be created without acceptance, including by requests that never
  rendered the registration form.
- **SC-003**: A reader can get from any screen in the product to the full Terms of Use in no more
  clicks than it takes to reach the privacy policy.
- **SC-004**: The three language versions contain an identical set of content slots — verified
  automatically, with a release-blocking failure if they diverge.
- **SC-005**: No unfilled placeholder can reach a release of the document — verified
  automatically, release-blocking.
- **SC-006**: For any account, the operator can state which version of the rules it is bound by
  and when it agreed, without inspecting application logs.
- **SC-007**: Every statement in the document about what the platform does with member data or
  member accounts is consistent with the privacy policy and the imprint — no contradictions on
  content ownership, retention, deletion, or admin action records.
- **SC-008**: Registration completion time is not materially increased — a member who intends to
  accept adds one interaction, not a separate screen or a scroll-to-bottom gate.

## Assumptions

- **One document, not two.** Owner decision: a single Terms of Use with the community rules as a
  section inside it, rather than a separate Community Guidelines page. One acceptance, one
  version, one text to keep synchronised across three languages.
- **Scope is the document plus acceptance.** Owner decision: the enforcement *capability* is out
  of scope; this feature supplies the agreement that the existing capability rests on.
- **Existing accounts are ignored.** Owner decision: all current accounts in all environments are
  test data.
- **The infrastructure from feature 036 is reused as-is** — the same catalogue mechanism, the
  same German-authoritative rule and divergence notice, the same unguarded off-shell routing, the
  same long-form content styling, and the same release-guard tests extended to cover the new
  text. This feature adds a third document to an existing pattern rather than inventing one.
- **The contact route is the operator address already published** in the privacy policy and
  imprint. No new contact channel is introduced.
- **Governing law is German** and the operator is established in Germany, consistent with the
  imprint and with the Hamburg supervisory authority already named in the privacy policy.
- **The document does not promise availability.** The platform is volunteer-run and the privacy
  policy already describes it that way; the terms are assumed to disclaim uptime and fitness
  guarantees to the extent the law permits, rather than committing to a service level.
- **A ban does not by itself erase what the member wrote.** Feature 013 makes a ban a retained
  soft-delete that hides the profile from player-facing surfaces; content authored by the account
  is not deleted by that action. The document is assumed to describe removal of content as a
  separate act from suspending or banning an account.
- **Enforcement is discretionary and informal**, in the same spirit as the repository's existing
  code of conduct — the operator is one person, and the document must not imply a moderation team
  or a formal process.
- **No age gate, by owner decision (FR-013).** The platform stays open to anyone and handles the
  question with a guardian-responsibility clause in the text. Two limits of that approach are
  accepted knowingly: under German law a minor's contract is provisionally void until a guardian
  actually approves it, so the clause does not bind a guardian who never saw it; and the clause
  is unverifiable by design, since the platform never learns anyone's age. It is defensible here
  chiefly because the privacy policy rests on contract and legitimate interest rather than
  consent, so GDPR Art. 8's verification duty for under-16s is not engaged. Recording an age
  would also mean collecting a new category of personal data about minors, which the current
  privacy policy does not cover — a further reason not to ask.
- **The published page is the only notice of change, by owner decision (FR-014).** Accepted
  trade-off: a member who agreed to an earlier version is not told when a later one appears.
  The version recorded at acceptance (FR-021) still names what they agreed to, so the platform
  can always tell the two apart even though it will not have asked them to re-agree.

## Dependencies

- Feature 036 (privacy policy and imprint) — supplies the document infrastructure, the
  authoritative-language rule, the link surfaces, and the release guards this feature extends.
- Feature 013 (admin area) — supplies the suspend/ban capability and its action record, which
  this feature grounds but does not modify.
- Feature 037 (account deletion) — determines how an acceptance record must behave when the
  account it belongs to is erased.
- Feature 031 (i18n) — supplies the language mechanism and the global fallback behaviour that
  FR-026 exists to defend against.
- Feature 003 (registration) — the flow this feature modifies.
