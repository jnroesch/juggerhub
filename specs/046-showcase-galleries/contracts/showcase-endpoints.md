# Contract: Showcase gallery endpoints

**Feature**: 046 | **Spec**: [../spec.md](../spec.md) | **Model**: [../data-model.md](../data-model.md)

Ten endpoints: five per surface, the same five shapes. Nothing existing changes shape. All paths are
under the existing `/api/v1` prefix and live on the existing `ProfilesController` and
`TeamsController` (Principle II — no new controller for a subresource of an existing one).

Throughout: **every refusal to serve an image is `404`**, whether the cause is "no such image",
"not permitted", or "the store did not answer". This is `GetAvatar`'s existing rule and spec FR-023 —
the endpoint must never become an oracle for whether a member, a team, or a picture exists.

---

## Profile surface

### `GET /api/v1/profiles/{handle}/showcase`

| Part | Value |
|---|---|
| Auth | `[AllowAnonymous]`; the **service** applies the feature-026 visibility gate |
| `handle` | Normalised server-side via `HandlePolicy.Normalize` |
| Query | **None** — the collection is capped at 5 (see [Complexity Tracking](../plan.md#complexity-tracking)) |

| Status | When | Body |
|---|---|---|
| `200` | Profile is visible to this caller | `ShowcaseImageDto[]`, **0–5 items**, ordered |
| `404` | No such profile, profile is private and caller is anonymous, or owner is banned | Problem details, identical in all three cases |

```json
[
  { "id": "01920f3e-....", "caption": "Tempelhofer Feld, first tournament", "position": 0 },
  { "id": "01920f41-....", "caption": null,                                  "position": 1 }
]
```

No object key, no URL, no size, no content type. The client builds the image address itself:
`/api/v1/profiles/{handle}/showcase/{id}/image`.

### `GET /api/v1/profiles/{handle}/showcase/{imageId}/image`

| Part | Value |
|---|---|
| Auth | `[AllowAnonymous]` + the same visibility gate, applied **before** the store is touched |
| Rate limit | `RateLimitPolicies.MediaRead` (existing, 300/min, IP fallback for anonymous) |

| Status | When | Body |
|---|---|---|
| `200` | Permitted | `image/webp` bytes, via `MediaResponse.File` |
| `304` | Caller's `If-None-Match` matches | empty |
| `404` | Any refusal (see above) | empty |

Headers come from `MediaResponse.File` unchanged: `Cache-Control: private, no-cache` and an `ETag`
that is a **hash** of the object key, never the key.

### `POST /api/v1/profiles/me/showcase`

| Part | Value |
|---|---|
| Auth | Authenticated; acts on the caller's own profile only |
| Body | `multipart/form-data`, field `file` (`IFormFile`) — same as `PUT /profiles/me/avatar` |
| Limits | `[RequestSizeLimit(8 MB)]`; `RateLimitPolicies.MediaUpload` (**new**, 20/min per user) |

| Status | When | Body |
|---|---|---|
| `201` | Stored | The created `ShowcaseImageDto` |
| `400` | Processing refused — not an image, unsupported type, too large, too many pixels, unreadable | Problem details with a **non-technical** reason |
| `409` | Gallery already holds 5 | Problem details naming the limit — distinct from `400` so the client can say "you already have five" |
| `404` | Caller has no profile | Problem details |
| `503` | Media store did not accept the object | Generic problem details; nothing stored, no row written |

### `PATCH /api/v1/profiles/me/showcase/{imageId}`

Body `{ "caption": "…" | null }`, max 120 chars. `204` on success, `404` when the image is not the
caller's, `400` when the caption is too long.

### `DELETE /api/v1/profiles/me/showcase/{imageId}`

`204` on success. `404` for **every** other case — no such image, already deleted, or belongs to
someone else — deliberately indistinguishable. Remaining positions are compacted before the response
returns.

> **Changed during implementation.** This contract first specified an idempotent `204` for an
> already-deleted image. That would have made the response distinguish "an id that used to be yours"
> from "an id that is not yours", which is exactly the kind of difference FR-023 exists to remove —
> for one status code's worth of convenience. A repeated delete now answers `404` like everything
> else, and the client treats it as "already gone" rather than needing the server to say so.

### `PUT /api/v1/profiles/me/showcase/order`

Body `{ "imageIds": ["…", "…", "…"] }` — the **complete** new order.

| Status | When |
|---|---|
| `204` | Applied in full |
| `409` | Not an exact permutation of the caller's current images (wrong length, duplicate, stranger, or one was deleted meanwhile) — **nothing written**, client reloads |

---

## Team surface

Identical five shapes under `/api/v1/teams/{slug}/showcase…`, with these differences:

| Aspect | Team surface |
|---|---|
| Auth on reads | **Signed-in required** — `TeamsController` is `[Authorize]` at class level and gains no `[AllowAnonymous]`. There is no anonymous team surface (feature 026). |
| Auth on writes | **Team admin only**, resolved by `TeamMembershipGuard.ResolveAsync` — `IsAdmin` false ⇒ `403`; not a member ⇒ `404` (existing convention: a non-member cannot distinguish a team they may not touch from one that does not exist) |
| Read visibility | Any signed-in caller, member or not (spec FR-020) — it does not widen for members and does not narrow for non-members |
| Ban gating | None. A team's gallery does not depend on any member's account standing (research R3) |
| `{slug}` | Normalised via `TeamSlugPolicy.Normalize` |

---

## What is deliberately absent

- **No `PagedResult<T>` envelope.** Recorded deviation, see Complexity Tracking.
- **No object key, storage URL, container name, or SAS anywhere** in any response body or header
  (FR-022 / SC-004).
- **No `uploadedBy` field.** Not stored (research R4), so it cannot be returned.
- **No global `/media/{id}` endpoint.** The gate lives with the owner; a global route would have to
  re-derive the owner to authorize, which is precisely the coupling `IMediaStore`'s contract forbids.
- **No moderation, report, or takedown endpoint** (spec Out of Scope).
