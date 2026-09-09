# Contract: `IChatMessageCipher`

**Feature**: 047 | `backend/Services/Chat/Encryption/`

The single seam through which chat message text becomes bytes and back. Registered as a
**singleton** — it holds parsed keys and no per-request state.

```csharp
namespace JuggerHub.Services.Chat.Encryption;

public interface IChatMessageCipher
{
    /// <summary>Encrypts <paramref name="plaintext"/> under the write key, bound to <paramref name="messageId"/>.</summary>
    byte[] Protect(string plaintext, Guid messageId);

    /// <summary>
    /// Decrypts. Returns false — never throws — when the envelope is malformed, names an
    /// unconfigured key version, or fails authentication.
    /// </summary>
    bool TryUnprotect(byte[] cipher, Guid messageId, out string plaintext);

    /// <summary>
    /// The key version a stored envelope names, or null when it is too short to say. Operator
    /// diagnostics only — it reads one plaintext byte and proves nothing about decryptability.
    /// </summary>
    byte? VersionOf(byte[] cipher);
}
```

> `VersionOf` was added during implementation. The decrypt-failure log line (FR-010) has to be
> useful without being dangerous, and the version is the one genuinely actionable fact in a failed
> row — it tells an operator whether they retired a key that rows still depend on. Reading it
> through the cipher rather than indexing `cipher[0]` at the call site keeps the envelope layout in
> one file.

## Behaviour

| # | Given | Then |
|---|---|---|
| C1 | `Protect("hello", id)` | 34 bytes: version, 12-byte nonce, 16-byte tag, 5-byte ciphertext |
| C2 | `Protect(x, id)` called twice with the same input | Two **different** byte arrays — the nonce is fresh each time |
| C3 | `TryUnprotect(Protect(x, id), id)` | `true`, and `plaintext == x`, for ASCII, emoji, combining marks, newlines and a full 2 000-character message |
| C4 | `Protect("", id)` | Throws `ArgumentException`. Callers must store `[]` for "no text" — the cipher never manufactures an empty-message envelope (data-model D2) |
| C5 | `TryUnprotect([], id)` | Throws `ArgumentException`. An empty array means "no text" and is the caller's business, not the cipher's |
| C6 | `TryUnprotect(cipher, **different id**)` | `false` — the associated-data binding rejects a relocated ciphertext |
| C7 | Any byte of the envelope flipped | `false` — GCM authentication, not a checksum |
| C8 | Envelope shorter than 29 bytes | `false`, no exception, no index-out-of-range |
| C9 | Version byte names a version not in configuration | `false`. The operator-facing signal is the caller's log line (FR-010), not an exception |
| C10 | Two versions configured, `"2:…;1:…"` | `Protect` stamps **2**; `TryUnprotect` reads rows stamped 1 **and** 2 (FR-006) |
| C11 | `Protect` on a 2 000-character all-4-byte-UTF-8 message | Succeeds; result is 8 029 bytes (FR-011) |

## Startup validation

Configuration key `Chat:Encryption:Keys`, one string, entries `version:base64key`
separated by `;`. **The first entry is the write key** (research §4).

| # | Configuration | Result |
|---|---|---|
| S1 | Missing or empty | Startup **throws**, naming `Chat:Encryption:Keys` (FR-007) |
| S2 | An entry whose key is not exactly 32 bytes after base64-decoding | Startup throws |
| S3 | An entry whose version is not an integer in 1..255 | Startup throws |
| S4 | Duplicate versions | Startup throws |
| S5 | Malformed base64, or an entry without `:` | Startup throws |
| S6 | Valid | Host starts; the parsed keys are held in memory only |

**No exception message, log line, or error response may contain key material** (FR-014).
Tests assert on the message text for S1–S5 precisely to prove this — the assertions check
that the configuration key is named and that no base64 fragment from the input appears.

There is no switch that turns encryption off. The absence of that switch is a requirement,
not an omission.
