# API Contract: Chat File Attachments

Amends `specs/019-chat/contracts/chat-api.md`. One endpoint changes shape, one is added.

---

## Changed — `POST /api/v1/chat/conversations/{conversationId}/messages`

Today: `application/json`, `{ "body": "…" }`.
Now: **`multipart/form-data`**, so text and files arrive as one request (plan D4).

| Part | Cardinality | Note |
|---|---|---|
| `body` | 0..1 | The message text. May be **absent or empty when at least one file is present** (FR-004) |
| `files` | 0..10 | The attachments, in the sender's chosen order |

`[RequestSizeLimit]` is set to accommodate 10 × 10 MB plus multipart overhead. The per-file and
per-count limits are enforced **before** any file is read into memory or processed.

JSON requests continue to be accepted for a text-only send, so existing callers and the
realtime-only paths are unaffected.

### Responses

| Status | When |
|---|---|
| `200` | `MessageDto`, now carrying `attachments` |
| `400` | Nothing to send: no text **and** no files |
| `400` | A file is too large, of a disallowed type, unreadable, or there are more than ten |
| `403` | Blocked in a direct conversation |
| `404` | Not a member — never distinguished from "no such conversation" |
| `409` | The conversation is archived |
| `413` | The whole request exceeds the transport limit |

**Every 4xx leaves the conversation unchanged** — no message row, no attachment row, no stored
object visible to anyone (FR-008).

Refusal reasons are non-technical and carry a stable code so the client can localize them
(FR-040); the prose in the response is the English fallback, not what the member is shown.

| Code | Meaning |
|---|---|
| `file_too_large` | Over the per-file limit |
| `too_many_files` | Over the per-message limit |
| `unsupported_type` | Not on the allowlist — by **detected** content, not by name |
| `unreadable_file` | Corrupt, truncated, or not what its header claims |
| `empty_message` | No text and no files |

---

## Added — `GET /api/v1/chat/attachments/{attachmentId}`

Returns the decrypted bytes of one attachment.

**Authorization**: the caller must be a member of the attachment's conversation, decided against
the descriptor row **before** the object store is touched (FR-024) — the same three-step shape
as `GetAvatarAsync`:

1. read the attachment row and its `ConversationId`
2. resolve membership through the existing `ChatGuard`
3. **only then** open the object, decrypt, and return

**Rate limited** under the existing `MediaRead` policy.

### Responses

| Status | When |
|---|---|
| `200` | The bytes |
| `304` | The caller's `If-None-Match` matches |
| `404` | No such attachment, **or** not permitted, **or** the object is unreadable, **or** its message was withdrawn |

`404` for every refusal, deliberately: the endpoint must never become an existence oracle
(FR-025), exactly as the avatar endpoint already is not.

### Headers

| Header | Value |
|---|---|
| `Cache-Control` | `private, no-cache` — never `public`; revocation takes effect on the next request |
| `ETag` | SHA-256 fingerprint of the object key, **never the key** |
| `Content-Type` | The stored type |
| `Content-Disposition` | **`attachment; filename*=UTF-8''…` for everything that is not `image/webp`** (FR-026, R1) |

There is no `Accept-Ranges` and range requests are not honoured (R8) — a consequence of
encrypting at rest.

---

## DTO changes

### `AttachmentDto` — new

```
Id           Guid      — the download URL is derived from this
FileName     string    — the sender's original name, for display and download
ContentType  string    — the STORED type; image/webp for every image
SizeBytes    int       — the stored size, which is what a download costs
Width        int?      — images only
Height       int?      — images only
```

**No URL and no object key.** The client builds `/api/v1/chat/attachments/{id}`, matching the
`hasAvatar` convention used everywhere except chat's own conversation avatars. The object key
never appears in a DTO, a header, or a link (FR-023).

`Width`/`Height` are present so the thread can reserve the right space before the image loads
and not jump as it arrives.

### `MessageDto` — one field added

```
IReadOnlyList<AttachmentDto> Attachments   — empty for a message without files
```

Everything else is unchanged. `Body` is `""` for an attachment-only message and
`IsUnavailable` keeps its existing meaning — *the stored text would not decrypt* — and is
**never** true merely because a message has no text (data-model D6).

### `LastMessageDto` — one field added

```
int AttachmentCount   — 0 for a message without files
```

The **client** renders the label from its own catalogues (R11). The server sends no prose.

---

## Unchanged, and deliberately so

- **Realtime.** The existing message-sent and message-deleted pushes carry attachments because
  they carry `MessageDto`. No new event (plan F10).
- **Link cards.** An attachment is not an unfurl. The platform still never fetches an external
  URL (FR-038); `ChatLinkKind` and `ChatLinkResolver` are untouched.
- **Search.** 046's inbox search matches conversation and member names only. File names are
  **not** searchable (FR-037).
- **Unread counting.** An attachment-only message counts exactly like any other.
- **`MediaResponse`'s existing signature.** Avatars and catalogue icons keep calling it and keep
  behaving identically; the download disposition is a new overload (R9).
