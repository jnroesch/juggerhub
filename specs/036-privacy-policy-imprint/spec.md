# Feature Specification: Privacy Policy & Imprint

**Feature Branch**: `036-privacy-policy-imprint`

**Created**: 2026-07-31

**Status**: Draft

**Input**: GitHub issue #92 — "Privacy policy page (deferred from 033 analytics)". Split out of feature `033-umami-analytics`, where it was originally FR-010 and is now recorded in Out of Scope. The platform has no privacy policy, imprint, or data-protection page of any kind — no route, no component, no content. Analytics (033) is **merged and deployed to Dev and Prod**, so the disclosure gap the issue anticipated is live today, not hypothetical.

## Why this is urgent, not deferred

Issue #92 was written while 033 was unshipped and framed the exposure as future ("between 033 shipping and this landing"). That window is now open: commit `47288e6` deployed self-hosted Umami to Dev and Prod, and 033 FR-008 records page paths verbatim — including `/u/<handle>` and `/t/<slug>`. The analytics store therefore already holds which member profiles and team pages were viewed and when. Under EU data-protection law a URL containing a username is personal data, so the platform is processing personal data of EU visitors with **no privacy disclosure anywhere in the product**. This feature closes that.

The exposure is also broader than analytics. Every feature since 002 has added processing — email addresses, chat messages, home cities, uploaded images — none of it disclosed to the people it concerns.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find out what the platform does with my data, without an account (Priority: P1)

Someone who is considering registering — or a member who has already been measured — wants to know what JuggerHub collects, why, who it is shared with, how long it is kept, and how to object or get it deleted. They reach a privacy policy from any screen, including while signed out, read it end to end, and leave knowing what happens to their data and whom to contact about it.

**Why this priority**: This is the whole feature and the only part that closes the live legal exposure. Everything else refines it. The platform is authenticated-only (feature 026), so a policy behind the sign-in wall would fail its own purpose: the people most in need of it are the ones deciding whether to hand over an email address in the first place.

**Independent Test**: In a browser with no session, open the app, follow the privacy link from the signed-out landing/sign-in screen, and confirm the full policy renders without being redirected to sign-in. Confirm every category of data the platform actually processes (see FR-004) appears in it.

**Acceptance Scenarios**:

1. **Given** a visitor with no account and no session, **When** they follow the privacy link from any signed-out screen, **Then** the complete privacy policy is displayed without any prompt to sign in.
2. **Given** a signed-in member, **When** they look for the privacy policy, **Then** they can reach it from a persistently visible place without leaving the app or searching.
3. **Given** the privacy policy is open, **When** the reader looks for how their profile page views are measured, **Then** they find an explicit statement that page addresses are recorded verbatim and that addresses of member profiles and team pages therefore name the profile or team that was viewed.
4. **Given** the privacy policy is open, **When** the reader looks for how to exercise their rights, **Then** they find a named contact and a described route for access, correction, deletion, objection, and complaint to a supervisory authority.
5. **Given** the privacy policy is open, **When** the reader checks how current it is, **Then** a "last updated" date is shown.
6. **Given** a visitor arrives directly at the privacy address as a deep link (from an email, or a search result), **When** the page loads, **Then** it renders the policy rather than redirecting to sign-in.

---

### User Story 2 - Reach the operator's legal identity (Priority: P1)

A visitor — or a supervisory authority, or someone with a complaint — needs to know who operates JuggerHub: the responsible person, a postal address, and a way to make contact. They reach an imprint page from the same globally visible place as the privacy policy, without signing in.

**Why this priority**: Equal to US1 rather than below it. A German-operated site is legally required to carry an imprint (Impressum), it is required to be reachable easily and directly from any page, and its absence is directly actionable — unlike the privacy gap, this one has an established enforcement path. It is also the page the privacy policy must point at for the "who is responsible" answer, so the two cannot sensibly ship apart.

**Independent Test**: With no session, follow the imprint link from a signed-out screen and confirm the operator's legal identity and contact details render without a sign-in prompt, and that the page is reachable in at most two clicks from any screen in the app.

**Acceptance Scenarios**:

