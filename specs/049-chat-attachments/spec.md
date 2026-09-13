# Feature Specification: Chat File Attachments — Images, Documents and Other Files

**Feature Branch**: `049-chat-attachments`

**Created**: 2026-09-13

**Status**: Draft

**Input**: User description: "Chats should allow that a user can send and receive images and documents or other files. I would like to see a + Button that allows to add files to the Chat which opens the filepicker from OS. Images should unfurl to a preview in the message."

## Context & Decision

GitHub issue **#282** is the intake. Chat carries text and nothing else today: `ChatMessage`
has no media column, `MessageDto` has no attachment field, the composer has no attach
affordance, and the product as a whole has exactly one upload endpoint (the profile avatar)
and no document handling of any kind.

Two merged features already supply the plumbing this feature needs, and it rides both rather
than inventing a parallel path:

| Existing | Supplies |
|---|---|
| **034 / #98** — `IImageProcessor` | Named per-context profiles, header-only identify before decode, detected-type allowlist, decompression-bomb guard, EXIF/IPTC/XMP/ICC strip, resize, WebP re-encode |
| **035 / #97** — `IMediaStore` | Owner-agnostic object storage, private container in every environment, proxy-only delivery, authorization decided before the store is touched, orphan reconciliation |
| **047 / #223** — message encryption | The AES-256-GCM envelope, the key mechanism, and the promise that a database copy alone reveals nothing |

Four decisions were taken by the owner before specification and are recorded under
Clarifications. They are settled inputs, not open questions.

## Clarifications

### Session 2026-09-13 (owner)

- Q: Which files may be sent into a chat? → A: **An allowlist**, never a denylist: images
  (PNG, JPEG, WebP, GIF) plus PDF, plain text, and the common Office formats (docx, xlsx,
  pptx). Everything else is refused with a clear reason. A denylist was rejected as the
  weaker pattern — every newly dangerous type is a gap until somebody notices it — and the
  allowlist is what keeps executables, scripts, SVG and HTML out by construction rather
  than by vigilance. This matters more here than it would elsewhere, because the
  application serves these bytes from its own origin and has **no Content-Security-Policy
  header** (a known forward dependency, `specs/033-umami-analytics/spec.md:214`).
- Q: Feature 047 encrypts message text at rest. Should attachment bytes get the same
  treatment? → A: **Yes.** The same envelope and the same key mechanism, bound to the
  attachment's own row. Storing the bytes access-controlled but unencrypted was rejected
  because 047 bought the promise that a database copy on its own reveals no message
  content, and an unencrypted attachment beside an encrypted body makes that promise half
  true in a way the privacy policy already states in three languages. Accepted costs:
  decryption on every serve, and no HTTP range requests — so no seeking inside a large
  file.
- Q: For images, what exactly is stored and what does the recipient get? → A: **Normalized
  through the 034 pipeline** — decoded, stripped of EXIF/GPS/ICC, re-encoded, capped at a
  maximum dimension, under a new chat-specific profile alongside `Avatar` and `Icon`.
  Keeping the original alongside a generated thumbnail was rejected: it doubles the stored
  objects per image and leaves the original carrying location data unless stripped
  separately. Accepted cost: the recipient receives the normalized image, never the
  untouched original.
- Q: What limits apply? → A: **10 MB per file, at most 10 files per message.** Comfortably
  covers a phone photo and a tournament PDF without inviting video. Consistent with the
  generous-input posture of the existing 8 MB avatar cap.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send a file into a conversation (Priority: P1)

A player is arranging a friendly match. They open the conversation, press the `+` button
next to the message box, pick the hall booking PDF from their computer or phone, and send
it. Everyone in the conversation sees the message arrive with the file named and available
to open.

**Why this priority**: This is the feature. Without it nothing else here exists, and on its
own — even with every file rendering as a plain named row — it already removes the reason
players leave the platform to share something.

