# Phase 1 Data Model: i18n

This feature adds **one persisted field** and a few DTO deltas. No new entity.

## Entity change: `User`

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `PreferredLanguage` | `string` (BCP-47 base tag) | **Yes** | `"en" \| "de" \| "es"`, or `null` = not chosen. `null` means "resolve by detection" (FR-007). Set by the user via the language `PUT`; read when rendering the app for that user and when localizing emails/notifications addressed to them (FR-012). |

- Lives on the existing `User` entity (derives `BaseEntity`); the audit interceptor stamps `ModifiedDate` on update (gate 2).
- **Validation** (server-side, gate 3): value must be in the supported allowlist (`en`/`de`/`es`); anything else is rejected (400). `null`/unset is valid (means "detect").
- **Migration**: add nullable `PreferredLanguage` column to `Users`. No backfill — existing rows stay `null` and fall back to detection/English (spec Assumptions). No data migration otherwise.

### State / lifecycle

- `null` (unset) → chosen value, via `PUT /account/language`. Reversible (a future "reset to automatic" could set it back to `null`; not required by this feature).
- Changing the value takes effect on the next `/me` hydration and immediately in the client that made the change; it never affects already-sent emails.

## Conceptual (non-persisted) models

These are not database entities but are part of the feature's data shape:

- **Anonymous Language Choice** — a supported language string stored in browser `localStorage` (key e.g. `jh.lang`). Superseded by the account `PreferredLanguage` on sign-in (FR-007). Never a token (gate 4).
- **Translation Catalog** — per-scope JSON files (`assets/i18n/**`) keyed by string id; `en` is the source and fallback. Data files, not schema.
- **Localized Email Content** — per-culture HTML under `EmailTemplates/{culture}/` plus subject strings in `.resx`; selected by the resolved recipient/request culture with `en` fallback (research D8/D9).

## DTO deltas

| DTO | Change |
|-----|--------|
| `AuthUserDto` (`GET /auth/me`) | **+ `preferredLanguage: string \| null`** — lets the client apply the stored preference on load (FR-005/FR-007). |
| `UpdateLanguageRequest` (new) | `{ "language": "en" \| "de" \| "es" }` — body of the language `PUT`. |
| Frontend `AuthUser` model | **+ `preferredLanguage: string \| null`** to mirror `AuthUserDto`. |

Pre-account request DTOs (`RegisterRequest`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `ResendVerificationRequest`) are **unchanged** — the caller's language rides the `Accept-Language` header set by the interceptor (research D5), not a body field.

## Supported-language set (extension point)

`["en", "de", "es"]`, defined once on each side (FE `supported-languages.ts`; BE `RequestLocalization` supported cultures + the `PUT` validator), kept in parity (research D11). `en` is the default and universal fallback (FR-018).
