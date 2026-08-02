# Feature Specification: Transactional Email Templates & Notification Preference Gating

**Feature Branch**: `039-transactional-email-templates`

**Created**: 2026-08-01

**Status**: Draft

**Input**: GitHub issue #109 — "Four transactional emails bypass the shared template — author the missing templates", extended during specification to include the notification-preference gating that makes the shared footer's promise true.

## Problem Context

Four outgoing emails are composed as inline HTML strings and never reach the shared
template layer. Each ends at a bare `— JuggerHub` sign-off:

| Email | Trigger |
|---|---|
| Event cancellation | An organiser cancels an event |
| Party request / nudge | A team opens a party for an event, or re-nudges a member |
| Party news | A party admin posts an update to the crew |
| Market invite | A party invites a free agent to play for them |

The split is not a judgement about these messages — it is "did the inherited starter kit
ship a template for this?". Where one happened to fit, the template path was used; where
none did, HTML was hand-rolled. As a result these four are unbranded, English-only
regardless of the recipient's chosen language, and — because they never consult the
notification preference system — unstoppable.

The project constitution's **Transactional Email** section requires base header/footer
templates "reused across all emails", with use-case templates extending them. These four
are the outstanding exceptions.

## Clarifications

### Session 2026-08-01

- Q: How should event cancellation be governed by the preference system, given it has no in-app counterpart today? → A: Add a new user-facing "Events" category with a real toggle (rather than making it always-on or folding it into "Invites & roster changes").
- Q: Should event cancellation also gain an in-app notification? → A: Yes — add a distinct cancellation notification type and fan it out alongside the email, which is what makes offering an Email toggle safe.
- Q: Should the privacy/imprint email footer links be delivered here or deferred? → A: Delivered here, in the shared footer, so every templated email gains them at once.
- Q: Does the literal-rendering requirement apply to subject lines as well as bodies? → A: Bodies only. Subjects are plain text, never interpreted as markup, so escaping them would show encoded entities in the recipient's inbox.
- Q: Party news currently emails the full post body while team news emails a 140-character excerpt. Which applies? → A: The same 140-character excerpt as team news, for consistency between the two news emails.
- Q: Should German and Spanish bodies for the four new templates be authored now, or fall back to English as invitation/team-news do? → A: Author all three languages now — the four new emails ship fully localized rather than relying on the English fallback.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every transactional email looks and reads like JuggerHub (Priority: P1)

A player receives a party request, a party news update, a market invite, or an event
cancellation notice. The email arrives with the same JuggerHub header, branding, address
block, and footer as the verification and password emails they already receive — and it is
written in the language they chose in their account settings, not in English by default.

**Why this priority**: This is the visible defect in #109 and the constitution violation.
Four of the platform's most-sent messages currently look like they came from a different,
less finished product. Recipients who set the product to German still get English mail.

**Independent Test**: Trigger each of the four emails against a recipient whose stored
language is German and inspect the captured message: it carries the shared header, footer,
address block, and a footer reason line, and its subject and body chrome are German.

**Acceptance Scenarios**:

1. **Given** a team member with no language preference set, **When** their team opens a
   party for an event, **Then** they receive a party-request email carrying the shared
   JuggerHub header, footer, address block, and a footer reason explaining why they got it.
2. **Given** a recipient whose stored language is German, **When** a party news post is
   published to their crew, **Then** the email subject and the shared chrome render in
   German rather than English.
3. **Given** an event with individual sign-ups and team sign-ups, **When** the organiser
   cancels it, **Then** every affected participant and team admin receives one branded
   cancellation email, and no recipient receives two.
4. **Given** a team whose name contains characters that are meaningful in markup (for
   example `<b>Ravens</b>`), **When** any of the four emails is sent, **Then** the name is
   displayed literally as typed and is not interpreted as formatting or as a link.

---

### User Story 2 - Notification preferences actually govern these emails (Priority: P2)

A player who has turned off the Email channel for a notification category stops receiving
those emails. The "Manage notifications" link in the email footer leads to a settings page
whose toggles genuinely control the mail they are reading.

**Why this priority**: Delivering User Story 1 alone would place a "Manage notifications"
link on four emails whose toggles do nothing — a worse outcome than the current unbranded
state, because it makes an explicit promise the product does not keep. This story is what
makes the footer honest.

**Independent Test**: Turn off the Email channel for a category in settings, trigger the
corresponding email, and confirm no message is delivered — while the in-app notification
for the same event still appears, proving the channels are independent.