**Independent Test**: In a conversation, press `+`, choose a PDF, send, and confirm the
message appears for both sender and recipient carrying the file's name, that the file can
be opened, and that what comes back is byte-for-byte what was sent.

**Acceptance Scenarios**:

1. **Given** a conversation the player may post in, **When** they press `+`, choose one
   allowlisted file and send, **Then** the message appears in the thread for every member
   with the file attached, named as it was on their device.
2. **Given** a selected file, **When** the player sends it with accompanying text, **Then**
   the text and the file arrive as **one** message, not two.
3. **Given** a selected file, **When** the player sends it with no text at all, **Then** the
   message is accepted — a file is enough on its own.
4. **Given** a member of the conversation who was offline, **When** they open it later,
   **Then** the attachment is present in history exactly as other members see it.
5. **Given** a player picks several allowlisted files at once in the picker, **When** they
   send, **Then** all of them arrive on the same message in the order chosen.

---

### User Story 2 - Images show themselves in the message (Priority: P2)

A player shares three photos from Saturday's training. Rather than three file names their
teammates must open one by one, the message shows the pictures directly in the thread, and
tapping one opens it larger.

**Why this priority**: It is the difference between a file-transfer feature and a chat
feature, and it is the half of the request most visible to members. It ranks below P1 only
because P1 must exist for it to have anywhere to render.

**Independent Test**: Send a JPEG; confirm the thread shows the picture itself rather than a
file name, that it is legible at 375 px width, and that opening it shows a larger view.

**Acceptance Scenarios**:

1. **Given** an allowlisted image is attached, **When** the message renders, **Then** the
   image is shown in the message body rather than as a file name.
2. **Given** an image preview, **When** a member activates it, **Then** a larger view opens.
3. **Given** a message carrying several images, **When** it renders, **Then** they are shown
   as a group that stays within the thread's width at the narrowest supported size.
4. **Given** a photo carrying GPS coordinates and a rotation flag, **When** it is sent,
   **Then** what every recipient receives has no embedded location or camera data and is
   already the right way up.
5. **Given** a non-image file, **When** the message renders, **Then** it appears as a named
   file row with a download action and never as a broken picture.

---

### User Story 3 - Unsuitable files are refused clearly and safely (Priority: P3)

Someone attaches a 40 MB video, a file type the platform does not accept, or a document that
turns out to be corrupt. They are told plainly what is wrong, in their own language, and
nothing half-sent is left in the conversation.

**Why this priority**: It is what makes P1 safe to expose, and it protects the conversation
from partially-sent messages. It ranks below the paths that deliver value, but nothing ships
without it.

**Independent Test**: Attempt each of an oversized file, a disallowed type, a file whose
extension lies about its contents, and an eleventh file; confirm each is refused with a clear
reason and that the conversation is unchanged.

**Acceptance Scenarios**:

1. **Given** a file above the per-file size limit, **When** it is attached, **Then** it is
   refused with a reason naming the limit, and any other selected files are unaffected.
2. **Given** a file whose real content is not what its name claims, **When** it is submitted,
   **Then** it is refused — the claimed type is never what the decision rests on.
3. **Given** an eleventh file on one message, **When** it is attached, **Then** it is refused
   with a reason naming the per-message limit.
4. **Given** any refusal, **When** the player looks at the conversation, **Then** no partial
   message was posted and no file was stored.
5. **Given** a refusal, **When** the player is using the platform in German or Spanish,
   **Then** the reason is in that language.
6. **Given** an upload that fails midway (connection lost), **When** the player retries,
   **Then** they are not left with a duplicate message from the first attempt.

---

### User Story 4 - Withdrawing a message takes its files with it (Priority: P4)

A player sends the wrong document into a team chat and withdraws the message. The file stops
being reachable — not merely hidden from the thread.

**Why this priority**: It preserves a guarantee the product already makes about message text
(019 FR-050: the content is *genuinely gone from the row*, not behind a flag). Ranked last
because it is a property of the paths above rather than a journey of its own, but it is not
optional: without it, "delete" quietly becomes "unlist".

