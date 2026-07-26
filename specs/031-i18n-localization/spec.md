# Feature Specification: Localization — German & Spanish (i18n)

**Feature Branch**: `031-i18n-localization`

**Created**: 2026-07-26

**Status**: Draft

**Input**: GitHub issue #77 — "Localization: support German and Spanish (i18n)". JuggerHub is currently English-only; the goal is to make the whole app usable in English (`en`), German (`de`), and Spanish (`es`) for the wider — especially German-speaking — Jugger community.

## Clarifications

### Session 2026-07-26

The three open questions raised in issue #77 were resolved with reasonable defaults and are recorded here (and in Assumptions). They can be revisited via `/speckit-clarify`.

- Q: Runtime language switching vs. a separate build/deploy per language? → A: **Runtime switching** — a language change takes effect immediately in the running app, without a page reload and without a separate per-language build or deployment. (The issue itself flagged this as desirable.)
- Q: Where does the language preference live — account setting, browser only, or both? → A: **Both**, with a fixed precedence: an explicit choice stored on the signed-in user's account is authoritative across devices; for anonymous/first-time visitors the choice is stored locally in the browser; absent any choice, the browser's language preference is used; the final fallback is English.
- Q: Localize backend-generated content (emails, in-app notifications) now, or split into a follow-up? → A: **Now, in this feature.** Persisting the user's language is required regardless, and both in-app notifications and transactional emails are user-facing surfaces the community will judge the app by. User-generated content is explicitly excluded.
- Q: Is the admin/operator area (catalogue, user/team management, overview) in scope for translation, or player-facing surfaces only? → A: **Everything is in scope** — the full admin area is translated into all three languages alongside the player-facing surfaces. There is no English-only region of the interface.
- Q: How does a not-signed-in visitor reach the language switcher (US2 needs anonymous choice)? → A: The language control MUST be reachable in **both states** — a globally visible control for signed-out visitors (e.g. footer and/or the auth screens) and via the account/settings + menu when signed in. Exact placement defers to DESIGN.md.
- Q: What language are pre-account emails (registration verification, password reset, resend) sent in, before any preference is stored? → A: The frontend MUST pass the visitor's **currently active (effective) language** — which already reflects any manual override — on those unauthenticated requests, and the email is sent in that language (English fallback if absent/unsupported). Reading the `Accept-Language` header instead was rejected because it would ignore an anonymous user's explicit switcher choice and send a mismatched-language email.
- Q: What is the translation-quality bar for shipping (who produces de/es text)? → A: **Draft-then-review** — the feature ships with 100% coverage in all three languages, where the initial German/Spanish catalogs may be machine/AI-drafted, and a native-speaker review pass is committed as a tracked fast-follow. English fallback guarantees nothing renders blank at any point.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the app in my language automatically (Priority: P1)

A German-speaking (or Spanish-speaking) player opens JuggerHub for the first time. Because their device/browser is set to that language, the entire interface — navigation, buttons, forms, validation messages, empty/loading/error states, and in-app notifications — is presented in that language. Dates and numbers appear in the format they expect. If their language is not one of the supported ones, they see English.

**Why this priority**: This is the core of the feature and the minimum that delivers value: the app becomes usable to a non-English speaker end-to-end. Everything else (explicit choice, persisted preference, localized emails) refines this. It is independently valuable and testable on its own — even without a switcher, auto-detection alone opens the app to the German community.

**Independent Test**: Set a browser to German, then to Spanish, then to an unsupported language (e.g. French); load the app in each case and confirm the interface renders fully in German, fully in Spanish, and in English respectively, with locale-appropriate dates/numbers and no leftover English (or raw placeholder) text in the supported languages.

**Acceptance Scenarios**:

1. **Given** a first-time visitor whose browser language is German, **When** they open the app, **Then** the interface, including in-app notifications, is shown in German.
2. **Given** a first-time visitor whose browser language is Spanish, **When** they open the app, **Then** the interface is shown in Spanish.
3. **Given** a visitor whose browser language is not supported (e.g. French), **When** they open the app, **Then** the interface is shown in English.
4. **Given** a visitor whose browser reports a regional variant (e.g. `de-AT` or `es-MX`), **When** they open the app, **Then** it is shown in the matching base language (German / Spanish).
5. **Given** any supported language is active, **When** a date, time, or number is displayed, **Then** it is formatted according to that language's locale conventions.
6. **Given** a piece of text has no translation for the active language, **When** it is displayed, **Then** the English text is shown (never a blank or a raw key).

---

### User Story 2 - Choose and keep my language (Priority: P2)

A player wants the app in a language other than the one auto-detected — for example an English browser but a German player, or a shared machine. They open a language control, pick their language, and the interface updates immediately without losing where they were. Their choice is remembered: a signed-in player gets it on every device they sign in to; an anonymous visitor gets it on the next visit from the same browser.

