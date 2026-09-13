---
description: "Task list for feature 049 — Chat File Attachments"
---

# Tasks: Chat File Attachments

**Input**: Design documents from `/specs/049-chat-attachments/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/chat-attachments-api.md](./contracts/chat-attachments-api.md)

**Tests**: Included. This feature adds an upload path, a byte-serving path and a deletion path
to the product's most security-sensitive surface. Several of its guarantees — a ciphertext not
authenticating against a different row, a withdrawn message's objects actually being gone, a
404 rather than a 403 on refusal — are invisible in the diff and rot silently without a test.

**Organization**: grouped by user story so each can be implemented and verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable — different file, no dependency on an incomplete task
- **[Story]**: US1 (send a file, P1) · US2 (inline image preview, P2) · US3 (refusals, P3) ·
  US4 (withdrawal takes the files, P4)

## ⚠ Bounds — check every task against these

**ONE** entity (`ChatAttachment`) · **ONE** migration · **NO** new dependency · **NO** new
outbound integration · **NO** new realtime event · **NO** second media mechanism · **NO**
`bytea` column for file bytes · **NO** staging/upload-then-send endpoint · **NO** CSP header
(separate issue).

If a task below seems to need one, stop and re-read [research.md](./research.md) R5/R6/R7 and
[plan.md](./plan.md) F10.

---

## Phase 1: Setup

- [ ] T001 Confirm the working tree is on the feature branch and `docker compose up -d` brings
      up postgres, redis and **azurite** (media fails closed without it; chat fails closed
      without redis). Run the baselines green before changing anything:
      `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"`,
      `… --filter "FullyQualifiedName~IntegrationTests.Media"`, and
      `cd frontend; npx nx test web --watch=false --testPathPattern="chat|catalog-parity"`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the storage seam, the cipher seam and the entity. Nothing in Phases 3–7 can start
until these land.

- [X] T002 [P] Add `ChatAttachment` to `MediaKind` in `backend/Services/Media/MediaObjectKey.cs`
      and its prefix `chat-attachments` to `Prefix`. The switch's
      `_ => throw new ArgumentOutOfRangeException(...)` arm means the prefix cannot be forgotten.
      **Keys stay UUIDv4** — the type's own doc explains why and asks not to be "corrected" to v7
      (research R10)
- [X] T003 [P] Add `image/gif` to `ImageProcessingOptions.AllowedContentTypes` in
      `backend/Common/ImageProcessingOptions.cs`, and add the `ChatImage` profile beside
      `Avatar` and `Icon`: `ResizeMode.Fit`, `MaxDimension 1600`, `Quality 82`,
      `MaxOutputBytes 1_500_000`. **`Fit`, never `SquareCrop`** — cropping a shared photo cuts
      people out of it (research R3). Document on the property that the shared allowlist now
      admits GIF avatars too, which is deliberate and harmless because every image is re-encoded
      to a still WebP (research R4)
- [X] T004 Create `backend/Services/Chat/Encryption/IChatBlobCipher.cs` and
      `AesGcmChatBlobCipher.cs`: `byte[] Protect(byte[] plaintext, Guid attachmentId)` and
      `bool TryUnprotect(byte[] cipher, Guid attachmentId, out byte[] plaintext)`. Reuse
      `ChatEncryptionOptions` — **the same key set, the same envelope**
      `[version:1][nonce:12][tag:16][ciphertext:n]`, the same fail-fast startup guard. Bind to
      the **attachment's** id, not the message's (data-model D2). Do **not** widen
      `IChatMessageCipher`: its empty-string rule is a text contract and must not govern files
      (research R5). Register it alongside the message cipher in
      `ChatEncryptionServiceCollectionExtensions.cs`
- [X] T005 [P] Unit tests for T004 in `backend/tests/JuggerHub.Api.IntegrationTests/Chat/`:
      round-trip under the write key; a ciphertext produced for attachment A **fails to
      authenticate** against attachment B's id; a truncated envelope returns `false` rather than
      throwing; an envelope naming an unconfigured key version returns `false`
- [X] T006 Create `backend/Entities/ChatAttachment.cs` per [data-model.md](./data-model.md):
      `ChatMessageId`, `ObjectKey`, `ContentType`, `SizeBytes`, `FileName`, `Ordinal`,
      `Width?`, `Height?`. **No `IsImage` column** — it is derived from `ContentType` (D3).
      XML-doc that `ObjectKey` never leaves the backend and that `FileName` is display data that
      never reaches a path