1. **Given** a visitor with no account, **When** they follow the imprint link, **Then** the operator's legal identity and contact details are displayed without a sign-in prompt.
2. **Given** any screen in the app, signed in or out, **When** the visitor looks for the imprint, **Then** it is reachable in no more than two clicks.
3. **Given** the imprint is open, **When** the reader looks for the data-protection contact, **Then** it is present or the page links to it in the privacy policy.
4. **Given** the privacy policy is open, **When** the reader looks for who is responsible for the processing, **Then** the responsible party is named there too, not only in the imprint.

---

### User Story 3 - Read the policy in my language (Priority: P2)

A German- or Spanish-speaking player — the German community being the platform's primary audience — opens the privacy policy or imprint and reads it in the language the rest of the app is showing them, or is told plainly which language version is the binding one.

**Why this priority**: The app already ships fully translated in English, German and Spanish (feature 031), so a legal page that suddenly reverts to one language is both a visible break in the product and, for the audience most affected, a disclosure they may not understand — which weakens the disclosure's purpose. It ranks below P1 because an accessible policy in one language closes far more exposure than no policy at all.

**Independent Test**: Switch the app to German, then Spanish, then English, and open both pages in each; confirm the legal text appears in that language, that the non-German versions state that German governs, and that no raw placeholder or blank section appears anywhere.

**Acceptance Scenarios**:

1. **Given** the app is set to German, **When** the privacy policy is opened, **Then** the authoritative German text is shown, with no untranslated interface chrome around it.
2. **Given** the app is set to Spanish or English, **When** either legal page is opened, **Then** that language's text is shown together with a visible statement that the German version governs in case of divergence.
3. **Given** either legal page in any language, **When** the reader looks for the other page, **Then** the cross-link between policy and imprint is present and stays within the active language.
4. **Given** any language is active, **When** either page is displayed, **Then** no raw translation key, blank section, or half-translated paragraph appears.

---

### User Story 4 - Object to being measured (Priority: P3)

A visitor who has read the policy and does not want their page views recorded finds, in the policy itself, a plainly described way to stop being measured, and it works.

**Why this priority**: Legitimate interest (Clarifications) is a basis the reader can object to, so this route is legally load-bearing rather than a courtesy — the policy must not describe a right it cannot deliver. It is P3 only because the mechanism already exists: 033 FR-007 records nothing for a visitor whose browser signals Do Not Track or Global Privacy Control, so this story is about disclosing and verifying it, not building it.

**Independent Test**: Follow the opt-out route exactly as the policy describes it, then browse several pages and confirm no analytics event was recorded for that session.

**Acceptance Scenarios**:

1. **Given** the privacy policy is open, **When** the reader looks for how to stop being measured, **Then** a concrete route is described in terms a non-technical reader can follow.
2. **Given** a visitor follows that route, **When** they then browse the app, **Then** no analytics event is recorded for them.
3. **Given** the policy describes an objection route, **When** it is tested end to end, **Then** its described effect matches the system's actual behaviour.

---

### Edge Cases

- **A member is signed in on a small screen.** The legal links must still be reachable — the mobile layout uses a fixed bottom navigation bar and has no footer today, so "put it in the footer" is not by itself an answer for mobile.
- **A reader is on a phone.** The pages are long-form text; they must remain readable and navigable at small widths without horizontal scrolling.
- **The policy is opened at a deep-linked section** (e.g. from an email pointing at the analytics section). Anchored navigation within a long document should land the reader in the right place.
- **A processing activity changes after the policy ships** (a new processor, a new data category, a retention change). The policy must be updatable and must carry a date that makes staleness visible; a policy that silently drifts out of date is worse than one that is visibly old.
- **A reader asks for their data or its deletion.** The platform has **no self-service export or account-deletion capability today** (verified: none exists in the backend or frontend). The policy must therefore describe a route that is actually honoured — a manual one — and must not promise a self-service control that does not exist.
- **A translated version and the authoritative version disagree.** The reader must be able to tell which one governs.
- **A screen-reader user reads the policy.** A wall of undifferentiated text is technically compliant and practically useless; headings and structure must be navigable.