**Independent Test**: Send a file, withdraw the message, and confirm the thread shows the
usual neutral tombstone and the file is no longer retrievable by anyone, including by a
member who had the conversation open.

**Acceptance Scenarios**:

1. **Given** a message with attachments, **When** its sender withdraws it, **Then** the
   attachments become unretrievable for every member.
2. **Given** a withdrawn message, **When** the thread renders, **Then** it shows the same
   neutral tombstone as a withdrawn text message, with no file names left behind.
3. **Given** a player erases their account, **When** the erasure completes, **Then** the
   files they sent are reclaimed along with the rest of their owned data.

---

### Edge Cases

- **A file that is an image by name but not by content, or vice versa.** The decision rests
  on the detected content, never the claimed one — a `.jpg` that is really a script is
  refused, and a correctly-formed image with a wrong extension is treated as the image it is.
- **An image that survives validation but fails to normalize** (truncated, corrupt beyond the
  header). It is refused with the same non-technical reason as any unreadable image, and the
  message is not posted.
- **A message whose only content is attachments.** The inbox last line, push/email
  notification, and any other place that quotes a message have no text to quote and need
  something to say.
- **A conversation that is snapshotted** (team deleted, event cancelled). Chat is snapshotted
  rather than deleted in those cases; whether attachments survive the snapshot is stated
  here, not left to be discovered.
- **A member who is removed from a conversation**, or blocks the sender, after a file was
  sent. Reachability of the bytes follows the same rule as reachability of the message.
- **The same file sent twice**, or sent to two conversations. Each send is its own attachment
  with its own lifecycle; withdrawing one must not affect the other.
- **An attachment whose stored object has vanished** (reconciliation raced, storage
  unavailable). It degrades to a "not available" state on that one attachment rather than
  failing the conversation — mirroring how a missing avatar and an undecryptable message
  already behave.
- **Session replay.** Feature 038 records the rendered screen at `maskLevel: moderate`, and
  inline image previews will therefore be captured in replays. The existing disclosure was
  written when chat was text only.
- **A very wide or very tall image** (panorama, long screenshot). The preview must not break
  the thread layout at the narrowest supported width.
- **Two members sending to the same conversation simultaneously**, one with attachments.
  Message order stays the server's order; attachments do not reorder a thread.

## Requirements *(mandatory)*

### Functional Requirements

**Composing and sending**

- **FR-001**: The chat composer MUST offer a `+` control that opens the operating system's
  file picker.
- **FR-002**: The picker MUST allow selecting several files at once.
- **FR-003**: Selected files MUST be shown to the sender before sending, each removable
  individually, so a mistaken pick can be corrected without abandoning the message.
- **FR-004**: A message MUST be sendable with attachments and no text.
- **FR-005**: Text and attachments submitted together MUST arrive as exactly one message.
- **FR-006**: Attachments MUST preserve the order in which the sender chose them.
- **FR-007**: The sender MUST see that an upload is in progress, and MUST be prevented from
  sending the same composition twice while it is.
- **FR-008**: A message MUST be posted only if all of its attachments were accepted and
  stored; a failure anywhere MUST leave the conversation unchanged (no partial message, no
  orphaned file visible to anyone).

**What may be sent**

- **FR-009**: The platform MUST accept only an allowlist of file types: images (PNG, JPEG,
  WebP, GIF), PDF, plain text, and the Office document formats (docx, xlsx, pptx).
- **FR-010**: The type decision MUST rest on the file's detected content, never on its name,
  extension, or the type the client declares.
- **FR-011**: Any file outside the allowlist MUST be refused, regardless of how it is named.
- **FR-012**: A single file MUST NOT exceed 10 MB, and a single message MUST NOT carry more
  than 10 files.
- **FR-013**: Every limit and the allowlist MUST be enforced on the server. Client-side
  checks exist for the sender's convenience and are never the boundary (Principle I).
