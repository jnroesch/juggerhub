# Research: Chat Message Encryption at Rest + Database Transport Hardening

**Feature**: 047 | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md) | **Issue**: [#223](https://github.com/jnroesch/juggerhub/issues/223)

Every decision below was taken by reading the code it affects. Where the issue's own
suggestion did not survive that reading, the section says so.

---

## §1 — The EF value converter the issue proposed does not work here

#223 suggests: *"An EF value converter on `Body` keeps every projection unchanged."* That is
the attractive option and it is the first thing this research tried. It fails on three
counts, each independently fatal:

1. **A converter cannot produce the localized placeholder FR-009 requires.** A
   `ValueConverter<string,string>` is a pure expression pair with no access to the request's
   culture. The neutral "this message can't be displayed" text has to be chosen by something
   that knows who is reading — which in chat is either `MemberPlaceholder.For(culture)`
   server-side or the frontend catalogue. Neither is reachable from inside the model.
2. **A converter that throws takes the whole page down.** Decryption failure inside a
   converter surfaces during `ToListAsync()` materialisation, so one unreadable row fails the
   entire conversation request — exactly the behaviour the owner rejected on 2026-09-09.
   Catching inside the converter only moves the problem: the converter must then return a
   sentinel `string`, and any sentinel a player could also type is a forgery surface.
3. **A converter cannot bind the ciphertext to its row (§4).** Authenticated encryption with
   the message id as associated data needs the id, which a single-property converter never
   sees.

**Decision**: no value converter. The entity property is **renamed and retyped** —
`ChatMessage.Body` (`string`, plaintext, `varchar(2000)`) becomes
`ChatMessage.BodyCipher` (`byte[]`, `bytea`) — and decryption happens in the chat services'
existing DTO-mapping step, which is where the culture and the row id are both already in
hand.

**Why the rename is the safeguard.** With a converter, a future projection that selects the
property gets plaintext for free, and the guarantee is invisible. With the rename, every
site that wants readable text has to select something called `BodyCipher` and hand it to
`IChatMessageCipher`. A reviewer sees ciphertext being put somewhere it does not belong.
This is the same reasoning 046 used for deleting `SearchMessagesAsync` rather than hiding
it: removed, not hidden.

**Alternatives considered**: a `MessageText` readonly-struct CLR type with a struct↔`byte[]`
converter (compiler-enforced at every site, but still cannot localize and still throws
inside materialisation); a converter plus a reference-identity sentinel string (works, but
depends on string interning behaviour to stay correct — unreviewable).

---

## §2 — `bytea`, not base64 `text`

The ciphertext column is `byte[]` → `bytea`.

- **It makes FR-013 structural.** You cannot `ILike` a `bytea` against a string. The
  requirement that nothing ever matches on message text again is enforced by the type
  system rather than by remembering. 046 removed the last such query; this makes
  reintroducing one a compile error rather than a review catch.
- 33 % smaller than base64 for the same payload, and no encoding step to get wrong.
- `HasMaxLength` is not set. A 2 000-character message is at most 8 000 UTF-8 bytes plus a
  29-byte envelope (§3); `bytea` is unbounded and the *player-facing* limit stays where it
  is, on the trimmed plaintext in `SendAsync` (FR-011).

**Empty means empty.** System lines and deleted messages store a **zero-length array**, not
the encryption of an empty string (FR-008). Two reasons: encrypting nothing produces 29
bytes that look exactly like a short message, so "the content is genuinely gone" would stop
being visible in the row; and the existing feature-019 assertion `row.Body == string.Empty`
carries over as `Assert.Empty(row.BodyCipher)` with its meaning intact.

---

## §3 — Envelope format

```text
byte  0        key version (1..255)
bytes 1..12    nonce        (12 bytes, RandomNumberGenerator.Fill)
bytes 13..28   tag          (16 bytes)
bytes 29..     ciphertext   (same length as the UTF-8 plaintext)
```

- **AES-256-GCM** via `System.Security.Cryptography.AesGcm`, constructed as
  `new AesGcm(key, tagSizeInBytes: 16)` — the tag-size argument is mandatory since .NET 8
  and omitting it does not compile.
- **Version first, fixed width, unencrypted.** FR-005 requires the version be readable
  without trial decryption; a leading byte satisfies that for an operator with `psql` as
  much as for the code. 255 versions is not a limit anyone reaches.
- **Nonce is random per message, never reused.** GCM's failure mode under nonce reuse is
  catastrophic (key recovery), so it is generated fresh per encryption from
  `RandomNumberGenerator`, never derived from the row.
- **Associated data = the message id's 16 bytes.** `BaseEntity` assigns the UUIDv7 in its
  field initialiser, so the id exists before the row is ever written and never changes
  afterwards. Binding the ciphertext to it means an operator with write access cannot move
  a ciphertext from one message to another — the tag check fails. It costs one argument at
  each call site and is the reason §1's converter route could not have offered it.

---

## §4 — Key configuration: one secret, first entry writes

```text
Chat__Encryption__Keys = "2:<base64 32 bytes>;1:<base64 32 bytes>"
```

One configuration value. Entries are `version:base64key`, semicolon-separated, and **the
first entry is the write key**; the rest exist only so older rows stay readable (FR-006).

**Why one value and not two.** The obvious shape is a key list plus a separate
`ActiveVersion`. That is two secrets that can disagree, and the disagreement is silent until
someone reads a message. Making position carry the meaning removes the failure mode
entirely: there is nothing to keep in sync. Rotation becomes "prepend the new key", which is
a single edit to a single secret in GitHub Environments, and the old key is removed on a
later deploy once nothing needs it.

**Validation at startup, fail fast (FR-007)**, mirroring the Redis guard already in
`backend/Program.cs` (~L426–443) both in placement and in tone:

- at least one entry; every entry parses; every key decodes to **exactly 32 bytes**;
  versions are 1..255 and unique; the first entry is the writer.
- anything else throws at startup with a message naming the configuration key and **never
  echoing key material**.

There is no "encryption disabled" switch. A configuration mistake must stop the process, not
quietly start storing plaintext — that is the state this feature exists to leave.

**Delivery** follows the constitution's existing channel with no new dependency: `.env` →
docker-compose for local, `TF_VAR_chat_encryption_keys` → `kubernetes_secret_v1.app` for Dev
and Prod, alongside `Jwt__SigningKey` and `MediaStorage__ConnectionString`. No Key Vault
(Principle V).

**The key is not in the database and the database does not imply it** (FR-024). Whoever
restores a database restores unreadable messages unless they also hold the key of the
version each row names. That is the point, and it is what the runbook note in
`infra/README.md` must say.

---

## §5 — Decryption failure: per-message, client-translated

`IChatMessageCipher.TryUnprotect(byte[] cipher, Guid messageId, out string text)` returns
`false` rather than throwing. The three chat projection sites then map the row to a DTO with
`Body = ""` and a new **`IsUnavailable = true`** flag.

**The placeholder text lives in the frontend catalogues, not in C#.** Chat already has a
server-side localized placeholder (`MemberPlaceholder`, for a departed sender), so the local
precedent points both ways — but prose assembled in the backend has no catalogue key, and
`catalog-parity.spec.ts` therefore cannot see it (this is exactly the gap GH #141 was filed
for). One new key, `chat.conversation.messageUnavailable`, added to **en/de/es in the same
commit**, is guarded for free.

The bubble reuses the existing deleted-message tombstone styling in
`chat-conversation.component.html` (L112) — same italic, same opacity, different key. No new
component, no new token.

**Inbox preview**: an unreadable last message previews as empty, the same treatment a
deleted message already gets (`ChatConversationService` L499). Recorded as a deliberate
minor simplification rather than threading a second flag through `LastMessageDto`; the row
still shows its sender and timestamp, and opening the conversation shows the placeholder.

**Logging** (FR-010/FR-014): one warning naming the conversation id, message id and key
version. Never the ciphertext, never the key, never a length that would leak the message
size beyond what the row already reveals.

---

## §6 — Postgres TLS: certificate provisioning

### Deployed (Dev/Prod) — cert-manager, which is already there

`infra/modules/platform/main.tf` already installs cert-manager and already creates
`ClusterIssuer` objects through `kubernetes_manifest`. Three more manifests give Postgres a
certificate with no new tooling:

1. a self-signed `Issuer` in the app namespace (bootstrap only),
2. a `Certificate` for an internal CA, issued by it, into `postgres-ca`,
3. a `CA` `Issuer` backed by `postgres-ca`, and a `Certificate` for the server into
   `postgres-tls` with `dnsNames = ["postgres", "postgres.<ns>.svc", "postgres.<ns>.svc.cluster.local"]`.

The connection string uses `Host=postgres`, so **`postgres` must be in the SAN list** —
`VerifyFull` checks the hostname as written, not the resolved name.

Let's Encrypt is deliberately not used for this: the database is not publicly resolvable,
ACME HTTP-01 could not validate it, and an internal CA is the correct trust anchor for an
in-cluster hop.

> ⚠ **Terraform two-phase apply.** `kubernetes_manifest` resolves the CRD schema at *plan*
> time, so on a cluster without cert-manager's CRDs a plan fails. `infra/README.md` already
> documents this for the existing ClusterIssuers; these resources inherit the same
> constraint and the same `depends_on = [helm_release.cert_manager]`.

### The file-permission rule that breaks this if ignored

PostgreSQL refuses to start if `ssl_key_file` is too permissive. The exact rule (`be-secure-common.c`)
is *not* "0600 only":

- owned by the database user → **no** group or world bits at all;
- owned by **root** → group **read** is allowed; group write/exec and any world bit is not.

A Kubernetes secret volume produces root-owned files, so mounting `postgres-tls` with
`default_mode = "0640"` and the pod's `security_context { fs_group = 70 }` (the `postgres`
uid/gid in `postgres:18.3-alpine`) is accepted. The naive `0600` also works; `0644` does
not, and fails as a startup crash-loop with a message about permissions rather than
anything about TLS.