- [X] T007 Add `ICollection<ChatAttachment> Attachments` to `backend/Entities/ChatMessage.cs`,
      and **amend the `BodyCipher` XML doc**: a zero-length array now has a **third** legitimate
      meaning — a member message carrying only attachments — beside a system line and a deleted
      message. Without this the next reader concludes such a row is corrupt (data-model D6).
      Do **not** change `ReadBody`
- [X] T008 Configure `ChatAttachment` in `backend/Data/AppDbContext.cs` (fluent, beside the
      `ChatMessage` block at ~L1032): `DbSet`, cascade FK to `ChatMessages`, column widths per
      the data model, index `(ChatMessageId, Ordinal)`
- [X] T009 Generate **one** migration into `backend/Data/Migrations/`. Create-table only — **no
      backfill, nothing dropped, no `ALTER` against `ChatMessages`**. Verify it applies and
      reverts cleanly. A second migration means something went wrong
- [X] T010 [P] Add the attachment i18n keys to **all three** catalogues in one change —
      `frontend/apps/web/public/i18n/{en,de,es}.json`: the `+` control's accessible name, the
      tray heading, remove-file, per-refusal reasons (`file_too_large`, `too_many_files`,
      `unsupported_type`, `unreadable_file`), the file-row download action, the unavailable-
      attachment placeholder, and the inbox labels ("Photo" / "File" / "{{count}} files").
      `catalog-parity.spec.ts` is red until all three land

**Checkpoint**: migration applies and reverts; cipher tests green; `catalog-parity` green.

---

## Phase 3: User Story 1 — Send a file into a conversation (P1) 🎯 MVP

**Goal**: a player attaches a file with `+` and sends it; every member receives it and can
open it. Shipped alone — with every file rendering as a plain named row — this already removes
the reason players leave the platform to share something.

**Independent test**: press `+`, choose a PDF, send; confirm it arrives for both sender and
recipient, named as it was on the device, and that what downloads is what was sent.

### Tests for User Story 1

- [X] T011 [P] [US1] Create
      `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatAttachmentTests.cs` following the
      neighbouring suites' shape (`ChatTestSupport` fixtures, `FakeChatRealtime`): a message with
      one file round-trips; **a message with files and no text is accepted** (FR-004, the
      collision at plan F1); text and files arrive as **one** message; several files keep the
      sender's order; a non-member gets **404** from the download endpoint
- [X] T012 [P] [US1] Add to the same suite: the stored object for a document is **byte-identical**
      to what was uploaded after decryption, and the row's `BodyCipher` for an attachment-only
      message is **zero-length** — never `Protect("")` (research R5)

### Implementation for User Story 1

- [X] T013 [US1] Create `backend/Services/Chat/ChatAttachmentService.cs` + interface. Accept
      path: enforce **count** then **per-file size** before reading bytes; detect content per
      research R2 (images via `IImageProcessor`; PDF/text/OOXML via magic bytes, and **OOXML
      must check the `[Content_Types].xml` part** or any renamed ZIP passes); normalize images
      through the `ChatImage` profile; mint one key per file; encrypt with `IChatBlobCipher`;
      `PutAsync` each object. Returns the descriptors for the caller to commit — **this service
      never calls `SaveChangesAsync`**
- [X] T014 [US1] Sanitise `FileName` on accept: trim, cap 255, strip control characters and both
      path separators, fall back to a generic name if nothing survives (data-model D4)
- [X] T015 [US1] **`ChatMessageService.SendAsync`** — change the empty-body refusal at L81 to
      consider attachments: refuse only when there is **no text and no files** (plan F1, the
      single most likely thing to be missed). Keep `Protect` unreachable for empty text — no
      text means `BodyCipher = []`, matching what the delete path and system lines already do
- [X] T016 [US1] `SendAsync` — build the `ChatMessage` **and** its `ChatAttachment` rows and
      commit them in **one `SaveChangesAsync`**, so a message can never be committed holding
      half its attachments (research R6). Objects are written **before** the commit; a failure
      leaves them unreferenced for the existing sweep, which is the harmless direction
- [X] T017 [US1] `backend/Controllers/ChatMessagesController.cs` — accept `multipart/form-data`
      on the existing send route (`body` 0..1, `files` 0..10) while **still accepting JSON** for
      a text-only send so existing callers are unaffected. Set `[RequestSizeLimit]` for
      10 × 10 MB plus overhead. Controller stays thin: bind, forward, shape (Gate 1)
- [X] T018 [US1] Add the `Content-Disposition` overload to
      `backend/Services/Media/MediaResponse.cs` taking the download file name, RFC 5987
      `filename*`-encoded. **The existing signature must keep behaving identically** — avatars
      and catalogue icons are untouched (research R9)
