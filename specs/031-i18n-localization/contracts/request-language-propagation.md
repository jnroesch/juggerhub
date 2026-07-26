# Contract: Request Language Propagation

How the caller's **effective** language reaches the backend so backend-generated content (especially pre-account emails) is localized — without adding a field to every DTO.

## Outbound (frontend)

A `languageInterceptor` adds to **every** `/api` request:

```
Accept-Language: <effective-language>
```

where `<effective-language>` is the current value from `LanguageService` — i.e. the language after any explicit user override, not merely `navigator.language` (research D4/D5). This is the mechanism by which "the request carries the visitor's currently active (effective) language" (FR-012a) is satisfied.

- Applied to all API calls, including the unauthenticated pre-account flows (`/auth/register`, `/auth/forgot-password`, `/auth/reset-password`, `/auth/resend-verification`).
- `Accept-Language` is not a Fetch-forbidden header, so the interceptor may set it.
- Interceptor chain: `[authInterceptor, retryInterceptor, languageInterceptor]` — header-only, never short-circuits, so it does not alter the 028 auth/retry ordering contract.

## Inbound (backend)

`RequestLocalizationMiddleware` (supported: `en`, `de`, `es`; default `en`) maps `Accept-Language` to a supported culture and sets `CurrentUICulture` for the request. Unknown/region-only tags collapse to a base language or fall back to `en` (FR-003) — the header is untrusted input mapped only onto the allowlist (gate 3).

## Culture selection rules for generated content

The request culture is **not** always the culture used to render content. Selection (research D9):

| Content | Culture used |
|---------|--------------|
| Pre-account email to the caller (verify, reset, resend) | **Request culture** (caller's `Accept-Language`) — FR-012a |
| Email/notification to a specific recipient user (welcome, password-changed, team news/role, notification mirrors, invites to existing users) | **Recipient's stored `User.PreferredLanguage`**, overriding request culture — FR-012 |
| Email to a raw address not tied to a known user | Look up a user by that email → their preference; else **`en`** |
| Any string with no translation for the chosen culture | **`en`** fallback (FR-008) |
| In-app UI text and in-app notification copy | Active client language (rendered client-side; no backend involvement) — FR-011 |

A `ResolveRecipientCultureAsync(userIdOrEmail)` helper centralizes recipient resolution so services never accidentally localize to the actor's language instead of the recipient's.
