# Research: Chat File Attachments

Decisions taken while planning, each with the alternative it beat. Findings marked **read**
were established by reading the code rather than reasoned about.

---

## R1 — The allowlist is the primary security control, because there is no CSP header

**read**: `grep -r "Content-Security-Policy"` over the whole repository returns two hits, both
in `specs/033-umami-analytics/` recording that **no such header exists** and that adding one is
a forward dependency.

That fact promotes the allowlist from hygiene to load-bearing. Serving user-supplied bytes from
the application's own origin with no CSP means an uploaded SVG or HTML file rendered inline is
stored XSS against the app — the single thing `ChatMessage`'s own XML doc says the product
closes ("a chat is the natural home for stored XSS, and this is the line that closes it").

Two controls together, neither sufficient alone:

1. **Allowlist on detected content** — SVG and HTML are simply not storable.
2. **`Content-Disposition: attachment`** on everything that is not a normalized image — so even
   a future allowlist mistake is downloaded rather than executed in our origin.

**Rejected — a denylist.** Every newly dangerous type is a gap until somebody notices it, and
the list of "dangerous" types is not stable. Owner decision, recorded in the spec.

**Rejected — relying on a CSP added in the same feature.** It is a separate concern with its own
blast radius (the 033 snippet is an inline script that a naive policy breaks). This feature must
be safe without it.

**Accepted types**: `image/png`, `image/jpeg`, `image/webp`, `image/gif`, `application/pdf`,
`text/plain`, and the three OOXML types (`…wordprocessingml.document`,
`…spreadsheetml.sheet`, `…presentationml.presentation`).

## R2 — Detection, not declaration, and OOXML needs more than a sniff

Images are decided by `IImageProcessor`, which already ignores the declared type and reads the
header. For documents there is no equivalent, so detection is by **magic bytes**:

| Type | Signature |
|---|---|
| PDF | `%PDF-` |
| OOXML (docx/xlsx/pptx) | ZIP (`PK\x03\x04`) **plus** the expected `[Content_Types].xml` part |
| `text/plain` | no signature — validated as decodable UTF-8 with no control bytes |

The OOXML case is the trap: every OOXML file **is** a ZIP, so a signature check alone accepts
*any* ZIP renamed to `.docx` — which is how an archive of executables gets in past an allowlist
that thinks it is strict. Reading the content-types part is what distinguishes them.

**Rejected — trusting the client's `Content-Type`.** Principle I, and the avatar path already
refuses to (`ImageSharpImageProcessor` L52: "declared content type is never trusted").

**Rejected — trusting the file extension.** Same reason, and it is the sender's device talking.

## R3 — `ChatImage` processing profile

```
ResizeMode     = Fit          // NOT SquareCrop — a shared photo must not lose its edges
MaxDimension   = 1600         // legible full-screen on a phone and on a laptop
Quality        = 82
MaxOutputBytes = 1_500_000
```