### Local (docker-compose) — generated certs, stock image

A `.ps1` script (Principle VI: no `.sh`) generates a local CA and a server certificate into
a **gitignored** `certs/local/` directory using .NET's `CertificateRequest` +
`ExportPkcs8PrivateKeyPem()`, so no OpenSSL dependency is introduced on Windows. SANs:
`database` (the compose service name), `localhost` and `127.0.0.1` (so `dotnet run` and
`dotnet ef` from the host also verify).

The bind mount cannot carry usable permissions from Windows, so the key is copied inside the
container before Postgres sees it, keeping the stock image and adding no Dockerfile:

```yaml
command: >
  sh -c "install -o postgres -g postgres -m 600 /certs/server.key /tmp/server.key &&
         install -o postgres -g postgres -m 644 /certs/server.crt /tmp/server.crt &&
         exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/server.crt -c ssl_key_file=/tmp/server.key"
```

The re-entry through `docker-entrypoint.sh postgres` is load-bearing: the entrypoint only
runs the initdb/permission dance when its first argument is `postgres`, so a `command` that
launches the server directly would skip it.

**Certificates are never committed** (FR-018). `certs/` joins `.gitignore`, the script is
idempotent, and `.env.sample` plus the README say to run it once.

---

## §7 — The connection string