**Acceptance Scenarios**:

1. **Given** a user who has disabled the Email channel for "Invites & roster changes",
   **When** their team opens a party or a party invites them via the marketplace, **Then**
   they receive no email for it.
2. **Given** that same user, **When** the same events occur, **Then** they still receive
   the in-app notification, because the In-app channel is separately enabled.
3. **Given** a user who has disabled the Email channel for "Team news", **When** a party
   news post is published to their crew, **Then** they receive no email for it.
4. **Given** a user who has never opened notification settings, **When** any of the four
   emails is triggered, **Then** they receive it — the absence of an explicit preference
   means enabled.
5. **Given** a fan-out to a mixed group where some recipients have disabled the channel,
   **When** the email is sent, **Then** only the enabled recipients are mailed and the
   others are silently skipped without failing the originating action.

---

### User Story 3 - An event cancellation is a first-class notification (Priority: P3)

When an event a player signed up for is cancelled, they see it in their notification list
alongside every other notification — not only in their inbox. In settings they can control
event notifications as their own category, separately from invites and team news.

**Why this priority**: Cancellation is the only one of the four with no in-app counterpart
at all, so it is the only one where a preference decision could leave a player with no
signal whatsoever. Making it a real notification type with its own category means the
Email toggle is safe to offer, because the in-app channel remains as a backstop.

**Independent Test**: Cancel an event with sign-ups and confirm each participant's
notification list gains a cancellation entry linking to the event, and that a new "Events"
row appears in notification settings with working In-app and Email toggles.

**Acceptance Scenarios**:

1. **Given** a player signed up for an event, **When** the organiser cancels it, **Then** a
   cancellation notification appears in the player's notification list and links to the
   event page.
2. **Given** a team signed up for an event, **When** the organiser cancels it, **Then** the
   team's admins receive the cancellation notification.
3. **Given** a user opening notification settings, **When** the page loads, **Then** an
   "Events" category is listed with its own In-app and Email toggles, labelled in the
   user's language.
4. **Given** a user who has disabled the Email channel for "Events", **When** an event they
   joined is cancelled, **Then** they receive no cancellation email but still see the
   in-app notification.

---

### User Story 4 - Legal links reachable from every email (Priority: P3)

A recipient of any JuggerHub email can reach the privacy policy and the imprint from the
message itself, without needing to sign in or hunt through the site.

**Why this priority**: The privacy and imprint pages exist but are not linked from email.
Because the links live in the shared footer, this story delivers them to every templated
email at once — including the four being migrated — rather than four times over.

**Independent Test**: Trigger any templated email and confirm the footer contains working
links to the privacy policy and the imprint, in each supported language.

**Acceptance Scenarios**:

1. **Given** any templated email in any supported language, **When** the recipient reads
   the footer, **Then** it contains links to the privacy policy and the imprint.
2. **Given** those links, **When** followed, **Then** they resolve against the same
   configured frontend host as every other link in the message.

---

### Edge Cases

- **Markup in user-supplied text.** Team names, event names, display names, and party news
  bodies are user-authored and appear inside emails. Today the four hand-rolled emails
  escape them; the shared template layer does not escape anything, so a member-authored
  news post can already inject markup into templated team-news mail. Migrating without
  addressing this would extend the exposure to four more messages. All user-supplied values
  MUST render literally.
- **Recipient has no language preference.** Falls back to the request language, then to
  English, matching existing behaviour.