`Fit` is the whole point of a separate profile: `Avatar` centre-crops because an avatar is a
circle, and applying that to a team photo would cut people out of it. `Icon` already
established that a second profile is the way to vary this (034: "an icon is artwork, so cropping
it would cut off content").

**Rejected — reusing `Avatar`.** It would crop every shared photo to a square.

**Rejected — storing the original beside a thumbnail.** Owner decision; doubles objects per
image and leaves the original carrying GPS unless stripped separately.

## R4 — Animated GIFs arrive as stills, for free

**read**: `ImageSharpImageProcessor` L68-71 removes every frame after the first, before
re-encoding. So FR-017 is satisfied by the existing pipeline with no work — an animated GIF
becomes a still WebP.

The coupling to watch is `AllowedContentTypes`, which is **one shared array** for every profile
(`ImageProcessingOptions` L58). Adding `image/gif` for chat also admits GIF avatars.

**Decision**: add it to the shared array. A GIF avatar is re-encoded to a still WebP like
everything else, so the change carries no new risk — but it is a change outside this feature's
stated surface and is recorded here so it is deliberate.

**Rejected — moving the allowlist onto the profile.** Tidier, but it touches avatar and icon
validation for the sake of one file type.

## R5 — A second cipher seam, sharing 047's keys

**read**: `IChatMessageCipher` is `string → byte[]` and documents that `Protect` **throws on
empty input**, because an envelope around `""` would be indistinguishable from a short message
and would destroy the observability of "this row holds nothing" (047 data-model D2).

That rule is correct for text and wrong for files — an empty file is a different question, and
a caller reasoning about attachments should not have to know why a text API refuses empties.

**Decision**: `IChatBlobCipher` (`byte[] → byte[]`), bound to the **attachment's** id as
associated data, reusing `ChatEncryptionOptions` — the same key set, the same envelope format
`[version:1][nonce:12][tag:16][ciphertext:n]`, the same fail-fast startup guard. One secret to
rotate, two seams.

Binding to the attachment id (not the message id) means an operator with write access cannot
move a ciphertext between attachments **or** between an attachment and a message body: the tag
fails and the attachment reads as unavailable rather than as someone else's file.

**Rejected — widening `IChatMessageCipher`.** Drags the empty-string rule into file territory
and puts two contracts behind one interface.

**Rejected — a separate key for attachments.** Two secrets to keep in sync and rotate, for no
gain: anything that can read one can read the other.

## R6 — Upload ordering, and why partial messages are structurally impossible

**read**: `ProfileService.SetAvatarAsync` L317-321 already names the constraint — "a row and a
blob cannot share a transaction, so some failure window is unavoidable — this ordering picks the
harmless one."

Applied here: validate everything → process → put every object → commit the message row **and**
every attachment row in **one** `SaveChangesAsync`.

The single commit is what makes FR-008 structural. A message cannot be committed holding half
its attachments, because they are in the same transaction. Anything that fails before the
commit leaves objects with no referent, which is the **already-solved** case: the
reconciliation sweep reclaims them after its grace window.

**Rejected — writing the message first, then attaching.** Produces exactly the visible partial
message FR-008 forbids.

**Rejected — a staged-upload endpoint.** Needs a staging area, an expiry, and an ownership rule
to stop one member attaching another's staged file — three new problems to avoid one request.

## R7 — Principle VII is only partially engaged, and over-applying it is review-rejectable

| Hop | Engaged? |
|---|---|
| Browser → backend upload | **Yes, as a prohibition.** It is a mutation, so it is **never** auto-retried (a timed-out send may already have posted). The user retries deliberately |
| Backend → object store | **Inherited.** `Resilience:Outbound:MediaStore` already exists and already covers `PutAsync`/`OpenReadAsync`. No new section, no new named client |
| Encryption, image processing | **No.** Local CPU. Wrapping them in retry or a breaker is review-rejectable |

This mirrors the note every recent plan carries, and the trap is the same: the feature *feels*
network-heavy, so reaching for `AddJuggerHubResilience` looks diligent. Here it would wrap a
local `AesGcm` call and stack a second strategy on the store's existing one.

## R8 — Serving decrypted bytes ends range requests

**read**: `IMediaStore.OpenReadAsync`'s contract already requires a **fully-materialised**
stream ("reading it MUST NOT be able to fail for storage reasons"), because the caller writes it
into an HTTP response and a deferred fetch fails after the headers are committed.

Decryption needs the whole envelope regardless — GCM authenticates over the complete ciphertext,
and returning unauthenticated plaintext prefixes would defeat the point. So an attachment is
read whole, decrypted whole, and returned whole.

Consequences, accepted: no `Accept-Ranges`, no seeking, and per-request memory bounded by the
10 MB cap. The cap is what keeps this acceptable; it is the reason a video allowlist entry would
change the calculus.

## R9 — Reuse `MediaResponse`, with one addition

**read**: `MediaResponse.File` already does the three things that carry security meaning —
`private` (never `public`, so no shared cache re-serves gated bytes), `no-cache` (so revocation
takes effect on the next request), and an ETag that is a **SHA-256 fingerprint of the object
key** rather than the key itself.

All three apply unchanged. The one gap is `Content-Disposition` (F6/R1). Add an overload taking
the download file name; **the existing signature must keep behaving identically** so avatars and
catalogue icons are untouched — the type's own doc says it exists "so avatars and catalogue
icons cannot drift apart on the headers that carry security meaning", and a third caller with
different needs is exactly when that drifts.

## R10 — File names are display data and never touch a key

`ObjectKey` is minted by `MediaObjectKey.Create` from a UUIDv4 and is independent of anything the
sender supplied. The original file name is stored **only** to render the file row and to fill the
download disposition.

Two rules follow:

1. Store it, but bound it (255 chars) and strip control characters and path separators — a name
   like `../../etc/passwd` must be inert as *display*, which it is once it never reaches a path.
2. Encode it properly in the `Content-Disposition` header (RFC 5987 `filename*`), or a name with
   a quote or a newline becomes header injection.

**Rejected — deriving the object key from the file name.** It would make the key guessable and
would make the name load-bearing.

## R11 — What an attachment-only message says everywhere text is quoted

**read**: `LastMessageDto` carries `Preview` and an `IsSystem` flag; the inbox renders system
lines from the flag rather than from server prose. 047 put its "message unavailable" placeholder
in the **frontend** catalogues for the same reason — server-assembled prose has no key to be
missing (GH #141).

**Decision**: `LastMessageDto` gains an attachment marker; the client renders the label
("Photo", "File", "3 files") from its own catalogues.

**Rejected — the server composing "sent a photo".** Puts user-facing prose in C# where the
parity guard cannot see it, and it would not be localized per reader.

## R12 — Attachments survive a conversation snapshot

FR-033 demands a stated answer rather than an emergent one. Chat is snapshotted rather than
deleted when a team is deleted or an event cancelled, and the privacy policy already says so.

**Decision**: attachments survive with the thread. A snapshot whose files had silently vanished
would make the surviving conversation misleading — members would read replies to documents that
are not there.

**Rejected — stripping attachments on snapshot.** Saves storage at the cost of making the
retained record wrong, which is the opposite of why it is retained.