- **FR-014**: The sender MUST be permitted to post in the conversation for an attachment to
  be accepted — the same rule that governs sending text, applied before anything is stored.

**Images**

- **FR-015**: Images MUST be normalized before storage: decoded, embedded metadata
  (EXIF/GPS/IPTC/XMP/ICC) removed, orientation baked into the pixels, bounded to a maximum
  dimension, and re-encoded to a single delivery format.
- **FR-016**: A normalized image MUST be what every recipient receives; the original bytes
  MUST NOT be retrievable.
- **FR-017**: Animated images MUST NOT be stored as animation — a still image is served.
- **FR-018**: An image MUST render as a preview inside the message, and MUST offer a larger
  view when activated.
- **FR-019**: A message with several images MUST present them as a group that fits the
  thread at the narrowest supported width.
- **FR-020**: A file that is not an image MUST render as a named row carrying its original
  file name, its size, and a download action.

**Storage, protection and delivery**

- **FR-021**: Attachment bytes MUST be stored in the media object store, never in a database
  column.
- **FR-022**: Attachment bytes MUST be encrypted at rest with the same mechanism used for
  message text, bound to the individual attachment so a stored object cannot be relocated to
  another row and still be read.
- **FR-023**: The location of a stored object MUST NOT reach a client — not in a response
  body, not in a header, not in a link.
- **FR-024**: Every retrieval MUST be authorized per request, against the conversation's own
  membership rules, **before** any byte is fetched from the store.
- **FR-025**: A retrieval that is refused, for any reason, MUST be indistinguishable from one
  for a file that does not exist, so the endpoint never reveals what exists.
- **FR-026**: A file that is not a normalized image MUST be delivered so that browsers save
  it rather than render it in the application's origin.
- **FR-027**: Attachment retrieval MUST be rate limited.
- **FR-028**: An attachment whose stored object cannot be read MUST degrade to an
  unavailable state on that attachment alone, leaving the rest of the conversation intact.

**Lifecycle**

- **FR-029**: Withdrawing a message MUST make its attachments unretrievable and MUST remove
  the stored bytes, matching the existing guarantee that a withdrawn message's content is
  genuinely gone rather than hidden.
- **FR-030**: A withdrawn message MUST leave no attachment metadata visible — no file names,
  no counts.
- **FR-031**: Erasing an account MUST reclaim the attachments that account sent.
- **FR-032**: Stored objects left without a referent MUST be reclaimable by the existing
  maintenance sweep rather than by a new mechanism.
- **FR-033**: The specification MUST state whether attachments survive a conversation
  snapshot (team deleted, event cancelled) and the chosen behaviour MUST be applied
  consistently.

**Everything that quotes a message**

- **FR-034**: The inbox last-line preview MUST describe an attachment-only message rather
  than showing an empty line.
- **FR-035**: Notifications about an attachment-only message MUST likewise have something to
  say, and MUST NOT include the file's contents.
- **FR-036**: Attachments MUST reach members already viewing the conversation by the same
  live delivery path as message text, with no reload required.
- **FR-037**: An attachment MUST NOT make a conversation searchable by file name or contents
  — the inbox search added by 046 finds conversations by name only, and that boundary is
  unchanged.
- **FR-038**: The existing link-card behaviour MUST be unaffected: the platform still never
  fetches an external URL, and an attachment is not an unfurl.

**Language, law and design**

- **FR-039**: Every new or changed string MUST exist in all three catalogues (English,
  German, Spanish) in the same change.
- **FR-040**: Refusal reasons MUST be non-technical and MUST be shown in the member's
  language.
- **FR-041**: The privacy policy MUST be corrected in all three locales (German
  authoritative): members can now upload files into conversations, and what the platform
  stores, how long, and who can reach it must be stated accurately.
- **FR-042**: The disclosure about session recording MUST be re-checked against inline image
  previews, which are rendered content and therefore captured.
- **FR-043**: All new surface (the `+` control, the attachment tray, the image preview, the
  file row, the larger view) MUST be verified against DESIGN.md via the UI review checklist
  (Gate 7), including at 375 px and with a keyboard.