## Requirements *(mandatory)*

### Functional Requirements

#### Reachability

- **FR-001**: Both the privacy policy and the imprint MUST be reachable **without an account and without a session**, notwithstanding the authenticated-only rule established by feature 026. These are the two documented exceptions to it, alongside the existing opt-in public profile.
- **FR-002**: Reachability differs by audience (Clarifications, 2026-08-01):
  - **Signed out** — both pages MUST be reachable in **one click from any screen**, desktop and mobile. This is the audience the requirement exists for: someone deciding whether to hand over an email address, and anyone who needs to identify the operator without holding an account.
  - **Signed in** — both MUST be reachable from the **account page**, and MUST NOT occupy every screen. A member has already made that decision; keeping the links in the chrome of every page is clutter for a document read once.
  - Both MUST remain reachable at any time by their stable address (FR-003), which is what a supervisory authority or an external link uses.
- **FR-003**: Each page MUST have its own stable address that can be linked to directly — from an email, an external site, or a supervisory authority's correspondence — and that address MUST render the page rather than redirect to sign-in.

#### Privacy policy content

- **FR-004**: The privacy policy MUST account for **every category of personal data the platform actually processes**, organised by **category of data and purpose rather than by product feature** (Clarifications, 2026-08-01). Nothing the platform processes may fall outside what the policy describes, but the description is written to absorb new features rather than enumerate current ones. The categories are at minimum:
  - **account and authentication** — email address, password (stored only as a hash), account status, and sign-in session records including the originating network address retained for security auditing (features 002, 013)
  - **transactional email** sent to the member's address (features 002, 028)
  - **what a member publishes on the platform** — profile, location, uploaded images, and participation in the community surfaces (features 003, 005–018, 026, 030, 034, 035). This category MUST be worded so a newly added way to take part is covered **without an edit**, and MUST still convey the **opt-in public-profile visibility model** and that community content is visible to other members.
  - **details a member supplies about other people**, who may have no account (`EventContact` stores a name, phone number and email address)
  - **messages** — that content is stored, is not end-to-end encrypted, and that conversations can outlive the team or event they belonged to (features 019, 022, 027)
  - **settings**, such as interface language and notification preferences (features 011, 031)
  - **analytics** as introduced by feature 033
- **FR-004a**: The policy MUST NOT name product features where a category would serve, and MUST NOT make **negative claims about the absence of a practice** (e.g. "no advertising network", "we do not load Google Fonts", "there is no self-service export") — except the no-consent-banner reasoning in FR-014, where the absence *is* the disclosure. Such statements are true only at the moment of writing and go silently false; a privacy policy that is quietly wrong is worse than one that is general. Durable **commitments** ("we don't sell your data") are permitted; snapshots of the current implementation are not.
- **FR-005**: For each category, the policy MUST state **what is collected, why, on what legal basis, how long it is kept, and who it is disclosed to** — at the granularity of the category, not of each feature within it.
- **FR-006**: The policy MUST disclose the **analytics processing as it actually behaves**, specifically: that it is self-hosted and sends nothing to a third party (033 FR-009); that it sets no cookie and stores nothing on the device (033 FR-006); that no identifier of the *viewer* is stored (033 FR-005); **and that page addresses are recorded verbatim (033 FR-008), so addresses naming a member profile or team page are recorded as such**. The last point MUST NOT be omitted or softened — it is the reason this feature exists.
- **FR-007**: The policy MUST name the **responsible party (controller)** and a contact for data-protection matters.
- **FR-008**: The policy MUST list the **third parties that process personal data on the platform's behalf**, at minimum the email delivery provider used in Dev and Prod (Resend) and the cloud hosting and object-storage provider (Microsoft Azure), and MUST state where that processing takes place and — where it leaves the EU — on what transfer basis.
- **FR-009**: The policy MUST describe the reader's **rights** — access, rectification, erasure, restriction, portability, objection, and complaint to a supervisory authority — and give a **route that is actually honoured**. Since no self-service export or deletion exists (#105), that route is a contact address. Per FR-004a the wording MUST be framed as what *does* happen ("write to us and we'll take care of it") rather than as an assertion that no control exists, so it stays true if one later ships. The policy MUST NOT point at a control that does not exist.
- **FR-010**: The policy MUST state the **legal basis relied on for analytics** and, if that basis is one the reader can object to, MUST describe the objection route concretely (see FR-013).
- **FR-011**: The policy MUST disclose the **cookies and device storage the application itself uses** — the authentication session cookie and any locally stored preference such as the anonymous language choice — and distinguish them from analytics, which stores nothing.
- **FR-012**: The policy MUST carry a **"last updated" date** that is visible on the page.

