# Data Model: Chat Message Encryption at Rest

**Feature**: 047 | **Date**: 2026-09-09 | See [research.md](./research.md) §1–§4 for the reasoning.

**No new entity. No new table. One column replaced.**

---

## Changed entity — `ChatMessage`

`backend/Entities/ChatMessage.cs`

| Before | After |
|---|---|
| `public string Body { get; set; } = string.Empty;` | `public byte[] BodyCipher { get; set; } = [];` |
| `varchar(2000)`, `IsRequired()` | `bytea`, `IsRequired()`, no max length |

Every other property is untouched: `Id`, `ConversationId`, `SenderId`, `Kind`, `IsDeleted`,
`SystemEvent`, `SystemSubjectUserId`, `LinkKind`, `LinkTargetId`, and the `Conversation` /
`Sender` navigations.

### D1 — The rename is the safeguard

The property is renamed, not merely retyped. A projection that selects `BodyCipher` and puts
it somewhere a player will read is visibly wrong; a projection that selects `Body` and gets
plaintext from a converter is invisibly right until it isn't (research §1). Every one of the
five call sites is forced to acknowledge the change at compile time.

### D2 — Empty array means "no text", and must stay that way

| Row | `BodyCipher` |
|---|---|
| A member's message | 29 + *n* bytes (§3 envelope) |
| A system line (`Kind = System`) | `[]` |
| A message the sender deleted | `[]` — cleared on delete, as today |

Encrypting the empty string would produce 29 bytes indistinguishable from a very short
message, and "the content is genuinely gone from the row" (feature 019, data-model R12)
would stop being observable. `ChatDeleteTests` L72's `Assert.Equal(string.Empty, row.Body)`
becomes `Assert.Empty(row.BodyCipher)` and keeps its meaning exactly.

### D3 — `bytea` is what makes FR-013 structural

Nothing may match, sort or filter on message text in SQL again. With `bytea` that is not a
convention to remember — `ILike` over a byte array does not compile. 046 removed the last
such query by hand; this makes reintroducing one impossible without a deliberate schema
change.

### D4 — Ordering is unaffected

`ChatMessage`'s entity documentation states that ordering is the UUIDv7 `Id`, never
`CreatedDate`, and that the id doubles as the read cursor
(`ConversationParticipant.LastReadMessageId`) and the keyset paging cursor. **None of that
touches the body.** Encryption changes what a row *contains*, never where it sits.

### D5 — The XML doc must change with the column

The existing remark — *"`Body` is plain text and is never markup… stored verbatim"* — becomes
false on the day this ships. The replacement says: stored as an authenticated-encryption
envelope (research §3); still never markup once decrypted, because the client still binds it
as text and that is what closes stored XSS (019 FR-014); and the decrypted value is only
ever produced by `IChatMessageCipher`.

---

## Envelope layout (the value stored in `BodyCipher`)

| Offset | Bytes | Meaning |
|---|---|---|
| 0 | 1 | Key version, 1–255, plaintext so it is readable without trial decryption (FR-005) |
| 1 | 12 | GCM nonce, fresh from `RandomNumberGenerator` per message |
| 13 | 16 | GCM authentication tag |
| 29 | *n* | Ciphertext, *n* = UTF-8 byte length of the plaintext |

**Associated data = the message's `Id` (16 bytes).** `BaseEntity` assigns the UUIDv7 in its
field initialiser, so it exists before the insert and never changes. An operator with write
access therefore cannot relocate a ciphertext from one row to another: the tag check fails
and the message reads as unavailable rather than as someone else's words.

Worst case size: 2 000 characters × 4 UTF-8 bytes + 29 = 8 029 bytes. `bytea` is unbounded;
the 2 000-character limit stays a check on the trimmed plaintext in `SendAsync` (FR-011).

---

## Migration — `EncryptChatMessageBodies`

```text
DropColumn   ChatMessages.Body        (varchar(2000))
AddColumn    ChatMessages.BodyCipher  (bytea, NOT NULL, defaultValue: new byte[0])
```

- **No backfill, no dual read, no plaintext compatibility** (FR-025). Every environment
  holds test data only; existing rows keep their metadata and lose their text, which is
  acceptable and was the owner's explicit position. Dropping and adding — rather than
  altering in place — states that plainly instead of implying a conversion happened.
- `Down` restores `Body` as an empty `varchar(2000)`. It cannot restore content; the
  migration comment says so rather than leaving a reversal that looks lossless.
- The startup auto-migration in `Program.cs` applies it in every environment, including the
  Testcontainers harness.

---

## No entity for keys

The key set is **configuration**, not data (research §4). It is a single secret value,
parsed once at startup into an immutable in-memory map, and it never touches the database —
which is the entire point: a database copy that also contained the keys would protect
nothing.

---

## Contract change

`MessageDto` gains one field:

```csharp
public sealed record MessageDto(
    …,
    bool IsDeleted,
    bool IsUnavailable,   // ← new: the stored text could not be decrypted (FR-009)
    …);
```

`IsUnavailable` and `IsDeleted` are never both true — a deleted row has no ciphertext to
fail on. `Body` is `""` whenever either is set. `LastMessageDto` is **not** changed:
an unreadable last message previews as empty, the same as a deleted one (research §5).

See [contracts/chat-message-cipher.md](./contracts/chat-message-cipher.md) for the service
contract and [contracts/chat-api-delta.md](./contracts/chat-api-delta.md) for the wire shape.