- **FR-044**: Image previews and file rows MUST carry an accessible name — the file's name at
  minimum — so a screen reader announces something other than "image".

### Key Entities

- **Chat attachment**: one file sent on one message. Belongs to exactly one message, carries
  the name the sender's device gave it, its size, what kind of file it is, enough to render
  it (for an image, its dimensions), and a reference to where the bytes live. Its lifetime is
  its message's lifetime. Several attachments may hang off one message, in a stable order.
- **Chat message** *(existing)*: gains the ability to carry attachments and the ability to
  exist with no text. Everything else about it — its ordering by identifier, its encryption,
  its withdrawal semantics, its link card — is unchanged.
- **Stored object** *(existing, 035)*: the bytes. Owner-agnostic; attachments become a fourth
  kind of media alongside avatars and the two catalogue icons.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member can attach and send a file from the OS picker in **at most three
  interactions** from an open conversation (press `+`, choose, send).
- **SC-002**: **100%** of files outside the allowlist are refused, including every file whose
  extension disagrees with its content.
- **SC-003**: **Zero** stored images contain embedded location, camera, or colour-profile
  metadata, verified by inspecting stored bytes rather than by inspecting the code.
- **SC-004**: **Zero** responses anywhere in the feature disclose a stored object's location.
- **SC-005**: After a message is withdrawn, **no** member — including one who had the
  conversation open at the time — can retrieve its attachments.
- **SC-006**: A refused upload leaves the conversation **byte-identical** to its state before
  the attempt: no message, no attachment row, no stored object.
- **SC-007**: An image sent from a phone is visible in the recipient's thread **without the
  recipient taking any action** to reveal it.
- **SC-008**: Every string introduced by the feature is present in **all three** catalogues;
  the parity guard passes.
- **SC-009**: The thread does not scroll horizontally at **375 px** for any combination of
  attachments, including a panorama and ten files on one message.
- **SC-010**: A message carrying the maximum ten files renders in the thread without the
  conversation taking measurably longer to open than a text-only one.
- **SC-011**: A conversation with an unavailable attachment still opens and renders every
  other message.

## Assumptions

- Members are sending files of the kind a sports community exchanges — schedules, booking
  confirmations, photos from training, a rules PDF. Video, archives and executables are out
  by the allowlist decision, and no demand for them is assumed.
- The existing image pipeline (034) and object store (035) are sound and are extended rather
  than modified; a new processing profile is an addition, not a change to avatar behaviour.
- The encryption mechanism from 047 is reusable for bytes as it is for text, with its key
  supplied through the same channel.
- All environments contain test data only, so no migration of existing content is required —
  consistent with the position taken by 047.
- Every conversation kind (direct, group, team, party, and the two inquiry kinds) gets the
  same capability; no kind is singled out for a different rule.
- The absence of a Content-Security-Policy header remains the case for this feature. The
  allowlist and the save-rather-than-render rule are therefore load-bearing, not defence in
  depth. Adding the header is out of scope and belongs to its own issue.

## Out of Scope

- **A Content-Security-Policy header.** Needed, tracked separately; this feature must be safe
  without it.
- **Editing an attachment after sending**, or adding a file to a message already sent.
- **Forwarding an attachment** to another conversation.
- **A per-conversation or per-account storage quota.** Limits here are per file and per
  message; cumulative growth is a real consequence and is recorded as a residual, not solved.
- **Virus and malware scanning.** The allowlist is the control chosen; scanning is a separate
  decision with a dependency the product does not have.
- **Attachments anywhere but chat** — news posts, team and event pages, marketplace listings.
  Galleries are #99; event images are #190.
- **Searching by file name or file contents.** 046 deliberately removed message-content
  search; this feature does not reintroduce it by another door.
- **Video and audio**, including voice messages.
- **End-to-end encryption.** Declined for message text in 047 and not revisited here.