```text
Host=postgres;Port=5432;Database=…;Username=…;Password=…;SSL Mode=VerifyFull;Root Certificate=/etc/juggerhub/certs/ca.crt
```

Verified against the pinned Npgsql (10.0.3): the keywords are exactly **`SSL Mode`** and
**`Root Certificate`**, and `SslMode` accepts `Disable|Allow|Prefer|Require|VerifyCA|VerifyFull`.
`Trust Server Certificate` MUST NOT appear — it disables the check this feature is for.

The CA certificate reaches the backend as a **file**, not an environment variable, because
Npgsql wants a path:

- deployed: `postgres-ca`'s `ca.crt` mounted read-only into the backend pod at
  `/etc/juggerhub/certs/`;
- local: `./certs/local/ca.crt` bind-mounted to the same path, so the connection string is
  character-identical across environments except for host and credentials (Principle V).

`infra/locals.tf` assembles `connection_string`; the two new parameters are appended there,
so no tfvars change and no new sensitive value in state.

---

## §8 — The other clients of this database

Turning on `ssl = on` **enables** TLS; it does not refuse plaintext clients. Forcing that
would mean replacing `pg_hba.conf` with `hostssl` rules via `-c hba_file=`, which on Dev
would also have to survive an already-initialised PVC (the trap recorded in feature 033: an
`/docker-entrypoint-initdb.d/` script is a silent no-op on a volume that already has a
cluster). The cost is a plausible way to break the database on Dev; the benefit is only
against a *misconfigured client of our own*.

**Decision**: leave `pg_hba.conf` alone and make every client ask for TLS.

| Client | Change |
|---|---|
| Backend | `SSL Mode=VerifyFull` + CA path — verified (FR-016) |
| Umami (compose + `analytics.tf`) | `?sslmode=require` appended to `DATABASE_URL`; Prisma's default is `prefer`, so this makes the encryption explicit rather than incidental. No CA needed — Umami carries analytics, not messages |
| compose `psql` helper containers | none; `psql` defaults to `sslmode=prefer` and upgrades on its own |
| Integration tests (Testcontainers) | **none — deliberately exempt** |

