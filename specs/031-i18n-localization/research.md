# Phase 0 Research: i18n (German & Spanish)

All decisions below resolve the Technical Context and the spec's clarified requirements. No `NEEDS CLARIFICATION` remain.

## D1 — Frontend i18n mechanism

**Decision**: Use **`@jsverse/transloco`** (the maintained successor to `@ngneat/transloco`) as the runtime translation library.

**Rationale**: The spec mandates immediate, runtime language switching with no full reload (FR-004, SC-003) and an English fallback for missing keys (FR-008). Transloco loads JSON catalogs at runtime, switches the active language in place, supports **per-scope lazy catalogs** that map onto the Nx feature-folder structure, exposes a built-in missing-key handler (English fallback), and works with Angular 21 standalone + zoneless (signal-based `translateSignal`/`*transloco` directive; no zone dependency).

**Alternatives considered**:
- `@angular/localize` (build-time `$localize`) — **rejected**: compiles one bundle per locale; switching needs a reload and a separate deployed build per language, violating FR-004 and the "no per-language build" constraint.
- `@ngx-translate/core` — viable runtime alternative, but Transloco has first-class scoped/lazy catalogs and a stronger testing story, which matter given the ~20-feature surface.

**Follow-up**: pin the Transloco version verified against Angular 21.2 during the setup task; no code depends on internals.

## D2 — Runtime-switchable date/number formatting

**Decision**: Use **`@jsverse/transloco-locale`** pipes for dates/times/numbers.

**Rationale**: Angular's `LOCALE_ID` (and `DatePipe`/`DecimalPipe`) is fixed at bootstrap and does not change at runtime without a reload — incompatible with FR-004. `transloco-locale` provides locale-aware pipes that react to the active language, satisfying FR-009 while keeping the runtime-switch guarantee. Locale data for `de`/`es`/`en` is registered once.

**Alternatives considered**: `registerLocaleData` + `LOCALE_ID` with a reload on switch — rejected (breaks no-reload requirement).

## D3 — Catalog structure & fallback

**Decision**: **Per-feature scoped JSON catalogs** under `assets/i18n/` — a root/shared catalog for app chrome and `jh-*` primitives, plus one scope per feature area (`auth`, `onboarding`, `profile`, `teams`, `events`, `browse`, `chat`, `dashboard`, `notifications`, `settings`, `admin`, …). `en` is the source and the fallback language; the missing-key handler resolves any absent `de`/`es` key to the `en` value (FR-008), never blank/raw key.

**Rationale**: Scoped catalogs lazy-load with their feature (smaller initial payload, aligns with existing lazy routes) and keep translation files close to ownership. A single fallback language keeps SC-002 measurable.

## D4 — Effective-language resolution & persistence

**Decision**: A `LanguageService` exposes the **effective language** as a signal, computed by the fixed precedence in FR-007:

1. Signed-in user's `preferredLanguage` (from `/auth/me`), else
2. `localStorage` anonymous choice, else
3. `navigator.language` **base-matched** to a supported language (FR-003; e.g. `de-AT`→`de`), else
4. `en`.

On explicit switch: update the signal (UI updates immediately), set Transloco active lang, set `document.documentElement.lang` (FR-016); if signed in, `PUT` the preference and update the cached `/me`; if anonymous, write `localStorage`. On login, the account preference supersedes any local choice (FR-007 / clarification Q2). Changing language never signs the user out (FR-015).

**Rationale**: One authority for "what language are we in" keeps UI, formatting, `<html lang>`, and the outbound header consistent.

## D5 — Passing the caller's language to the backend

**Decision**: A `languageInterceptor` sets **`Accept-Language: <effective-language>`** on every `/api` request, where `<effective-language>` is the post-override value from `LanguageService`.

**Rationale / non-contradiction**: The clarification (Q3) rejected *reading the browser's raw `Accept-Language`* because it ignores an anonymous user's explicit switch. Here the **frontend sets** `Accept-Language` to the *effective* (post-override) language — this **is** "the request carries the effective language" (FR-012a). Using the standard header (rather than a bespoke field on every DTO) means pre-account flows (register / reset / resend) need **no DTO changes** and the backend uses idiomatic `RequestLocalization`. `Accept-Language` is not a Fetch-forbidden header, so the interceptor may set it.

**Interceptor order**: append `languageInterceptor` to the existing chain `[authInterceptor, retryInterceptor]`. It only adds a request header and never short-circuits, so it does not affect the 028 auth/retry ordering contract. Documented in `app.config.ts`.

**Alternatives considered**: a `language` field on each pre-account DTO — rejected (repetitive, misses non-auth calls); a custom `X-App-Language` header — rejected (reinvents `Accept-Language`, loses built-in middleware support).

## D6 — Backend request UI culture

**Decision**: `AddRequestLocalization` with supported cultures `[en, de, es]`, default `en`, using the `AcceptLanguageHeaderRequestCultureProvider`. Sets `CurrentUICulture` per request.