- [X] T019 [US1] Add `GET /chat/attachments/{id}` to the controller and its service method:
      read the row → resolve membership via the existing `ChatGuard` → **only then** open,
      decrypt and return (contract). **404 for every refusal** — not-found, not-permitted and
      unreadable are deliberately indistinguishable. `[EnableRateLimiting(MediaRead)]`.
      Disposition for everything that is not `image/webp`
- [X] T020 [US1] Add `AttachmentDto` and `MessageDto.Attachments` to
      `backend/Dtos/Chat/ChatDtos.cs`; project attachments in `ChatMessageService`'s existing
      message projections, ordered by `Ordinal`. **No URL, no object key** in the DTO — the
      client derives the path from the id
- [X] T021 [US1] `frontend/apps/web/src/app/core/services/chat.service.ts` — send as
      `FormData` when files are present. **Never auto-retry** this call: it is a mutation on the
      browser hop (Principle VII, research R7)
- [X] T022 [US1] Composer in
      `frontend/apps/web/src/app/features/chat/chat-conversation/chat-conversation.component.{html,ts}`
      — a `+` button opening a hidden `<input type="file" multiple>`, copying the established
      pattern from `profile-owner.component.html:67`. **Neutral/ghost styling, not coral**: the
      send button is the one brand CTA in this view and the template says so (plan F12). Tray
      state is **signals** — the app is zoneless (plan F13)
- [X] T023 [US1] Render non-image attachments as a named file row (name, size, download action)
      in the message bubble
- [X] T024 [P] [US1] Jest: the `+` opens the picker, a selected file appears in the tray and is
      individually removable, and send is possible with files and no text

**Checkpoint**: a PDF can be sent and downloaded by another member; a non-member gets 404.

---

## Phase 4: User Story 2 — Images show themselves in the message (P2)

**Goal**: shared photos appear in the thread rather than as file names.

**Independent test**: send a JPEG; the thread shows the picture, legible at 375 px, and
activating it opens a larger view.

- [X] T025 [P] [US2] Test: an uploaded JPEG carrying **EXIF GPS and a rotation flag** is stored
      with **no** EXIF/GPS/ICC and already upright; the stored type is `image/webp`; `Width`/
      `Height` are populated. Assert against the **stored bytes**, not the code path (SC-003)
- [ ] T026 [P] [US2] Test: an **animated GIF** is stored as a single frame (FR-017), which the
      existing pipeline already does — the test pins it
- [X] T027 [US2] Render image attachments as an inline preview, sized from `Width`/`Height` so
      the thread reserves space and does not jump as the image arrives
- [ ] T028 [US2] Multi-image layout: a group that stays within the thread at 375 px. A panorama
      and ten files on one message must not scroll the page horizontally (SC-009)
- [ ] T029 [US2] A larger view on activation, keyboard-dismissable, focus returned to the
      trigger
- [ ] T030 [US2] An attachment whose object cannot be read renders an **unavailable placeholder
      for that attachment alone**, leaving the rest of the conversation intact (FR-028) —
      mirroring how a missing avatar and an undecryptable message already behave
- [X] T031 [P] [US2] Jest for T027–T030

**Checkpoint**: photos render inline; a corrupt attachment does not break the thread.

---

## Phase 5: User Story 3 — Unsuitable files are refused clearly and safely (P3)

**Goal**: every refusal is clear, localized, and leaves the conversation untouched.

**Independent test**: attempt an oversized file, a disallowed type, a file whose extension lies,
and an eleventh file; each is refused and the conversation is unchanged.

- [X] T032 [P] [US3] Tests, one per refusal class: over 10 MB; an eleventh file; a disallowed
      type; **a ZIP renamed `.docx`** (the OOXML trap, research R2); **an executable renamed
      `.jpg`**; a truncated image; an empty submission with neither text nor files
- [X] T033 [P] [US3] Test SC-006 explicitly: after each refusal above there is **no message row,
      no attachment row and no stored object** — assert against the store, not just the API
- [X] T034 [US3] Return stable refusal codes per the contract; the response prose is the English
      fallback, not what the member is shown
- [X] T035 [US3] Frontend: map each code to its catalogue string, show it against the offending
      file in the tray, and leave the other selected files intact (FR-003). Client-side checks
      are for convenience only — the server is the boundary (Principle I)
- [ ] T036 [US3] Guard against the double-send: while an upload is in flight the same
      composition cannot be submitted again (FR-007). A failed send leaves the tray populated so
      the member can retry without re-picking

**Checkpoint**: every refusal class is covered and leaves no residue.

---

## Phase 6: User Story 4 — Withdrawing a message takes its files (P4)

