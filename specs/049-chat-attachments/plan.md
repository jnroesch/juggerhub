# Implementation Plan: Chat File Attachments

**Branch**: `049-chat-attachments` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/049-chat-attachments/spec.md`, GH **#282**

## Summary

Chat gains file attachments: a `+` control opens the OS picker, images render inline in the
message, everything else renders as a named file row with a download action. Files are
allowlisted, normalized (images), encrypted at rest, and stored in the existing object store.

**Scope markers** — a task that produces any of these means something has gone wrong:

| | |
|---|---|
| **ONE new entity** | `ChatAttachment` — and one migration for it |
| **NO new dependency** | `AesGcm` is BCL, ImageSharp and the Blob SDK are already referenced |
| **NO new outbound integration** | the store hop already has `Resilience:Outbound:MediaStore` |
| **NO new realtime event** | the existing message-sent and message-deleted pushes carry this |
| **NO second media mechanism** | a `bytea` column for attachment bytes is a regression against 035 |

## Technical Context

**Language/Version**: .NET 10 (backend), Angular + Nx + Tailwind (frontend), PostgreSQL 18
**Primary dependencies**: EF Core, SixLabors.ImageSharp (034), Azure.Storage.Blobs (035), `System.Security.Cryptography.AesGcm` (047)
**Storage**: Postgres descriptor row + Azure Blob object (Azurite local/test)
**Testing**: xUnit integration tests w/ Testcontainers (backend), Jest (frontend)
**Target**: Web, ≥375 px
**Performance goals**: a conversation with attachment-bearing messages opens no slower than a text-only one (SC-010)
**Constraints**: 10 MB/file, 10 files/message; no HTTP range requests (a consequence of FR-022); no CSP header exists

## Constitution Check

| Gate | Status |
|---|---|
| 1 — Architecture | Thin controllers; the work lives in services behind interfaces; DTOs from explicit `.Select` projections |
| 2 — Data access | `ChatAttachment : BaseEntity` (UUIDv7); attachments read by projection with `AsNoTracking`; the message page is already keyset-paged and stays so; **no unbounded list** — attachments are capped at 10 per message by FR-012, so they ride the message's page rather than paginating separately |
| 3 — Security | The whole feature is OWASP surface: allowlist on detected content (A03), authorization before bytes (A01), no object key to the client (A01), save-don't-render (A03), no stack traces out (A09) |
| 4 — Auth | Unchanged; attachment endpoints sit behind the existing cookie auth |
| 5 — Conventions | Frontend keeps `.html`/`.css`/`.ts` separate; any script is `.ps1` |
| 6 — Parity | Azurite locally and in tests, real storage deployed — the shape is identical |
| **7 — UI/Design** | **ENGAGED.** New composer control, attachment tray, image preview, lightbox, file row → `checklists/ui-review.md` |
| **8 — Resilience** | **PARTIALLY ENGAGED — read R7 before writing any resilience code.** The browser→backend upload is a **mutation and is never auto-retried**. The store hop **inherits** `Resilience:Outbound:MediaStore`; it does not get a section of its own. Encryption and image processing are local CPU — wrapping them in retry/breaker is review-rejectable |

## Project Structure

### Documentation (this feature)

```
specs/049-chat-attachments/
├── spec.md
├── plan.md              # this file
├── research.md          # the decisions, and why the alternatives lose
├── data-model.md        # ChatAttachment + the three meanings of an empty body
├── contracts/
│   └── chat-attachments-api.md
├── checklists/
│   └── ui-review.md     # Gate 7
└── tasks.md             # produced by /speckit-tasks
```

### Source Code

```
backend/
├── Entities/ChatAttachment.cs                      NEW
├── Dtos/Chat/ChatDtos.cs                           MessageDto + AttachmentDto, LastMessageDto
├── Services/Chat/
│   ├── ChatMessageService.cs                       send + delete paths
│   ├── ChatAttachmentService.cs                    NEW — accept, store, serve
│   └── Encryption/IChatBlobCipher.cs               NEW — byte[]↔byte[], shares 047's keys
├── Services/Media/MediaObjectKey.cs                +MediaKind.ChatAttachment
├── Services/Media/MediaResponse.cs                 +download disposition
├── Common/ImageProcessingOptions.cs                +ChatImage profile
├── Controllers/ChatMessagesController.cs           send accepts files; +download action
└── Migrations/                                     ONE migration

