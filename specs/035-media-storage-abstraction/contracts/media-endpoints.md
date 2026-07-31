# Contract: Media HTTP endpoints

**Feature**: 035 · **Rule**: routes, verbs, status codes, and payload shapes are **unchanged** (FR-004).
The frontend requires no change. What changes is *where the bytes come from* and *which headers ship*.

## Endpoints (all pre-existing)

| Endpoint | Auth | Gate applied by the service | Change |
|---|---|---|---|
| `PUT /api/v1/profiles/me/avatar` | `[Authorize]` | owner is the caller | Stores to the media store, persists a descriptor. Same statuses. |
| `GET /api/v1/profiles/{handle}/avatar` | `[AllowAnonymous]` | visibility (026) + ban filter | Streams from the media store after the gate. Adds cache headers. |
| `GET /api/v1/badges/{definitionId}/icon` | `[AllowAnonymous]` | none by design (FR-014) | Streams from the media store. Adds cache headers. |
| `GET /api/v1/achievements/{definitionId}/icon` | `[AllowAnonymous]` | none by design (FR-014) | Streams from the media store. Adds cache headers. |
| Admin catalogue icon upload | admin | admin policy | Stores to the media store. |
| **`POST /api/v1/admin/media/reconcile`** | admin | admin policy | **NEW** — orphan sweep (FR-030). |

## Read semantics

**Order of operations — this ordering is the security contract:**

1. Load the **descriptor** (`ObjectKey`, `ContentType`, plus whatever the gate needs) with a projection.
   The `HasQueryFilter` ban gate applies here automatically.
2. Apply the visibility gate (`IsVisibleTo` for avatars; none for catalogue icons).
3. **Only then** call `IMediaStore.OpenReadAsync`.

Reversing 2 and 3 would fetch bytes for a viewer who may not see them. Nothing is leaked as long as
they are discarded, but it wastes a store call and puts the gate downstream of the data — the shape of
mistake FR-011 exists to prevent.

**Responses**:

| Situation | Response |
|---|---|
| Gate passes, object present | `200` + bytes, `Content-Type` from the descriptor |
| Gate fails (private + anonymous, banned, unknown handle) | `404` — unchanged, and deliberately not `403`, so the endpoint stays a non-oracle |
| Descriptor absent (no picture) | `404` — unchanged |
| **Descriptor present, object missing** | `404` — same "no picture" outcome (US5 scenario 4), logged for operators |
| **Store unreachable** | `404`, so the page still renders with its placeholder (FR-029). Logged at a severity that gets noticed; never a 500 to the client, never a provider error (FR-031) |

The last two rows are the only new behaviours, and both deliberately resolve to the outcome the
frontend already knows how to render.

**Headers**:
- `Cache-Control: private, no-cache` — two separate decisions, both load-bearing:
  - **`private` always.** A gated avatar must never be storable in a shared cache; using `public` here
    would recreate, in an intermediary, the exposure the proxy design exists to prevent.
  - **`no-cache`, not a long `max-age`.** `no-cache` means "may store, must revalidate" — not "must not
    store". A long `max-age` would stop the browser making a request at all, so a member who goes
    private or is banned would keep rendering in a viewer's browser for the whole window. FR-016
    requires the change to take effect on the **next request** with no such window. Revalidation costs
    one descriptor read and a `304`, so SC-004's benefit is kept in full.
- `ETag` — an opaque **hash** of `ObjectKey`, never the key itself. The key regenerates on every upload,
  so a hash of it changes exactly when the bytes change, while FR-013 ("a media object's location MUST
  never be disclosed to a client") stays intact. Emitting the raw key as an ETag would publish the very
  value FR-013 and FR-015 exist to keep inside the backend.
- `304 Not Modified` on a matching `If-None-Match` — a repeat view then costs one descriptor read and
  **no store call at all**, which is the main answer to SC-004.

**Rate limiting**: a `MediaRead` policy partitioned by user when authenticated and by client IP
otherwise. The existing `PartitionByUser` helper is insufficient here because these endpoints serve
anonymous callers by design.

## Write semantics

Ordering (research §10): **generate key → store object → save descriptor → delete superseded object.**

- A failure after storing but before committing leaves an unreferenced object — reclaimable by the
  sweep, and never a descriptor pointing at nothing.
- A failure deleting the superseded object leaves an orphan — likewise reclaimable.
- Store failure during upload → the existing non-success path, generic reason, **previous picture
  untouched** (US5 scenario 2). This preserves the 034 guarantee that a rejected upload never disturbs
  stored media.

Existing `AvatarSetStatus` values are unchanged; a store outage maps to a generic failure rather than a
new client-visible reason, because the client can do nothing differently with the distinction.

## Explicitly NOT in the contract

- No endpoint returns an object key, a container name, a storage URL, or a signed link (FR-013).
- No redirect (`302`) to storage — the proxy decision forbids it.
- No new client-visible field anywhere.