#### Consent and objection

- **FR-013**: The system MUST provide a working way for a visitor to **stop being measured**, described in the policy in non-technical terms. The existing Do Not Track / Global Privacy Control behaviour (033 FR-007) satisfies this if the policy states it plainly; any additional mechanism MUST be verified end to end rather than merely described.
- **FR-014**: The system MUST NOT introduce a consent banner (Clarifications, 2026-07-31). The policy MUST instead carry the reasoning that makes that correct, keeping the two questions distinct: (a) analytics writes nothing to the device and the authentication cookie is strictly necessary, so the storage-consent rule is not engaged; and (b) the processing of personal data in verbatim page paths rests on **legitimate interest**, with the balancing test written out in the policy — what the interest is, why the impact on the reader is limited, and how to object.
- **FR-014a**: Because the basis is legitimate interest, the objection route in FR-013 is not optional courtesy — the policy MUST describe it, and it MUST be verified to work end to end (SC-004).

#### Imprint content

- **FR-015**: The imprint MUST contain the information a German-operated site is required to carry: the operator's name, a postal address at which they can be reached, and an electronic contact route (at minimum an email address), plus any further particulars applicable to the operator's legal form.
- **FR-016**: The imprint MUST be a **distinct page** from the privacy policy, and each MUST link to the other.

#### Presentation and language

- **FR-017**: Both pages MUST be presented using a **long-form content treatment** that is defined for this feature and added to DESIGN.md, since none exists today. The treatment MUST cover measured line length, heading hierarchy for a multi-section document, list and emphasis styling, and link treatment within prose.
- **FR-018**: Both pages MUST be readable at mobile widths with **no horizontal scrolling**, and MUST use a semantic heading structure that a screen reader can navigate section by section.
- **FR-019**: The legal text MUST be published in all three supported languages, with **German as the authoritative version** and English and Spanish as informational translations (Clarifications, 2026-07-31). Every non-German version MUST carry a visible statement that the German version governs in case of divergence. The rule applies identically to both pages.
- **FR-020**: The pages MUST NOT introduce a visual style outside DESIGN.md, and the interface chrome around the legal text (navigation, language switcher, links) MUST remain fully translated in all supported languages regardless of the language treatment chosen for the legal text itself.

#### Maintenance

- **FR-021**: The legal content MUST be editable and shippable **without a data migration or an administrative interface** — it is versioned content that changes by deployment, not member-generated data. No new stored entity is introduced by this feature.

### Key Entities

None. This feature introduces **no new persisted data**. Its content is versioned application content, and its only interaction with stored data is to describe data that already exists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A visitor with no account can reach and read the complete privacy policy and the complete imprint, from a cold start, in **two clicks or fewer** from any screen, on both a desktop and a mobile viewport.
- **SC-002**: An audit that lists every category of personal data the platform stores, derived from the running system rather than from the policy, finds **zero categories absent from the privacy policy**.
- **SC-003**: The policy's description of analytics matches the deployed behaviour on **every point checked** — no cookie set, nothing stored on the device, no viewer identifier stored, page addresses recorded verbatim including profile and team addresses.
- **SC-004**: Following the objection route exactly as the policy words it results in **zero analytics events recorded** for that session.
- **SC-005**: Every third party that receives personal data in Prod appears in the policy — **zero undisclosed processors**.
- **SC-006**: Every right described in the policy has a route a reader can follow that reaches a person who will act on it; **zero rights are described via a control that does not exist**.
- **SC-007**: Both pages render without horizontal scrolling at 320 px width, and their heading structure is navigable section by section with a screen reader.
- **SC-008**: Both pages load for a signed-out visitor **without any authentication request being issued** and without a redirect to sign-in.
- **SC-009**: In every supported language, both pages render with **zero raw translation keys, zero blank sections, and zero half-translated paragraphs**.
- **SC-010**: After this feature ships, the period during which JuggerHub measures EU visitors with no privacy disclosure is **closed** — measurable as: the policy is live in Prod and names the FR-008 processing.