**Why this priority**: Auto-detection (US1) covers most people, but explicit control and persistence are what make the experience feel intentional and correct on shared devices, mismatched browser settings, or when a player simply prefers another language. It builds directly on US1.

**Independent Test**: With US1 in place, open the language control, switch language, and confirm the UI updates in place; then reload / sign out and back in / open another session and confirm the chosen language is retained per the precedence rules.

**Acceptance Scenarios**:

1. **Given** the app is loaded in one language, **When** the player selects a different supported language from the language control, **Then** the visible interface updates to that language immediately, without a full page reload and without navigating them away from their current screen.
2. **Given** a signed-in player who selected a language, **When** they sign in later on a different device, **Then** the app is shown in their chosen language.
3. **Given** an anonymous visitor who selected a language, **When** they return later in the same browser, **Then** the app is shown in their chosen language.
4. **Given** a player who selected a language while signed out, **When** they sign in to an account that has its own stored language preference, **Then** the account's preference takes effect.
5. **Given** the language control, **When** a player views the available options, **Then** each language is labelled in its own name ("English", "Deutsch", "Español") and the current language is clearly indicated.
6. **Given** a player changes language, **When** the change is applied, **Then** they remain signed in and are not forced to re-authenticate.

---

### User Story 3 - Receive emails and notifications in my language (Priority: P3)

A player who has set (or been detected as) German or Spanish receives transactional emails (verification, password reset, invitations, team news, etc.) written in that language, and sees in-app notifications in that language. Content that other users wrote (news bodies, chat, profile free-text) stays in whatever language it was written.

**Why this priority**: Emails and notifications are user-facing touchpoints that betray an "English-only" app even when the UI is translated, and the German community will notice. It depends on the preference existing (US2) and is the last piece to make the localization feel complete. In-app notification copy is part of the interface and largely arrives with US1; localized emails are the distinct backend-generated surface this story adds.

**Independent Test**: With a player whose stored/preferred language is German (then Spanish), trigger a transactional email and an in-app notification, and confirm both arrive in the expected language; confirm a player with no stored preference receives English.

**Acceptance Scenarios**:

1. **Given** a player whose preferred language is German, **When** the system sends them a transactional email, **Then** the email content is in German.
2. **Given** a player whose preferred language is Spanish, **When** an in-app notification is generated for them, **Then** the notification is presented in Spanish.
3. **Given** a recipient with no stored language preference (or an unsupported one), **When** an email is sent, **Then** it is sent in English.
4. **Given** any content authored by another user (team news, chat, profile text), **When** it is shown or emailed, **Then** it appears exactly as authored and is not translated.

---

### Edge Cases