**Residual, recorded rather than hidden**: the server would still accept a plaintext
connection from a client that insisted on one. Every client we ship asks for TLS, and the
backend additionally verifies. Closing the last gap is a `pg_hba` change and belongs with
whatever work rebuilds the Dev volume.

### Why the test harness is exempt

`JuggerHubApiFactory` boots the real app against a throwaway `postgres:18-alpine`
Testcontainer and uses `_database.GetConnectionString()` verbatim. Giving that container a
certificate would mean minting one per test run for a random mapped port on `localhost` —
significant machinery to assert something no test asserts. TLS here is **configuration**,
not code: the app requires whatever the connection string says, so the tests exercise every
line this feature adds *except* the connection parameters. That parity gap is stated in
`quickstart.md` so it is not mistaken for an oversight, and the connection-string half is
verified by hand (quickstart §4) instead.

---

## §9 — Redis (#219) is stated, not built

The realtime backplane carries message DTOs between replicas, so it is a genuine plaintext
hop — but **nothing deploys Redis in any environment** (#219), Dev runs one replica and
falls back in-process, and Prod cannot start at all. There is no hop to harden yet.

FR-019 records the requirement — TLS and AUTH when Redis lands — and this feature adds it as
a comment to #219 rather than deploying Redis inside a chat-encryption feature. The privacy
text must not imply the backplane is protected, because when it exists it will not be
(§11).

---

## §10 — Principle VII is NOT engaged

⚠ Encryption and decryption are local CPU work. TLS parameters are added to a connection
this application already makes. **No outbound HTTP call is added, no new integration
exists**, and wrapping any of this in `AddJuggerHubResilience`, a retry policy or a circuit
breaker is review-rejectable (this is the same call 042, 043, 044 and 045 each recorded).

The database half of Principle VII is already satisfied and unchanged:
`options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())` in
`Program.cs` L62. A TLS handshake failure is not transient and must not be retried into an
outage — `VerifyFull` failing means the certificate is wrong, and Npgsql surfaces it as a
non-transient error, which is the correct behaviour.

Quality **Gate 8 is therefore recorded as not engaged**, deliberately.

---

## §11 — The privacy text: what may and may not be claimed

The single sentence at `frontend/apps/web/public/i18n/legal/{de,en,es}.json` L129 is the
only place in the product that describes how message text is protected — verified by
grepping all three catalogues for every form of "encrypt" (FR-023). Nothing else needs
touching.

| May be claimed after this ships | May **not** be claimed |
|---|---|
| Message text is encrypted where it is stored | End-to-end encryption, or anything a reader would hear as it |
| Database access alone does not reveal it | That *we* cannot read messages — we hold the key |
| The connection to the database is encrypted | That every hop everywhere is protected — the backplane (§9) is not |

The owner's standing rule for legal copy is **never state what the product does not do**,
because negative claims go false silently. This paragraph keeps one negative — "this is not
end-to-end encryption" — for the same reason 036 kept its two documented exceptions: without
it, "your messages are encrypted" is the sentence a reader completes incorrectly, and
overstating protection is the more damaging error. German is authoritative; en/es follow it.

---

## §12 — What deliberately does not change

Listed because the temptation to touch each one exists:

- **No new endpoint, no route change, no contract change** beyond one added boolean on
  `MessageDto`.
- **Ordering, the read cursor and keyset paging** are the UUIDv7 `Id` and are untouched.
  Nothing about encryption reaches them.
- **The delete path** still empties the row; it now writes an empty array instead of an
  empty string, and still clears `LinkKind`/`LinkTargetId`.
- **Link unfurl** still parses the plaintext the player typed, at send time, before
  encryption (FR-012). `ChatLinkParser.Parse(trimmed, …)` runs on `trimmed`; only the
  resulting kind and target id are stored, and those stay plaintext columns — they are ids,
  not content, and the card is still resolved per viewer at read time.
- **Membership, blocking, hiding, archival, the join cutoff, the former-player
  placeholder** — all inherited unchanged from 019/022/027/046.
- **Other text columns** (news posts, descriptions, listings, `EventContact`) keep their
  current storage. #223 sequences messages first.
- **Umami session replay** still captures chat history rendered on screen (038 FR-006a).
  Encryption at rest does not touch it, and the privacy text must not read as if it does.
