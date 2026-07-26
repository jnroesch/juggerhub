# Contract: Language Preference

Versioned under `api/v{version}` like the rest of the API. Auth via the existing httpOnly-cookie scheme.

## GET `/api/v{n}/auth/me` (delta)

Existing endpoint; response gains one field.

**Response `200` — `AuthUserDto`** (delta only):

```jsonc
{
  // …existing fields (id, email, emailConfirmed, onboardingCompleted, handle)…
  "preferredLanguage": "de"   // "en" | "de" | "es" | null  — null = not chosen (detect)
}
```

Client applies `preferredLanguage` as the top-precedence language source on load (FR-005/FR-007). `null` → fall through to local/browser detection.

## PUT `/api/v{n}/account/language`

Set the signed-in user's persisted language preference.

- **Auth**: required (JWT bearer cookie). `401` if unauthenticated.
- **Request body** — `UpdateLanguageRequest`:

  ```json
  { "language": "de" }
  ```

- **Validation** (server-side, never trust the client — gate 3):
  - `language` MUST be one of the supported allowlist (`en`, `de`, `es`) → otherwise `400` with a safe validation message.
- **Behavior**: persists `User.PreferredLanguage` via `ExecuteUpdateAsync` (stamps `ModifiedDate`). Idempotent. Does **not** affect the session/cookie (user stays signed in — FR-015).
- **Responses**:
  - `204 No Content` (or `200` echoing `{ "preferredLanguage": "de" }`) on success.
  - `400` invalid/unsupported language.
  - `401` not authenticated.

**Not retried across the browser hop** (Principle VII / gate 8): this is a user-initiated mutation; the client may re-issue on explicit user action only.

## Notes

- There is intentionally **no** anonymous variant of this endpoint — anonymous choice is stored client-side in `localStorage` (research D4/D10). On sign-in, the account value wins (FR-007).
- No endpoint is exposed to *list* supported languages; the set is a small parity constant on both sides (research D11). Adding one is a code change on both sides, not a runtime configuration (SC-008 extension path).