- **Regional variants**: `de-AT`, `de-CH`, `es-419`, `es-MX`, etc. collapse to the base supported language (`de` / `es`). Region-specific dialect catalogs are not provided.
- **Unsupported browser language**: falls back to English.
- **Preference conflict on sign-in**: the signed-in account's stored preference wins over a locally stored (anonymous) choice.
- **Missing/late translation**: an untranslated string falls back to English rather than showing blank or a raw key; this must hold for strings added after initial translation.
- **Email before a preference exists**: pre-account emails (registration verification, password reset, resend) use the active language passed with the request (FR-012a); if none is supplied — e.g. a system-triggered send, or an account predating this feature — they fall back to English.
- **Very long German strings**: buttons, navigation, chips/badges, table headers, and other tight UI must not truncate, overlap, or overflow.
- **Switching language mid-form**: changing language must not discard data the user has already entered where technically avoidable, and must not sign them out.
- **Number/date formatting**: decimal/thousand separators and date order differ by locale and must follow the active language.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All user-facing interface text MUST be available in English, German, and Spanish; no user-facing UI string may be permanently locked to a single language. This includes the admin/operator area (catalogue, user/team management, overview, and detail screens) — there is no English-only region of the interface.
- **FR-002**: On first visit (no stored choice), the system MUST detect the visitor's preferred language from their browser and present a supported match; otherwise it MUST default to English.
- **FR-003**: Regional/variant language tags MUST be matched to their base supported language (e.g. `de-AT` → German, `es-MX` → Spanish).
- **FR-004**: Users MUST be able to select their language from a visible language control, and the selection MUST take effect immediately, without a full page reload and without navigating them off their current screen.
- **FR-004a**: The language control MUST be reachable both when signed out (via a globally visible location and/or the authentication screens) and when signed in (via the account/settings area and the account menu), so anonymous visitors can choose and persist a language before authenticating. Exact placement is governed by DESIGN.md.
- **FR-005**: A signed-in user's selected language MUST be persisted to their account and applied wherever they are signed in.
- **FR-006**: For users who are not signed in, the selected language MUST persist locally so it is retained on return visits in the same browser.
- **FR-007**: Language resolution precedence MUST be: explicit account setting (when signed in) → locally stored choice → browser preference → English default.
- **FR-008**: When a string has no translation for the active language, the system MUST fall back to the English text (never a blank string or a raw key).
- **FR-009**: Dates, times, and numbers displayed in the UI MUST be formatted according to the active language's locale conventions.
- **FR-010**: Layouts MUST accommodate longer translated text (notably German) on all key screens without truncation, overlap, or overflow.
- **FR-011**: In-app notifications MUST be presented in the user's active language.
- **FR-012**: Transactional and notification emails MUST be sent in the recipient's stored preferred language, defaulting to English when none is stored or the language is unsupported.
- **FR-012a**: For pre-account emails triggered before a preference exists (registration verification, password reset, verification resend), the request MUST carry the visitor's currently active (effective) language — which reflects any manual override, not merely the browser setting — and the email MUST be sent in that language, falling back to English if it is absent or unsupported.
- **FR-013**: User-generated content (team news, chat messages, profile free-text, event/party descriptions, etc.) MUST NOT be translated and MUST be displayed/sent exactly as authored.
- **FR-014**: The language control MUST label each option in its own language (endonym) and clearly indicate the currently active language.
- **FR-015**: Changing language MUST NOT sign the user out or require re-authentication.
- **FR-016**: The active interface language MUST be exposed to assistive technologies (i.e. the page's language is announced correctly) so screen readers pronounce content appropriately.
- **FR-017**: The set of supported languages MUST be extensible so additional languages can be added later without re-architecting the localization mechanism.
- **FR-018**: The default and fallback language for the entire system (UI, notifications, emails) MUST be English.

### Key Entities *(include if feature involves data)*

- **User Language Preference**: the language a signed-in user has explicitly chosen, stored on their account. One of the supported languages, or unset (meaning "not chosen" → fall back to detection). Read when rendering the app for that user and when generating their emails/notifications.
- **Anonymous Language Choice**: the language chosen by a visitor who is not signed in, retained locally in their browser; superseded by an account preference on sign-in.
- **Translation Catalog**: the collection of translated UI strings for each supported language, keyed so any string can be resolved for any supported language, with English as the source and fallback.
- **Localized Email Content**: the per-language version of each transactional/notification email, selected by the recipient's preferred language with an English fallback.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A German-speaking user can complete every core journey — register, verify, onboard, create/join a team, browse and join events, chat — entirely in German, with no English UI text appearing except content authored by other users.
- **SC-002**: 100% of user-facing UI strings resolve in all three languages; no raw keys or untranslated placeholders are visible in normal flows.
- **SC-003**: Switching language via the control updates the visible interface within 1 second and without a full page reload.
- **SC-004**: A signed-in user's language choice is retained across sign-out/sign-in and across devices; an anonymous user's choice is retained across return visits in the same browser — in 100% of cases per the precedence rules.
- **SC-005**: Transactional emails are delivered in the recipient's chosen language for 100% of supported languages, with English used when no preference is stored.
- **SC-006**: On the designated key screens, no truncation, overflow, or overlapping text occurs when displayed in German (verified against the UI review checklist).
- **SC-007**: Dates, times, and numbers display in locale-appropriate format for each supported language on 100% of screens that show them.
- **SC-008**: Adding a fourth language later requires only supplying its catalog and registering it — no changes to feature architecture (demonstrated by a documented, catalog-only path).

## Assumptions

- **Supported languages** for this feature are English (source/default), German, and Spanish. Additional languages are out of scope but the mechanism must not preclude them (FR-017).
- **Runtime switching** (immediate, no per-language rebuild/redeploy) is the required model, chosen over a build-per-language approach; the specific mechanism is an implementation decision for the plan.
- **Preference storage is both** per-user (account setting, authoritative when signed in) and local (for anonymous/first visit), with the precedence in FR-007.
- **Backend-generated content is in scope**: in-app notifications and transactional emails are localized in this feature, not deferred.
- **User-generated content is never translated** — it is shown/sent in the language the author wrote it.
- **Base-language matching only**: regional/variant tags collapse to the base language; region-specific dialect catalogs are out of scope.
- **No right-to-left languages** are in scope (en/de/es are all left-to-right); RTL layout support is not addressed by this feature.
- The existing **account/settings** system is the home for the stored preference, and the existing **email and notification** systems are extended to produce localized output — no new subsystems are introduced for those.
- Accounts created before this feature (and system emails sent before a preference is captured) default to English until a preference is set.
- **Translation quality bar is "draft-then-review"**: the feature ships with 100% string coverage in all three languages, where the initial German/Spanish catalogs may be machine/AI-drafted; a native-speaker review pass is committed as a tracked fast-follow (see [issue #77](https://github.com/jnroesch/juggerhub/issues/77)). The English fallback ensures nothing renders blank even for a string awaiting review.

## Dependencies

- The existing user **account/settings** capability, extended to store and update the language preference.
- The existing **transactional email** and **in-app notification** capabilities, extended to render in the recipient's language.
- **DESIGN.md** governs the appearance and placement of the language control and the layout tolerance for longer strings; any conflict is resolved in DESIGN.md's favour.
