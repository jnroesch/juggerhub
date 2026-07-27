# Implementation Plan: Localization — German & Spanish (i18n)

**Branch**: `031-i18n-localization` | **Date**: 2026-07-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/031-i18n-localization/spec.md`

## Summary

Make the entire JuggerHub interface — player-facing **and** the admin area — available in English, German, and Spanish, with **runtime** language switching (no per-language build), auto-detection on first visit, and a persisted preference (per-user account when signed in, browser-local when anonymous). Backend-generated content is localized too: in-app notifications (already rendered client-side) and transactional emails (subjects + bodies). English is the universal source and fallback.

Technical approach: adopt **@jsverse/transloco** (+ `transloco-locale` for runtime-switchable date/number formatting) on the Angular frontend, extracting all hardcoded UI strings into per-feature JSON catalogs. A `LanguageService` resolves the effective language by a fixed precedence and drives Transloco's active language and `<html lang>`. An HTTP interceptor stamps `Accept-Language: <effective-language>` on every API call so the backend knows the *caller's* chosen language (this is the frontend passing the effective, post-override language — not "trusting the browser header"). On the backend, `RequestLocalization` sets the request UI culture; email **subjects** and other backend copy move to `IStringLocalizer` (`.resx`), and email **bodies** gain per-locale template folders (`EmailTemplates/{culture}/*.html`). Emails/notifications addressed to a *specific recipient* use that recipient's stored `PreferredLanguage`, not the ambient request culture. A single new nullable `User.PreferredLanguage` column and one `PUT` endpoint carry the persisted preference.

## Technical Context

**Language/Version**: Backend C# / .NET (ASP.NET Core, EF Core); Frontend TypeScript 5.9 / Angular 21.2 (zoneless, standalone), Node 22.

**Primary Dependencies**: FE — `@jsverse/transloco`, `@jsverse/transloco-locale`. BE — ASP.NET Core `RequestLocalizationMiddleware`, `IStringLocalizer` + `.resx` resources (no new NuGet package required).

**Storage**: PostgreSQL — new nullable `User.PreferredLanguage` column. Browser `localStorage` — anonymous language choice only (never a token).

**Testing**: Jest + `@jsverse/transloco` testing harness (FE component specs, zoneless — no `fakeAsync`, per project convention); xUnit (BE culture-resolution + email-language tests); Playwright (e2e language switch + persistence).

**Target Platform**: Web — Angular SPA served via nginx + same-origin REST API.

**Project Type**: Web application (frontend + backend).

**Performance Goals**: Language switch reflected in the UI in < 1s with **no full page reload** (SC-003); catalogs lazy-loaded per feature scope.

**Constraints**: English is always the fallback (never blank / raw key); no per-language build or redeploy (runtime switch); layouts tolerate ~+35% German string length without truncation/overflow; only base-language matching (region variants collapse to `de`/`es`); no RTL.

**Scale/Scope**: ~88 component templates + inline TS strings (validation messages, toasts, aria labels) across ~20 player-facing feature areas **and the full admin area**; ~11 email templates (subjects + bodies); 3 languages.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate | Assessment |
|---|------|------------|
| 1 | **Architecture** (thin controllers, DI'd services, Mapster DTOs) | ✅ Language `PUT` is a thin controller action delegating to a service; `AuthUserDto` gains `preferredLanguage` via the existing mapping. No new patterns. |
| 2 | **Data access** (pagination, projections, `ExecuteUpdateAsync` sets `ModifiedDate`) | ✅ Preference write is a single-row `ExecuteUpdateAsync` setting `ModifiedDate`; no new list endpoints; `PreferredLanguage` is a column on existing `User` (no new entity). |
| 3 | **Security / never-trust-the-client** | ✅ Server validates the submitted language against the supported allowlist; the `Accept-Language` header is treated as untrusted — `RequestLocalization` maps only to supported cultures and ignores anything else. Language is **not** an authorization input. No secrets/exceptions leak. |
| 4 | **Auth** (tokens httpOnly, never `localStorage`) | ✅ Only the *anonymous language choice* (a plain string) lives in `localStorage`; **no token** is ever stored there. The signed-in preference is server-side. Explicitly called out to preempt review. |
| 5 | **Conventions** (`.html`/`.css`/`.ts` separate; only `.ps1` scripts) | ✅ Translation catalogs are JSON **data** files (not logic/scripts); components keep separated files. No non-`.ps1` scripts added. |
| 6 | **Environment parity** | ✅ Identical catalogs, supported-culture list, and behavior across local/Dev/Prod; no per-env divergence. |
| 7 | **UI/Design compliance** | ⚠️ Ships UI (language switcher + long-string tolerance) → requires `specs/031-i18n-localization/checklists/ui-review.md` (from the template) verified against the diff; DESIGN.md governs switcher placement and layout tolerance. Tracked as a task. |
| 8 | **Resilience (Principle VII)** | ✅ No **new** outbound integration: the `Accept-Language` header rides existing calls; emails already flow through the 028 resilient sender. The language `PUT` is an ordinary browser-hop mutation and is **not** auto-retried. No new resilience surface. |

**Result**: PASS — no violations. Gate 7 produces a required checklist task (normal for UI work), not a deviation.

## Project Structure

### Documentation (this feature)

```text
specs/031-i18n-localization/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions & rationale
├── data-model.md        # Phase 1 — User.PreferredLanguage + DTO deltas
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/           # Phase 1 — API contract deltas
│   ├── language-preference.md
│   └── request-language-propagation.md
├── checklists/
│   ├── requirements.md  # (from /speckit-specify)
│   └── ui-review.md      # (created during implementation, gate 7)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
frontend/apps/web/src/
├── app/
│   ├── app.config.ts                         # provide Transloco + transloco-locale; register languageInterceptor
│   ├── core/
│   │   ├── i18n/
│   │   │   ├── language.service.ts            # effective-language signal, precedence, persistence, <html lang>
│   │   │   ├── transloco-http.loader.ts       # loads scoped JSON catalogs
│   │   │   ├── supported-languages.ts         # ['en','de','es'] single source (FR-017 extension point)
│   │   │   └── missing-handler.ts             # fall back to English (FR-008)
│   │   ├── interceptors/language.interceptor.ts   # stamps Accept-Language: <effective>
│   │   └── services/auth.service.ts           # reads preferredLanguage from /me; applies on login
│   ├── features/…                             # every feature's *.html/*.ts strings → translate keys
│   │   └── settings/language/                 # language switcher (also surfaced signed-out; see DESIGN.md)
│   └── shared/… (jh-* primitives)             # chrome/shared strings
└── assets/i18n/
    ├── en/  de/  es/                          # root/shared catalogs
    └── <scope>/{en,de,es}.json                # per-feature scoped catalogs

backend/
├── Entities/User.cs                           # + PreferredLanguage (nullable)
├── Data/Migrations/…                          # add PreferredLanguage column
├── Dtos/Auth/AuthUserDto.cs                   # + PreferredLanguage
├── Dtos/Account/UpdateLanguageRequest.cs      # { language }
├── Controllers/AccountController.cs (new)      # PUT api/v{n}/account/language  (thin)
├── Services/Account/…                         # LanguagePreferenceService (validate allowlist + persist)
├── Services/Localization/                      # culture resolution helpers (recipient vs request culture)
├── Resources/                                  # .resx: email subjects + backend-emitted copy
├── EmailTemplates/{en,de,es}/*.html            # per-locale bodies (en = existing files moved)
├── Services/EmailTemplateService/…             # LoadTemplateAsync picks {culture} folder, en fallback
├── Services/Email/*EmailService.cs             # subjects via IStringLocalizer; resolve recipient culture
└── Program.cs                                   # AddRequestLocalization(en,de,es; default en)
```

**Structure Decision**: Existing **web application** layout (`frontend/` Angular + `backend/` ASP.NET Core). No new project. The change is broad (touches nearly every template) but architecturally shallow: one column, one endpoint, one FE service + interceptor, plus catalog files and per-locale email assets.

## Complexity Tracking

No constitution violations — no entries.

*(Note: the large string-extraction surface is scope volume, not architectural complexity; it is sequenced per feature in `/speckit-tasks`.)*