## Clarifications

### Session 2026-08-01

- **Q: Where do the legal links live for a signed-in member?** → A: **On the account page, not in the chrome of every screen** (FR-002). The app footer now renders for **anonymous visitors only**. The reasoning splits cleanly by audience: a signed-out visitor is the reader a privacy policy exists for and gets it in one click from anywhere; a member has already made that decision, and a document read once does not earn permanent space on every page.
  **Noted, and accepted by the owner**: the §5 DDG "easily and directly reachable" expectation for the imprint is usually read as a two-click ceiling. This still holds for the audience it protects — anyone without an account reaches it in one click, and the stable URL always works — but a signed-in member now needs three (avatar menu → Account → link). The exposure is minimal and the trade is deliberate.
- **Q: How specific should the policy be about individual features?** → A: **Organise by category of data and purpose, not by feature** (FR-004, FR-004a). The first implementation had one section per feature — profile, location, chat, participation, media, language — which meant every shipped feature silently dated a legally binding document in three languages, one of which is authoritative. Owner's judgement, and correct: the maintenance burden made staleness the likely outcome, and a privacy policy that is quietly wrong is worse than one that is general. The categories are worded to absorb new features ("teams, events, training, and whatever else the site grows to offer") so that adding a feature needs no edit here.
- **Q: Should the policy state what the platform does *not* do?** → A: **No** (FR-004a). Statements like "no advertising network", "no third-party analytics", "the fonts are not loaded from Google Fonts" and "there is no self-service export" were removed. Each was true when written and each would go false without anyone noticing — the worst failure mode for a legal document. Durable *commitments* stay ("we don't sell your data", "we don't pass it to anyone for advertising"), because those are promises rather than snapshots. The single exception is the no-consent-banner reasoning, where the absence of device storage **is** the disclosure and is what makes the missing banner correct.
- **The one thing that stays specific**: the verbatim page-path disclosure (FR-006). Genericising it to "we collect usage data" would hide precisely the processing this feature exists to reveal, so it is exempt from the rule above and is guarded by a test.

### Session 2026-07-31

Issue #92 identified three decisions as needing the owner rather than the codebase. Two are resolved; the third is open.

- **Q: What lawful basis covers the analytics processing, and is a consent banner required?** → A: **Legitimate interest, no consent banner.** The two questions stay separate and are answered separately: (a) *device storage* — analytics writes no cookie and nothing else to the device (033 FR-006), and the authentication cookie is strictly necessary, so the storage-consent rule is not engaged at all; (b) *lawful basis for the processing* — legitimate interest, with a balancing test documented in the policy itself, and the existing Do Not Track / Global Privacy Control handling (033 FR-007) as the objection route, described in plain language. A consent banner was rejected: it would suppress a large share of measurement, which is precisely the failure mode 033 self-hosted to avoid, and a click-through would obscure rather than convey the FR-008 exposure. Grouping profile and team paths was rejected as an amendment to an explicit owner decision (033 FR-008), and is not revisited here.
- **Q: How is the long-form legal text handled across the three supported languages?** → A: **German is authoritative; English and Spanish are published as clearly-labelled informational translations.** The site is German-operated with a German-majority audience, so the binding text is German. English and Spanish versions are published so the disclosure actually reaches the members it concerns, each carrying a visible statement that the German version governs in case of divergence. Publishing three equally-binding texts was rejected — every future edit would need three legally-reviewed versions and any drift becomes a real problem. Publishing one language only was rejected as failing the disclosure's purpose for a third of the audience.
- **Q: What are the operator's imprint particulars?** → A: **Resolved 2026-08-01.** Jan Niklas Rösch, Lattenkamp 12, 22299 Hamburg, Germany; contact and data-protection address `hello@juggerhub.com`. Operated by a natural person, so there is no legal form, register entry, or VAT identifier to state; the same person and address answer § 18 (2) MStV.

  The public-git-history concern raised at plan time (research R4) is **moot**: the owner confirmed, and inspection verified, that this exact address is **already committed** to this repository — it is the postal address in all three transactional email footers (`backend/EmailTemplates/{en,de,es}/footer.html`). Publishing it in the imprint adds no exposure that the repository did not already carry, so the committed-content decision stands with its one real objection removed.

  Two things this answer unlocked, both now done: the privacy policy **names the controller directly** rather than pointing at the imprint (FR-007, US2 acceptance scenario 4), and the **supervisory authority is named concretely** — Der Hamburgische Beauftragte für Datenschutz und Informationsfreiheit — instead of "the authority for the operator's state".