**Rationale**: Standard ASP.NET Core; only maps to the supported allowlist (unknown/region tags collapse or fall back to `en`), satisfying "never trust the client" (gate 3) and base-language matching (FR-003).

## D7 — Backend string localization (subjects + emitted copy)

**Decision**: Move email **subjects** (currently inline English in the `*EmailService` classes) and any other backend-emitted user-facing copy into **`.resx` resources** resolved via `IStringLocalizer`, keyed by message name with placeholders.

**Rationale**: `.resx` + `IStringLocalizer` is the idiomatic .NET approach, integrates with the request/recipient culture, and keeps subjects out of code.

## D8 — Email body localization

**Decision**: **Per-locale template folders**: `EmailTemplates/{culture}/<name>.html` (existing files move into `EmailTemplates/en/`). `LoadTemplateAsync` selects the folder for the resolved culture and **falls back to `en`** if a localized file is missing. The shared `header`/`footer`/`base-styles` also localize per folder (footer contains copy).

**Rationale**: Matches the existing "HTML template file + `{{VAR}}` replacement" pattern with the least churn; translators edit whole HTML files; missing translations degrade to English, never blank.

**Alternatives considered**: single template with every string pulled from `.resx` — rejected (heavy churn to the template engine for little gain over folder selection).

## D9 — Recipient vs request culture (critical correctness rule)

**Decision**: The culture used to render an email/notification depends on **who the content is for**:

- **Pre-account emails to the caller themselves** (verify, reset, resend) → the **request culture** (from the caller's `Accept-Language`), per FR-012a.
- **Emails/notifications to a specific recipient user** (welcome, password-changed, team news/role, invites to existing users, notification mirrors) → the **recipient's stored `PreferredLanguage`** (FR-012), resolved via a helper, **overriding** the ambient request culture for that send.
- **Emails to a raw address that is not a known user** (targeted invites to non-members) → look up a user by that email if one exists (use their pref), else **`en`**.

**Rationale**: Prevents the classic bug of localizing a notification to the *actor's* language instead of the *recipient's*. A small `ResolveRecipientCultureAsync` helper centralizes this; `*EmailService` methods that already take a `User` read `user.PreferredLanguage` directly.

## D10 — Preference storage & contract

**Decision**: Add nullable `User.PreferredLanguage` (BCP-47 base tag: `"en" | "de" | "es"`, `null` = unset → detect). Expose on `AuthUserDto` (`/auth/me`). Add `PUT api/v{n}/account/language` (auth required) accepting `{ "language": "de" }`, validated against the allowlist server-side, persisted via `ExecuteUpdateAsync` (sets `ModifiedDate`). Anonymous choice lives in `localStorage`.

**Rationale**: Minimal schema change; mirrors the thin-controller + service + DTO pattern (gate 1/2). See [data-model.md](./data-model.md) and [contracts/](./contracts/).

## D11 — Supported-language single source (FR-017 extensibility)

**Decision**: Define the supported set once per side — FE `supported-languages.ts` constant and BE `RequestLocalization` supported-cultures + the `PUT` validator — kept in parity and documented as **the** place to add a language. Adding a 4th language = add the code to both constants + supply its catalogs/`.resx`/email folder; no architectural change (SC-008).

**Rationale**: Keeps the extension point explicit and small; avoids an over-engineered runtime language registry endpoint for a 3-language app.

## D12 — String extraction strategy

**Decision**: Extract per feature area, one slice at a time: replace hardcoded text in each feature's `*.html` (and inline TS strings — validation messages, toasts, aria-labels) with translation keys under that feature's scope; author `en` from the current copy; **draft `de`/`es` via AI**, each catalog **flagged for native-speaker review** as a tracked fast-follow (clarification Q4). The English fallback keeps every screen fully rendered even mid-extraction.

**Rationale**: Independently shippable slices (aligns with the spec's prioritized stories), reviewable diffs, and no "big bang" rewrite. `/speckit-tasks` will sequence the slices.

## D13 — Testing approach

**Decision**:
- FE: Transloco testing harness in component specs; assert keys resolve and switching updates rendered text; `LanguageService` precedence unit tests; **zoneless — no `fakeAsync`** (project convention).
- BE: xUnit tests for culture resolution (base-matching, allowlist rejection, `en` fallback), recipient-vs-request culture selection (D9), and email language selection.
- e2e: Playwright — detect from browser, override via switcher, persistence across reload/sign-in, and a German long-string smoke on key screens.

**Rationale**: Locks the behaviors most prone to regression (fallback, recipient culture, persistence precedence).

## D14 — Accessibility & layout

**Decision**: `LanguageService` keeps `document.documentElement.lang` in sync with the active language (FR-016). Long-German-string tolerance is verified via the gate-7 `ui-review.md` checklist against key screens (SC-006); DESIGN.md governs switcher placement (reachable signed-out and signed-in per clarification Q2) and layout tokens.

**Rationale**: Screen-reader pronunciation correctness (FR-016) and the DESIGN.md compliance gate are both explicit spec requirements.
