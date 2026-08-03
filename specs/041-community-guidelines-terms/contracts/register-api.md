# Contract: `POST /auth/register` — acceptance fields

**Endpoint**: `POST /auth/register` · `[AllowAnonymous]` (unchanged — no new anonymous surface)

No new endpoint is introduced. The existing registration endpoint gains three request fields and
two refusal outcomes.

---

## Request

```jsonc
{
  "email": "player@example.com",
  "password": "…",
  "handle": "some-handle",

  // NEW
  "acceptsTerms": true,          // the affirmative act
  "termsVersion": "2026-08-03",  // the version the client actually displayed
  "termsLanguage": "de"          // the translation it was displayed in
}
```

`RegisterRequest` is a **positional record**. Validation attributes go on the constructor
*parameters*, never on the generated properties — MVC reads parameter-level metadata for
positional records and throws otherwise. The file already carries this warning; the new fields
follow it.

| Field | Rules | Why it exists separately |
|---|---|---|
| `acceptsTerms` | required, must be `true` | The affirmative act, mapping 1:1 to the checkbox. Keeping it distinct from the version makes "acceptance was never given" and "acceptance was given against stale text" two different refusals with two different fixes |
| `termsVersion` | required, non-empty, must equal `TermsOptions.CurrentVersion` | Proves the client rendered the current document (research R1). The submitted value is **checked and discarded**; the row records the server's constant |
| `termsLanguage` | required, must be in the supported allowlist (`en`/`de`/`es`) | FR-020. Validated server-side against the same allowlist `PUT /account/language` uses — an unvalidated value would let a client write arbitrary text into an evidence row |

---

## Responses

| Condition | Status | Title | Notes |
|---|---|---|---|
| Accepted (or neutrally absorbed) | `200` | — | Unchanged neutral `MessageResponse` |
| `acceptsTerms` missing / `false` | `400` | `Terms not accepted` | FR-018 |
| `termsVersion` / `termsLanguage` missing or malformed | `400` | model validation | Standard `[Required]` handling |
| `termsLanguage` not in the allowlist | `400` | `Unsupported language` | |
| `termsVersion` ≠ current | `409` | `Terms have changed` | Detail asks the reader to reload and read the current version |
| Password policy | `400` | *unchanged* | |
| Handle invalid / taken | `400` / `409` | *unchanged* | |

Two new `RegisterStatus` values back these: `TermsNotAccepted` and `TermsVersionMismatch`.

### Ordering matters

**Terms validation runs first — before the password check, the handle resolution, and the
`FindByEmailAsync` lookup.** Two reasons:

1. **It must not interact with enumeration neutrality.** `RegisterAsync` deliberately returns a
   neutral `Accepted()` for several real failures so the response never reveals whether an email
   is registered. A terms refusal is not enumeration-sensitive — it depends only on values the
   caller sent — so returning it early keeps the two concerns from entangling. Folding a terms
   refusal into the neutral response would strand the user with a "check your email" message for
   an account that was never created.
2. **It is the cheapest check.** Two string comparisons, no database round-trip, no password hash.

### What is never logged

The refusal path logs the **outcome** (`TermsNotAccepted` / `TermsVersionMismatch`) and nothing
else. No email, no handle, no submitted values (Principle I, Principle VII's logging rule).

---

## Client payload

`frontend/apps/web/src/app/core/models/auth.models.ts` gains the same three fields on the
register payload type. The register component supplies:

- `acceptsTerms` — the checkbox control's value
- `termsVersion` — read from the loaded legal catalogue, **not** hard-coded in the component
- `termsLanguage` — the Transloco active language at the moment of submission

### Client-side behaviour

- Submit stays disabled until the checkbox is ticked (FR-017) — **usability only**. The server
  refusal in FR-018 is the boundary, and the integration tests exercise it by calling the endpoint
  directly rather than through the form.
- Submit also stays disabled while the catalogue has not loaded, and if it failed to load. A
  member must not be able to agree to a document the app could not fetch.
- A `409` is surfaced as a distinct, actionable message ("the terms have been updated — reload
  and read the current version"), never as the generic error string. This is the rolling-deploy
  and stale-cache path.
- Registration is a mutation and is **never** auto-retried on the browser hop (Principle VII).
  This is inherited from the existing interceptor and requires no new code — but it must not be
  "helpfully" added for the new failure modes.