**Goal**: "delete" means the bytes are gone, not unlisted.

**Independent test**: send a file, withdraw the message, confirm the usual tombstone and that
the file is unretrievable by everyone including a member who had the thread open.

- [X] T037 [P] [US4] Tests: after withdrawal the download endpoint returns **404** for every
      member; the attachment rows are gone; **the objects are gone from the store**; the thread
      shows the standard tombstone with **no file names or counts** left behind (FR-030)
- [X] T038 [P] [US4] Test: erasing an account **leaves** the attachments it sent in place, with the
      sender rendering as "A former player" — the corrected FR-031. Feature 037 promises members in
      three languages that their chat messages survive erasure, and an attachment is part of a
      message; deleting them would make other people's conversations half-gone. Their profile
      picture is still erased, which 037 already covers
- [X] T039 [US4] `ChatMessageService.DeleteAsync` — extend the existing clearing block (L557-563,
      which already clears `BodyCipher`, `LinkKind`, `LinkTargetId`) to delete the attachment
      rows **and** their objects. `PushMessageDeletedAsync` already fans the tombstone out to
      every member including the sender's other tabs — **no new realtime event** (plan F10)
- [X] T040 [US4] `backend/Services/Account/AccountDeletionService.cs` — **no change**, and that is
      the deliberate outcome of the FR-031 correction above. Add nothing to
      `EraseOwnedDataAsync`: attachments ride their messages, which survive. If a future reader
      thinks this is an oversight, the reasoning is in FR-031 and in feature 037's
      `RetainedCategories`
- [ ] T041 [US4] Confirm `MediaReconciliationService` sweeps the `chat-attachments/` prefix — it
      lists the whole container, so verify rather than assume, and extend if it is prefix-scoped

**Checkpoint**: withdrawal and erasure both remove bytes, verified against the store.

---

## Phase 7: Copy, law and the design gate

- [ ] T042 Correct the privacy policy in **all three** locales,
      `frontend/apps/web/public/i18n/legal/{en,de,es}.json`, **German authoritative**: members
      can now upload files into conversations — what is stored, that it is encrypted at rest,
      how long it is kept, and who can reach it. The existing uploaded-content paragraphs
      (de L51/L57/L128) name profile pictures and text; they now understate what is uploaded.
      `legal-catalog.spec.ts` enforces key parity
- [ ] T043 [P] Re-read feature 038's session-recording disclosure against inline image previews
      (FR-042): `maskLevel: moderate` masks **inputs**, not rendered content, so shared photos
      **are** captured in replays. Either the disclosure is widened or a `blockSelector` is
      applied to the thread — record which, and why, in this feature's residuals
- [ ] T044 Add the attachment marker to `LastMessageDto` and render the inbox label from the
      **client's** catalogues (research R11). Do **not** compose the prose server-side, and do
      **not** add a per-row attachment lookup to the inbox query — it is the hottest read in
      chat (data-model, "Where attachments are read")
- [ ] T045 Instantiate Gate 7: copy `.specify/templates/ui-review-checklist-template.md` to
      `specs/049-chat-attachments/checklists/ui-review.md` and verify **every** item against the
      diff, recording `file:line` for anything that fails. Feature-specific items to append:
      the `+` is not a second coral CTA (CHK002); the tray, preview, lightbox and file row all
      work at 375 px; image previews and file rows carry an accessible name (FR-044); the
      lightbox is keyboard-dismissable and returns focus. **DESIGN.md wins on any conflict** —
      report, do not silently resolve
- [ ] T046 Amend `specs/019-chat/` the way 022, 046 and 048 did: an "Amended by feature 049"
      callout, and a pointer from `contracts/chat-api.md` to
      [contracts/chat-attachments-api.md](./contracts/chat-attachments-api.md). Leave 019's
      message-body requirements alone — what a message *is* has widened, but nothing 019 said
      about text has changed
- [ ] T047 Full verification: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`,
      `cd frontend; npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`,
      `dotnet build`. Report anything skipped or failing — never claim a gate passed unrun

---

## Dependencies

- **Phase 2 blocks everything.** T004 (cipher) blocks T013; T006–T009 (entity) block T016.
- **US1 (Phase 3) blocks US2 and US4** — both need attachments to exist before they can render
  or delete them. **US3 (Phase 5) is independent of US2** and can run in parallel with it.
- T018 (`MediaResponse` overload) blocks T019.
- T042/T043 depend on nothing in code and can start any time.

## Parallel opportunities

T002, T003, T005 and T010 are four different files with no ordering between them. Within each
story the `[P]` tests are independent of one another. T042 and T043 can run alongside any phase.
