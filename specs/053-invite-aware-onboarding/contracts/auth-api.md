# Contract: auth API changes (053)

Two existing requests gain two **optional** fields. No endpoint is added, renamed, or re-shaped; no
response changes. Existing clients that omit the fields see byte-identical behaviour.

## `POST /api/v1/auth/register`

```jsonc
{
  "email": "…", "password": "…", "handle": "…",
  "acceptsTerms": true, "termsVersion": "…", "termsLanguage": "de",
  "inviteSlug": "berlin-jugger",          // optional; ^[a-z0-9]+(?:-[a-z0-9]+)*$, 3–30 chars
  "inviteToken": "Xy…43 base64url chars…" // optional; ^[A-Za-z0-9_-]{16,128}$
}
```

- Both fields or neither. A pair that fails validation is **dropped silently**; the registration
  proceeds and the response is the unchanged neutral `200 { message }` (or the unchanged specific
  refusals for terms / password / handle).
- The pair is **never stored** and the invitation is **never read or changed** by registration.
- Effect: when present and well-formed, the verification email's link carries it (below). That is
  the entire effect.

## `POST /api/v1/auth/resend-verification`

```jsonc
{ "email": "…", "inviteSlug": "…", "inviteToken": "…" }   // both optional, same rules
```

Same neutral `200` as today in every case.

## The verification link (emailed; composed by the server)

```text
{FrontendBaseUrl}/verify-email?userId={guid}&token={enc}
{FrontendBaseUrl}/verify-email?userId={guid}&token={enc}&inviteSlug={enc}&inviteToken={enc}
```

- The suffix appears only when the request carried a valid pair. Each part is URL-encoded on its own.
- The link never carries a path or URL beyond its own; the SPA composes `/join/{slug}/{token}` from
  the two parts after validating them again.

## Frontend query contract on `/verify-email`

Reads `userId`, `token` (as today) and, optionally, `inviteSlug`, `inviteToken`. When both parse, the
success state's sign-in button carries `returnUrl=/join/{slug}/{token}?action=accept` and the
resend form forwards the pair. Otherwise the page is unchanged.
