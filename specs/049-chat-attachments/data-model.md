# Data Model: Chat File Attachments

One new entity, one migration. Nothing existing changes shape; `ChatMessage` gains a collection
and an amended XML doc.

---

## `ChatAttachment`

One file sent on one message.

| Column | Type | Null | Note |
|---|---|---|---|
| `Id` | `uuid` | no | `BaseEntity`, UUIDv7. **Also the cipher's associated data** (D2) |
| `ChatMessageId` | `uuid` | no | FK → `ChatMessages`, **Cascade** (D1) |
| `ObjectKey` | `varchar(200)` | no | Where the bytes live. **Never leaves the backend** |
| `ContentType` | `varchar(128)` | no | The *stored* type — `image/webp` for every image (D3) |
| `SizeBytes` | `int` | no | Size of the stored object, after normalization |
| `FileName` | `varchar(255)` | no | The sender's original name. Display data only (D4) |
| `Ordinal` | `int` | no | The sender's chosen order (FR-006) |
| `Width` | `int` | yes | Images only |
| `Height` | `int` | yes | Images only |
| `CreatedDate` / `ModifiedDate` | `timestamptz` | no | `BaseEntity`, set by the interceptor |

**Indexes**: `(ChatMessageId, Ordinal)` — every read is "the attachments of these messages, in
order", and it is the only access pattern.

### D1 — Cascade from the message, but objects still need explicit reclamation

Cascade keeps the rows honest: no attachment row can outlive its message. It does **nothing**
for the blobs, which are not in the transaction. So the delete path deletes objects explicitly
(plan F10), and the reconciliation sweep is the backstop for whatever a crash leaves behind.

Stating it plainly because the cascade makes the row side look finished and invites the
conclusion that the object side is too.

### D2 — `Id` is the associated data, not `ChatMessageId`

The ciphertext is bound to the **attachment's own** id. Binding to the message id would let an
operator with write access swap two attachments *within* a message and have both still
authenticate.

`BaseEntity` assigns `Id` in its field initialiser, so it exists the moment the object does —
the same property 047 relies on. Encrypt **after** construction, never in an initialiser.

### D3 — `ContentType` is the stored type, and `IsImage` is derived from it

After normalization every image is `image/webp`; nothing else is. So "is this an image?" is
`ContentType == "image/webp"`, computed at projection time.

**No `IsImage` column.** It would be a second source of truth that a future projection could
disagree with, and the disagreement would be invisible — an image rendering as a file row, or a
PDF rendering as a broken picture.

### D4 — `FileName` is display data and never reaches a path

It fills the file row and the download disposition and nothing else. `ObjectKey` is a UUIDv4
minted independently (R10).

On accept: trim, cap at 255 characters, strip control characters and both path separators, and
fall back to a generic localized name if nothing survives. On serve: RFC 5987 `filename*`
encoding, or a name containing a quote or newline is header injection.

### D5 — `SizeBytes` is the stored size, not the uploaded size

For an image the two differ, often by a lot, and the file row shows what the recipient will
download. The 10 MB limit is enforced on the **input**, before processing (FR-012/FR-013).

---

## `ChatMessage` — amended, not reshaped

Gains `public ICollection<ChatAttachment> Attachments { get; set; } = [];`. No new column.

### D6 — `BodyCipher.Length == 0` now has a THIRD meaning, and the doc must say so

The entity's XML doc currently enumerates exactly two:

> a system line has nothing to say, and a deleted message's content is genuinely gone

An attachment-only message is a third. It is a legitimate member message, from a real sender,
with a real attachment, and no text.

**This is the highest-value comment in the diff.** Without it the next reader sees a
`Member`-kind row with an empty body and concludes the row is corrupt — or "fixes" it.

What must **not** change:

- `ReadBody` (L441) already maps zero-length to `("", false)` — empty text, **not** unavailable.
  It needs no branch for attachments, and adding one produces dead code that looks necessary.
- `Protect` is still never called with `""` (R5). No text means no envelope.
- `IsUnavailable` still means *the stored text would not decrypt* and nothing else.

### D7 — the delete path clears attachments the same way it clears text

`DeleteAsync` clears `BodyCipher`, `LinkKind` and `LinkTargetId` under a comment explaining that
a flag would make "deleted" a rendering convention rather than a fact. Attachments join that
list: rows removed, objects deleted.

A withdrawn message must leave **no attachment metadata** either (FR-030) — file names and
counts are content.

---

## Migration

One migration: create `ChatAttachments`, its FK and its index. **No backfill, nothing dropped,
nothing altered.** Existing messages simply have no attachments.

If a task produces a second migration, or an `ALTER` against `ChatMessages`, something has gone
wrong.

---

## Where attachments are read

| Reader | Shape |
|---|---|
| Message page (keyset-paged) | Attachments of the page's messages, one projection, ordered by `(ChatMessageId, Ordinal)` |
| A single message after send | Same projection, one id |
| Inbox row | **Not read.** Only a marker on `LastMessageDto` (R11) — the inbox must not fan out to a second table per row |
| Download endpoint | One row by id, for its `ConversationId`, `ObjectKey`, `ContentType`, `FileName` |

The inbox rule is the one worth guarding: the inbox is the hottest read in chat and 046 already
kept its query to a single shape. A per-row attachment lookup would undo that quietly.