frontend/apps/web/src/app/
├── features/chat/chat-conversation/                + button, tray, preview, file row
├── features/chat/chat-inbox/                       attachment-only last line
├── core/services/chat.service.ts                   multipart send
└── i18n/{en,de,es}.json                            new keys ×3, one change
frontend/apps/web/public/i18n/legal/{en,de,es}.json privacy policy ×3
```

---

## Load-bearing findings (all established by reading the code)

### F1 — `SendAsync` refuses an empty body on line 81, and that is the first collision

```csharp
var trimmed = body?.Trim() ?? string.Empty;
if (trimmed.Length == 0)
{
    return ChatResult<MessageDto>.Fail(ChatOutcome.Invalid, "Write a message first.");
}
```

FR-004 (a message may be attachments alone) cannot hold until this becomes "no text **and**
no attachments". This is the single most likely thing to be missed, because everything else
about the send path looks attachment-agnostic.

### F2 — the DTO layer already handles an empty body correctly, with zero changes

`ChatMessageService.ReadBody` (L441):

```csharp
if (r.IsDeleted || r.BodyCipher.Length == 0)
{
    return (string.Empty, false);
}
```

A zero-length `BodyCipher` maps to **empty text, not unavailable**. An attachment-only message
therefore stores `BodyCipher = []` — exactly as a system line and a deleted message already do
— and renders correctly through the existing path. **Do not add an `IsAttachmentOnly` branch to
`ReadBody`**; it would be dead code that looks necessary.

The cost is that zero-length now carries a **third** meaning. `ChatMessage.BodyCipher`'s XML doc
enumerates exactly two ("a system line has nothing to say, and a deleted message's content is
genuinely gone") and **must be amended**, or the next reader will conclude an attachment-only
row is corrupt.

### F3 — `Protect("")` throws, deliberately

`IChatMessageCipher.Protect` raises `ArgumentException` on empty input, because an envelope
around the empty string is 29 bytes indistinguishable from a short message and would destroy the
observability of "this row holds nothing" (047 data-model D2). The send path must **skip**
`Protect` when there is no text, never call it with `""`.

### F4 — the cipher's contract is text, and attachments are not text

`IChatMessageCipher` is `string → byte[]`. Widening it to bytes would drag the empty-string rule
(F3) into a place where an empty *file* is a different question. Introduce a sibling
**`IChatBlobCipher`** (`byte[] → byte[]`, bound to the **attachment's** id as associated data)
that **reuses 047's key material and envelope format** through the existing
`ChatEncryptionOptions`. One key set, two seams, no second key to keep in sync — and no
possibility of an attachment ciphertext authenticating against a message row.

### F5 — the upload ordering is already solved, and `SetAvatarAsync` is the reference

`ProfileService.SetAvatarAsync` L317-328 states it: *mint the key → put the object → commit the
descriptor → only then delete what it replaced*, because "a row and a blob cannot share a
transaction, so some failure window is unavoidable — this ordering picks the harmless one."

Applied here, with the harmless direction being unreferenced objects:

1. Validate **every** file (allowlist, size, count) — reject the whole send if any fails (FR-008).
2. Process images; mint one key per file; put every object.
3. Build the `ChatMessage` **and** its `ChatAttachment` rows, and commit them in **one
   `SaveChangesAsync`** — so a message can never be committed holding half its attachments.
4. A failure before step 3 leaves objects nothing references; the **existing** sweep reclaims
   them. A failure during step 3 does the same. Neither leaves a visible partial message.

There is no superseded object to delete, so the avatar path's fourth step has no analogue.

### F6 — `MediaResponse.File` renders inline, and FR-026 needs it not to

`MediaResponse.File` sets `Cache-Control: private, no-cache` + an ETag that is a **hash of the
object key** (never the key), and answers 304 — all reusable verbatim. But it ends in
`controller.File(stream, contentType)` with **no `Content-Disposition`**, so a browser renders
the bytes in the application's origin.

For a normalized image that is exactly what we want (FR-018). For every other file it is the
stored-XSS route the allowlist exists to close, and the application **has no CSP header** to fall
back on (`specs/033-umami-analytics/spec.md:214`). Add a download-disposition overload carrying
the original file name; **avatars and catalogue icons must keep calling the existing one
unchanged**.

### F7 — encryption ends streaming, and that is accepted

`IMediaStore.OpenReadAsync` already returns a fully-materialised stream by contract. Decryption
needs the whole envelope regardless. So serving is read-all → decrypt → return bytes: no range
requests, no seeking inside a file, and per-request memory bounded by the 10 MB cap. Recorded as
a residual, not a defect — it is the direct cost of the owner's encryption decision.

### F8 — `MediaKind` has three values and the switch throws on a fourth

`MediaObjectKey.Prefix` ends in `_ => throw new ArgumentOutOfRangeException(...)`, so adding
`MediaKind.ChatAttachment` forces the prefix (`chat-attachments`) to be acknowledged rather than
defaulted. Keys stay **UUIDv4** for the reason already written there: unguessable beats
timestamp-ordered for a storage key. Do not "correct" this to v7.

### F9 — the GIF decision has a coupling outside chat

`ImageProcessingOptions.AllowedContentTypes` is **one shared array** across every profile. The
spec allows GIF in chat; today the array is PNG/JPEG/WebP. Adding `image/gif` also lets GIF
avatars in — harmless in itself (every image is re-encoded to still WebP, and the processor
already flattens animation to frame 1, which is FR-017), but it is a change outside this
feature's stated surface and must be a conscious call, not a side effect.

**Recommendation**: add `image/gif` to the shared array and note it. Moving the allowlist onto
the profile is the tidier design but touches avatar and icon validation for one file type.

### F10 — the delete path is one place, and it already tells every tab

`DeleteAsync` L557-563 clears `BodyCipher`, `LinkKind` and `LinkTargetId` under a comment
explaining that a flag would make "deleted" a rendering convention rather than a fact. FR-029
extends the same statement to attachments: **delete the rows and the objects**, in that same
block. `PushMessageDeletedAsync` already fans a tombstone out to every member including the
sender's other tabs — **no new realtime event**.

### F11 — the inbox preview needs a flag, not server prose

`LastMessageDto` carries `Preview` plus an `IsSystem` flag, and the client renders system lines
from the flag. An attachment-only message has no text, and server-assembled prose has no key to
be missing (GH #141) — 047 put its placeholder in the frontend catalogues for exactly this
reason. So `LastMessageDto` gains an attachment marker and the **client** renders the label.

### F12 — the composer has exactly one coral CTA, by design

`chat-conversation.component.html:222` carries the comment *"The one coral CTA in this view
(CHK002/CHK034)"*. The `+` button must therefore be a **neutral/ghost** control, not a second
brand-coloured button. The hidden-input pattern is already established twice
(`profile-owner.component.html:67`, `admin-catalogue.component.html:293`) — copy it, adding
`multiple`.

### F13 — the app is zoneless, so tray state must be signals

045's finding, and it applies unchanged: an `effect()` cannot observe a plain property. The
selected-file tray, per-file progress and per-file error are all signals.

---

## Design decisions

### D1 — one attachment row per file, owned by the message

`ChatAttachment : BaseEntity` with `ChatMessageId` (cascade), `ObjectKey`, `ContentType`,
`SizeBytes`, `FileName`, `Ordinal`, and nullable `Width`/`Height` (images only). Cascade delete
keeps the rows honest; the **objects** still need explicit reclamation (F10) because a blob is
not in the transaction.

`FileName` is the sender's original name and is **display data, never a path**: it is returned
for rendering and used in the download disposition, and never participates in building an object
key. See `data-model.md` for the sanitisation rule.

### D2 — `IsImage` is derived from the stored content type, not stored separately

After normalization an image is `image/webp` and nothing else is. A second boolean would be a
second source of truth that a future projection could disagree with.

### D3 — a new `ChatImage` processing profile, `Fit` not `SquareCrop`

Avatars centre-crop because an avatar is a circle. A shared photo must not lose its edges, so
`ResizeMode.Fit`, a larger `MaxDimension` than the avatar's 512, and a larger `MaxOutputBytes`.
Concrete values in `research.md` R3.

### D4 — the send endpoint accepts multipart; attachments are not uploaded separately

A two-step upload-then-send would need a staging area, an expiry for abandoned uploads, and a
way to stop one member attaching another member's staged file. One request keeps FR-008
("no partial message") structural rather than a cleanup path.

The cost is a request up to ~100 MB. `[RequestSizeLimit]` is set accordingly and the per-file
and per-count limits are enforced **before** any processing.

### D5 — download is one endpoint keyed by attachment id

`GET /chat/attachments/{id}` — the id is a UUIDv7 and the row carries its conversation, so the
existing `ChatGuard` decides access **before** the store is touched (FR-024), and every refusal
is a 404 (FR-025), matching `GetAvatarAsync`'s three-step shape exactly.

### D6 — attachments survive a conversation snapshot

FR-033 requires a stated answer. Chat is snapshotted rather than deleted on team-delete and
event-cancel, and the privacy policy says so; an attachment that vanished from a snapshot would
make the surviving thread misleading. So they survive, and the policy text covers them.

---

## Phases

| Phase | Content | Verification |
|---|---|---|
| **1 — Storage seam** | `MediaKind.ChatAttachment`, `IChatBlobCipher` + implementation, `ChatImage` profile, GIF decision (F9) | Unit: round-trip encrypt/decrypt bound to an id; a ciphertext from attachment A fails to authenticate against attachment B |
| **2 — Entity + migration** | `ChatAttachment`, EF configuration, ONE migration | `dotnet ef migrations` applies and reverts cleanly |
| **3 — Accept path** | `ChatAttachmentService` validation + store; `SendAsync` changes incl. **F1**; multipart endpoint | Integration: each refusal class; attachment-only message accepted; no partial message on failure |
| **4 — Serve path** | download endpoint, `ChatGuard` gate, `MediaResponse` disposition overload (**F6**) | Integration: non-member 404; disposition present for documents, absent for images; avatars unchanged |
| **5 — Read path** | `AttachmentDto` on `MessageDto`, `LastMessageDto` marker (**F11**), realtime unchanged | Integration: thread renders; unavailable attachment degrades alone |
| **6 — Lifecycle** | delete (**F10**), account erasure, sweep coverage | Integration: withdraw → objects gone; erase → reclaimed |
| **7 — Frontend** | `+` button + tray (**F12/F13**), inline preview, lightbox, file row, inbox label | Jest; 375 px |
| **8 — Copy & law** | i18n ×3 in one change, privacy policy ×3 (German authoritative), 038 re-check | `catalog-parity.spec.ts`, `legal-catalog.spec.ts` |
| **9 — Gate 7** | `checklists/ui-review.md` against the diff | DESIGN.md wins on conflict |

## Complexity Tracking

| Deviation | Why | Rejected alternative |
|---|---|---|
| A second cipher seam beside 047's | F4 — the text contract's empty-string rule must not govern files | Widening `IChatMessageCipher` to bytes |
| A bare list of attachments on `MessageDto`, not `PagedResult<T>` | Capped at 10 by FR-012; a `totalCount` would advertise paging that does not exist. Precedent: `Roster` (48), `RecentActivity` (6), 044's feed | Paginating a list that cannot exceed ten |
| Multipart on the send endpoint | D4 — keeps "no partial message" structural | Staged upload + reference |

## Recorded residuals

1. **No range requests** (F7) — a consequence of encrypting at rest, accepted with the decision.
2. **No cumulative storage quota** — limits are per file and per message; growth over time is
   real and unaddressed. Follow-up.
3. **No malware scanning** — the allowlist is the chosen control, and what it rules out is
   now verified by test rather than asserted (macro-enabled Office refused, embedded image
   payloads destroyed by re-encoding, served extension forced to match detected content).
   What remains is a hostile PDF or a macro-free-but-nasty document. Tracked as **#284**,
   which also records the question that must be answered first: what happens when the
   scanner is down — fail open makes it decorative, fail closed takes out file sending.
4. **Normalization is lossy** — a screenshot re-encoded to WebP loses some fidelity; the sender
   is not warned. The alternative doubles objects and keeps EXIF alive.
5. **Session replay captures inline previews** (038, `maskLevel: moderate`). FR-042 requires the
   disclosure re-read; the lever if it is judged too wide is a `blockSelector` on the thread.
6. **No CSP header** — the allowlist plus `Content-Disposition` carry the whole load. Separate
   issue, not this feature.
7. **An attachment-only message counts as unread exactly like any other** — no change, stated so
   it is not mistaken for an oversight.

---

## Implementation notes — what the build changed about this plan

Recorded after the fact, per CLAUDE.md's "report spec drift". Five things the plan did not
anticipate, four of them found by the code rather than by reasoning.

### N1 — The sweep would have deleted every attachment (found by T041, fixed, tested)

`MediaReconciliationService` enumerates the **whole container** and deletes any object it cannot
match to a descriptor row — and its referenced-key set was built from three tables. Chat
attachments were a fourth, so every one of them would have been reclaimed one grace period after it
was sent. Silent, irreversible, and an hour late.

The task said "verify rather than assume", and verifying is what found it. Fixed by adding
`ChatAttachments` to the set, guarded by `Sweep_never_reclaims_a_chat_attachment`, and **confirmed
by removing the fix and watching the test fail**. The comment above the set now says explicitly
that a missing table is not a coverage gap but a table whose objects get destroyed.

### N2 — FR-031 was wrong as written, and was corrected rather than implemented

The spec said account erasure should reclaim attachments. Feature 037 tells members **in three
languages** that their chat messages survive erasure, and its retention rationale is that other
people's conversations stay coherent. An attachment is part of a message, not a separate
possession. Deleting them would have left surviving threads half-gone.

So `AccountDeletionService` is **untouched**, and FR-031 now says so with its reasoning. The
erased sender still renders as "A former player"; their profile picture is still erased, which 037
already covers.

### N3 — Routing on the image processor's failure does not work

The plan assumed `IImageProcessor` could serve as the image detector. It cannot: ImageSharp raises
`ImageFormatException` for **any** unrecognised format, which the processor maps to `Unreadable`,
while its `UnsupportedType` means a format it *does* recognise but the allow-list excludes. A PDF
and a truncated PNG therefore arrive identically, and every document would have been refused as a
damaged image.

`AttachmentContentType` now sniffs signatures first and routes; the validator still decides whether
the file is any good. The reasoning is written where the next reader will hit it.

### N4 — `MediaObjectKey.Create` hardcoded `.webp`

Fair when every object was a normalized image; misleading the moment a PDF is stored. An extension
overload was added, derived from the **stored content type** and rejecting anything but 1–8 ASCII
alphanumerics — never from a supplied file name.

### N5 — Two surfaces treated "no text" as "withdrawn"

- The **inbox row** rendered "Message deleted" whenever the preview was empty, so a shared photo
  would have read as a message someone took back. Now it checks the attachment count first
  (five tests).
- The **live inbox bump** built a `LastMessage` without the new fields, so a photo arriving over
  SignalR would have shown the same wrong label until the next reload. Caught by the compiler
  during the production build, not by a test — worth noting, because the type system was the only
  thing standing between this and a bug nobody would have reproduced locally.

### N6 — Gate 7 found four real failures

Not drift, but worth recording as evidence the gate earns its place: a 32px touch target, an
upload that showed nothing in flight, `md` radius on media where DESIGN.md specifies `xl`, and an
ad-hoc `border-white/30` with no precedent. All four fixed; see `checklists/ui-review.md`.

### Residual added to the list above

7. **The session-recording disclosure was widened, not just re-read.** FR-042 asked for a check;
   the check found that paragraph 6 of the analytics section said "including what the other person
   wrote" — text only. Inline image previews are rendered content and are captured, so all three
   locales now say so. The `blockSelector` lever remains available if that is judged too wide.