## Open Questions

None. All three clarifications are resolved.

## Assumptions

- **The disclosure gap is live in Prod today.** Verified from git history: 033 merged (`b38cee4`) and deployed (`47288e6`). This feature is remedial, not preventative.
- **The privacy policy and imprint ship together.** They are conventionally adjacent, the policy must point at the imprint for the responsible party, and the imprint is the one with an established enforcement path. Splitting them would leave the sharper exposure open.
- **No self-service data export or account deletion exists**, and building one is **out of scope** for this feature — verified by inspection of the backend and frontend. The policy therefore documents a manual route. A self-service capability should be raised as a separate issue; the policy's wording must not depend on it landing.
- **Legal content is static, translated application content** — not a database entity, not administratively editable. It changes by deployment, which is appropriate for content that changes rarely and must be reviewed before it changes.
- **A global footer (or an equivalent persistently reachable surface) is introduced by this feature.** None exists today, and the signed-out shell carries only a slim public bar. The exact placement — footer, account menu, mobile navigation, or a combination — defers to DESIGN.md, but the two-click requirement (FR-002) must hold in every layout state.
- **The content is drafted from the codebase for the technical facts and reviewed by the owner for the legal ones.** The data inventory (FR-004), the analytics behaviour (FR-006), the processor list (FR-008), and the cookie inventory (FR-011) are all verifiable against the running system and are drafted here. The legal characterisation — bases, retention periods, the imprint particulars — is the owner's, and this spec does not substitute for legal review.
- **Retention periods are stated as they are actually operated**, not aspirationally. Where no retention rule is currently enforced for a data category, the policy states the honest position rather than inventing a period the system does not apply. Any category where this reads badly is a signal to raise a retention issue, not to write a nicer sentence.
- **The self-hosted geocoder from feature 030 is not deployed** (verified: no such service in compose or infra), so no geocoding processor is disclosed. If it is deployed later, the processor list must be revisited.
- **Analytics is not amended by this feature.** 033's behaviour is disclosed as-is, per the Clarifications decision to keep FR-008 intact and rely on legitimate interest.
- **The legitimate-interest balancing test is written into the policy, not just asserted.** A basis stated without reasoning is not a defensible one, and the reader needs it to decide whether to object.

## Out of Scope

- Self-service data export or account deletion (no such capability exists; see Assumptions).
- Terms of service / house rules / community guidelines — adjacent, but a separate document with a separate purpose.
- A cookie-consent banner — decided against (Clarifications).
- Retention automation — actually enforcing deletion schedules. This feature discloses what happens; changing what happens is separate work.
- Amending feature 033's recording behaviour — grouping profile/team paths was considered and rejected (Clarifications).
- Administrative editing of legal content.

## Dependencies

- Feature 026 (authenticated-only access) — this feature adds two documented exceptions to its rule; the existing allowlist mechanism is the natural place for them.
- Feature 031 (i18n) — the surrounding interface, and any translated legal text, run through the existing translation mechanism.
- Feature 033 (analytics) — supplies the processing that must be disclosed, and the Do Not Track behaviour that FR-013 relies on.
- DESIGN.md — needs a long-form content treatment defined (FR-017); it has none.