- **Recipient's language has no translated body template.** The English body is used while
  the surrounding chrome renders in the recipient's language. After this feature that mixed
  result applies only to the pre-existing invitation and team-news emails (see #84) — the
  four emails introduced here are authored in all three languages and never fall back.
- **A translated template is missing a link or placeholder its English sibling has.** The
  fallback is per-file, not per-placeholder, so a German template that omits the
  call-to-action would silently ship a German email with no way to act on it. The three
  language variants of each template must stay structurally identical.
- **Preference lookup fails.** The preference system is fail-safe: on error the email is
  delivered rather than dropped. A preferences outage must never silently suppress mail.
- **Email delivery fails for one recipient in a fan-out.** The originating action (cancel
  the event, post the news) still succeeds; the failure is logged and the remaining
  recipients are still attempted.
- **A recipient appears twice in a cancellation fan-out** (signed up individually *and* an
  admin of a signed-up team). They receive exactly one email.
- **A user disables the Email channel while a fan-out is in progress.** Either outcome is
  acceptable; no guarantee is made about in-flight sends.
- **An unresolved placeholder reaches a recipient.** No email may ship with a visible
  unsubstituted template placeholder.

## Requirements *(mandatory)*

### Functional Requirements

#### Shared chrome and content

- **FR-001**: All four emails (event cancellation, party request/nudge, party news, market
  invite) MUST be composed from the shared email template system rather than from inline
  markup, so they carry the same header, branding, address block, and footer as existing
  templated mail.
- **FR-002**: Each of the four MUST supply a footer reason line stating why the recipient
  received that specific message.
- **FR-003**: Each of the four MUST present a primary call-to-action linking to the relevant
  page (the event, the party, or the invite), plus a plain-text fallback link for clients
  that do not render the button.
- **FR-004**: The four new email bodies MUST reuse the existing shared visual vocabulary and
  MUST NOT introduce new styling.
- **FR-005**: The party news email MUST present the post body as a distinct quoted excerpt,
  consistent with the existing team news email. The excerpt MUST be truncated to the same
  length as the team news excerpt (140 characters, with a trailing ellipsis when shortened)
  rather than carrying the full post body as it does today.

#### Safety of user-supplied content

- **FR-006**: All values substituted into an email **body** MUST be rendered literally by
  default, so user-authored text (team names, event names, display names, news bodies)
  cannot introduce markup, links, or formatting into a JuggerHub-branded message.
- **FR-007**: Where a value is intentionally markup authored by the product itself, it MUST
  be explicitly designated as such at the point it is supplied; designation MUST NOT be the
  default.
- **FR-008**: FR-006 MUST apply to all existing templated emails as well, closing the
  current exposure in team news, not only to the four being migrated.

#### Language

- **FR-009**: Each of the four emails MUST be rendered in the recipient's stored language
  preference, falling back to the request language and then to English.
- **FR-009a**: Each of the four emails MUST have an authored body in all three supported
  languages (English, German, Spanish), so that a recipient in any supported language
  receives a fully translated message rather than an English fallback body.
- **FR-010**: Subject lines for the four MUST be localizable rather than fixed English, and
  MUST support embedding the relevant team and event names. Subjects are plain text and MUST
  NOT be markup-escaped — FR-006 applies to bodies only, and escaping a subject would render
  encoded characters visibly in the recipient's inbox.
- **FR-011**: Resolving a recipient's language for a fan-out MUST NOT introduce a per-
  recipient lookup; the language MUST be obtained alongside the recipient data already read.

#### Preference gating

- **FR-012**: Before sending, each of the four emails MUST consult the recipient's Email
  channel preference for the category the message belongs to, and MUST NOT be sent when
  that channel is disabled.
- **FR-013**: Preference gating MUST be evaluated for the whole recipient set at once rather
  than per recipient inside the send loop.
- **FR-014**: A recipient with no stored preference MUST be treated as enabled.
- **FR-015**: A failure to resolve preferences MUST result in delivery, never in silent
  suppression.
- **FR-016**: Gating the Email channel MUST NOT affect the In-app channel for the same
  event, and vice versa.

#### Event cancellation as a notification

- **FR-017**: Event cancellation MUST become a distinct notification type that produces an
  in-app notification, delivered to the same recipients as the cancellation email.
- **FR-018**: The cancellation notification MUST link to the cancelled event's page and MUST
  render correctly in the notification list.
- **FR-019**: A new user-facing "Events" notification category MUST be added, with In-app
  and Email toggles, and the cancellation notification type MUST map to it.
- **FR-020**: The new category's label and description MUST be available in English, German,
  and Spanish, consistent with the existing categories.
- **FR-021**: Existing users MUST NOT need any stored preference migration for the new
  category — its absence means enabled.

#### Footer legal links

- **FR-022**: The shared email footer MUST link to the privacy policy and the imprint, in
  every supported language's footer.
- **FR-023**: Those links MUST be built from the same configured frontend host as every
  other link in the message.

#### Verification

- **FR-024**: Automated coverage MUST assert that a non-authentication email carries the
  shared footer chrome including the privacy and imprint links.
- **FR-025**: Automated coverage MUST assert that user-supplied markup arrives escaped.
- **FR-026**: Automated coverage MUST assert that none of the new emails ships an unresolved
  placeholder.
- **FR-026a**: Automated coverage MUST assert that the three language variants of each new
  template carry the same set of placeholders, so a translated file cannot silently omit the
  call-to-action or a required value.
- **FR-027**: Automated coverage MUST assert that a disabled Email channel suppresses the
  corresponding email while leaving the in-app notification intact.

### Key Entities

- **Notification type**: the discriminator describing what happened. Gains a new member for
  event cancellation, which drives the notification's icon, copy, and link target.
- **Notification category**: the user-facing settings row grouping one or more notification
  types under a single set of channel toggles. Gains a new "Events" member.
- **Notification preference**: a user's setting for one (category, channel) cell. Sparse —
  no stored row means enabled. Unchanged in shape; only newly consulted by four more
  producers and newly addressable for one more category.
- **Email template**: a per-language content file for one message, combined with the shared
  header, styles, and footer at send time. Gains four new members.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of transactional emails the product sends carry the shared header and
  footer — no message ends in a bare sign-off.
- **SC-002**: 100% of the four migrated emails render fully — subject, body, and chrome — in
  the recipient's chosen language, verified by triggering each of the four against a
  recipient in each of the three supported languages (12 combinations, zero English
  fallbacks).
- **SC-003**: A recipient who disables the Email channel for a category receives zero emails
  from that category, verified for each of the four messages.
- **SC-004**: A recipient who disables the Email channel still receives 100% of the
  corresponding in-app notifications.
- **SC-005**: Every notification the product produces is reachable from a settings toggle or
  is explicitly listed as always-on — no notification type is both unlisted and ungoverned.
- **SC-006**: User-supplied text containing markup renders literally in 100% of emails, with
  zero cases of injected links or formatting.
- **SC-007**: Zero emails ship with a visible unresolved placeholder.
- **SC-008**: The privacy policy and imprint are reachable in one click from any
  transactional email, in all three supported languages.
- **SC-009**: Cancelling an event still succeeds even when every email send fails, and the
  cancellation remains visible to participants in-app.

## Assumptions

- The in-app cancellation notification goes to the same recipient set as the cancellation
  email: individual sign-ups, plus the admins of any signed-up team, de-duplicated.
- The new "Events" category exposes both In-app and Email toggles, like every other
  togglable category, rather than being email-only.
- The new "Events" category covers event cancellation only for now. Other event-related
  messages may join it later; naming it "Events" rather than "Event cancellations" leaves
  that room without a future rename.
- German and Spanish bodies for the four new emails **are** authored here (FR-009a), so the
  four ship fully localized. This deliberately does not extend to the pre-existing
  invitation and team-news bodies, which remain English-only under #84 — the difference is
  that new content can be written translated from the start, whereas retrofitting the
  existing set is a separate review pass.
- German and Spanish copy for these emails is draft-quality pending the native-review pass
  (#77), consistent with how the existing notification category and auth email strings are
  treated.
- Recipients of these four emails are always registered users, so a stored language
  preference and a preference matrix always exist to consult. Raw-email invitations (team
  invites to non-users) are not in scope.
- The privacy and imprint pages already exist at stable public paths from feature 036 and
  need no change.
- Existing email fan-outs already tolerate per-recipient send failures without rolling back
  the originating action; that behaviour is preserved, not redesigned.

## Out of Scope

- Translating the **pre-existing** invitation and team-news email bodies into German and
  Spanish — tracked under #84. They remain English-only with the existing fallback. Only the
  four emails introduced by this feature are authored in all three languages (FR-009a).
- Removing the unused inherited boilerplate (access-request, unusual-login, and
  subscription-welcome generation, and the orphaned subscription-welcome template).
  A reasonable companion cleanup, but independent of this feature.
- Any change to how email is transported or to the delivery provider.
- Digest, batching, or frequency controls for notifications.
- Retention or automated cleanup of notifications.

## Dependencies

- **Feature 011 (notification preferences)** supplies the preference matrix, the category
  and channel model, and the recipient-filtering behaviour this feature consumes.
- **Feature 010 (notifications)** supplies the notification type model and the in-app
  notification list this feature extends.
- **Feature 031 (localization)** supplies the recipient-language resolution and the
  per-language template loading this feature relies on.
- **Feature 036 (privacy policy & imprint)** supplies the pages the new footer links target.

## Notes

Issue #109's proposed step 4 — "Delete `Services/Email/EmailLegalFooter.cs`" — has no
counterpart in this repository. That file does not exist in any branch; the branch it was
observed on carries no commits beyond the main line. Its intent (privacy/imprint links
reachable from email) is delivered here directly through the shared footer, which is why
User Story 4 exists.
