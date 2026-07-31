# Contract: `IMediaStore` (internal service seam)

**Feature**: 035 · **Namespace**: `JuggerHub.Services.Media` · **Lifetime**: singleton (stateless)

This is the owner-agnostic seam required by FR-001–FR-003. It knows about **objects and keys** and
nothing else — no profiles, no badges, no achievements, no galleries. #99 consumes it unchanged.

## Operations

| Operation | Purpose | Notes |
|---|---|---|
| `PutAsync(key, content, contentType, ct)` | Store or overwrite the object at `key`. | Overwrite-by-key is what makes a retried write idempotent (the key is generated once per upload, before the first attempt). |
| `OpenReadAsync(key, ct)` | Read the object into a **fully materialised** stream. | Returns `null` when the object does not exist *or cannot be reached* — **not** an exception. Callers turn that into the ordinary "no picture" outcome (FR-029, US5 scenario 4). The returned stream MUST already hold every byte: a lazily-loading stream defers the fetch until the response body is being written, i.e. after status and headers are committed, where a storage failure can no longer be turned into a graceful response and the request dies mid-body instead. Bounded by 034's stored-output caps (≤512 KB avatars, ≤128 KB icons), so buffering is cheap; repeat views never reach it (304). |
| `DeleteAsync(key, ct)` | Remove the object. | Idempotent: deleting an absent object succeeds. |
| `ExistsAsync(key, ct)` | Whether an object is present. | For reconciliation and diagnostics, not for authorization. |
| `ListKeysAsync(prefix, ct)` | Enumerate stored keys as an `IAsyncEnumerable<string>`. | Used **only** by the reconciliation sweep (FR-030). Never on a request path. **Must stream, never materialise**: Principle III forbids a service method returning an unbounded collection, and a container's object count is unbounded by definition. Azure's listing is already continuation-token paged, so streaming is the natural shape — the caller processes in batches and never holds the whole key set. |

## Behavioural contract

1. **No authorization.** The store never decides who may see an object; it has no notion of a viewer.
   Every visibility and ban decision is made by the calling service before `OpenReadAsync` is called
   (FR-011). Adding a permission parameter to this interface would be a design error.
2. **Never leaks keys outward.** Keys are backend-internal (FR-013). No method returns anything a
   controller should serialise to a client.
3. **Absence is a value, not a fault.** A missing object is `null`/`false`, never an exception, so the
   "descriptor without object" edge case degrades to the placeholder instead of a 500.
4. **Real faults surface as exceptions** and are handled by the caller's resilience pipeline and the
   global exception middleware — never forwarded to the client verbatim (Principle I, FR-031).
5. **Resilience is inherited, not implemented here.** Retries, timeouts, and the breaker come from the
   shared 028 pipeline attached to the HTTP transport (research §3). The implementation MUST NOT
   contain a retry loop, a `Task.Delay` backoff, or its own timeout.

## Implementation notes (`AzureBlobMediaStore`)

- Wraps a `BlobContainerClient` resolved via `Microsoft.Extensions.Azure`.
- `BlobClientOptions.Retry.MaxRetries = 0` — **required**; see research §3. Leaving the SDK's retry on
  stacks two resilience implementations and multiplies attempts under failure.
- Ensures the container exists at startup, idempotently, and **never** with a public access level.
- Sets the blob's content type on write so a future direct-read path (should one ever be added) cannot
  serve the wrong type.

## Testing contract

- Round-trip: `PutAsync` → `OpenReadAsync` returns identical bytes and content type.
- Overwrite: two `PutAsync` calls at the same key leave exactly one object with the second's content.
- Absence: `OpenReadAsync` on an unknown key returns `null`; `DeleteAsync` on an unknown key succeeds.
- Opacity: a generated key does not contain the owner's id or handle (FR-015).
- **Transport wiring**: assert that store calls travel through the named, resilience-carrying
  `HttpClient`. Without this, a mis-wired transport silently leaves the store with no resilience at all
  and every other test still passes (research §3).
- **Materialisation**: open a stream, **delete the object**, then read the stream successfully. This is
  the only assertion that distinguishes a buffered result from a lazy one, and the lazy variant is a
  defect that hides behind `304`s for every viewer who already has the image (research §9).
