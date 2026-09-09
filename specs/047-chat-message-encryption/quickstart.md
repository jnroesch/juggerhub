# Quickstart: verifying feature 047

**Feature**: 047 | Each section proves a success criterion from [spec.md](./spec.md).

## 0 — One-time local setup

```powershell
# Generate the local CA + Postgres server certificate (idempotent; writes to certs/local/, gitignored)
./scripts/dev-postgres-certs.ps1

# Generate an encryption key and put it in .env
"CHAT_ENCRYPTION_KEYS=1:$([Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 } | ForEach-Object { [byte]$_ })))"
```

Add that line to `.env` (see `.env.sample`), then:

```powershell
docker compose up -d --build
```

The database must reach `healthy`. If it crash-loops, read the Postgres log first: a
message about key-file permissions means the certificate copy step failed, **not** that TLS
is misconfigured (research §6).

---

## 1 — SC-001: the stored row does not contain what was typed

1. Sign in as two seeded players and send `meet at the north pitch at 18:00` in a DM.
2. Read the row directly, bypassing the application:

```powershell
docker compose exec database psql -U postgres -d appdb -c "SELECT ""Id"", octet_length(""BodyCipher"") AS bytes, encode(""BodyCipher"",'escape') FROM ""ChatMessages"" ORDER BY ""Id"" DESC LIMIT 3;"
```

**Expect**: no fragment of the sentence appears; `bytes` is 29 + the UTF-8 length; the first
byte decodes to the key version.

**Also expect** — the reason `bytea` was chosen — that this fails outright:

```sql
SELECT * FROM "ChatMessages" WHERE "BodyCipher" ILIKE '%pitch%';   -- ERROR: operator does not exist
```

---

## 2 — SC-002: nothing a player sees has changed

With the same two sessions open side by side:

| Check | Expect |
|---|---|
| Open the conversation | Every message reads exactly as typed, in order |
| Send a message with the other window open | It appears live (SignalR), correct text |
| The chat inbox | Last-line preview shows the real text |
| Delete your own message | Tombstone in both windows; `SELECT octet_length("BodyCipher")` on that row returns **0** |
| Paste a link to a team or event and send | The link card resolves and renders as before |
| A message of exactly 2 000 characters | Accepted; 2 001 is rejected with the same message as today |
| Emoji, combining marks, newlines | Round-trip unchanged |

---

## 3 — SC-005 / SC-006 / SC-007: the failure paths

**No key (SC-005)** — comment `CHAT_ENCRYPTION_KEYS` out of `.env` and restart the backend.
Expect: the container exits at startup naming `Chat:Encryption:Keys`. Expect **no** base64
fragment anywhere in the log, and **no** message stored unencrypted.

**Undecryptable row (SC-006)** — corrupt one row's ciphertext, then reopen the conversation:

```powershell
docker compose exec database psql -U postgres -d appdb -c "UPDATE ""ChatMessages"" SET ""BodyCipher"" = overlay(""BodyCipher"" placing '\x00'::bytea from 30) WHERE ""Id"" = '<id>';"
```

Expect: the conversation **still opens**; that one bubble reads *"This message can't be
displayed."* in the UI language; every other message is normal; one backend warning naming
the ids and the key version, containing **no ciphertext**.

**Two key versions (SC-007)** — prepend a second key:
`CHAT_ENCRYPTION_KEYS=2:<new>;1:<old>`. Restart, send a new message. Expect: old messages
still read; the new row's first byte is `2`. Now remove the `1:` entry and restart: the old
messages become unavailable placeholders and the new one still reads — which is the rotation
story, demonstrated rather than promised.

---

## 4 — SC-003 / SC-004: the connection

**Encrypted and verified**, from inside the backend container's connection:

```powershell
docker compose exec database psql -U postgres -d appdb -c "SELECT usename, ssl, version, cipher FROM pg_stat_ssl JOIN pg_stat_activity USING (pid) WHERE datname = 'appdb';"
```

Expect: the backend's rows show `ssl = t` with a TLS 1.3 cipher.

**Verification actually happens (SC-004)** — three checks, because "it connected" proves
nothing on its own:

1. Point `Root Certificate` at a *different* CA file → the backend must fail to start with a
   certificate error.
2. Change `Host=database` to `127.0.0.1` while the certificate has no such SAN → must fail
   hostname verification. (This is the check that catches a SAN list missing the short name.)
3. Confirm `Trust Server Certificate` appears **nowhere**:

```powershell
Select-String -Path docker-compose.yml,infra/locals.tf,.env.sample -Pattern "Trust Server Certificate"   # expect: no matches
```

> **Recorded parity gap**: the integration-test Postgres (Testcontainers) is *not* given a
> certificate, so this section has no automated equivalent — TLS here is connection-string
> configuration and every line of new *code* is covered by the suite. Run this section by
> hand when the connection string or the certificates change (research §8).

### Run this section. It has already caught one real defect.

The first version of the compose `command:` used a YAML folded scalar with the `-c` flags
indented to line up under `postgres`. A folded scalar joins lines with spaces **only while they
share the block's indentation** — a more-indented line keeps its newline verbatim. So the shell
saw `exec docker-entrypoint.sh postgres`, terminated the command there, and dropped every flag.

The failure mode is the reason this check exists: **Postgres started perfectly, reported healthy,
and served the application — with `ssl` still off.** No error, no warning, nothing in any log. The
certificates were mounted, the key was copied with the right ownership, the container was green.
Only `SHOW ssl;` said otherwise.

Verified results, 2026-09-09, against local compose:

| Check | Result |
|---|---|
| `SHOW ssl;` | `on` |
| Backend's pooled connection in `pg_stat_ssl` | `t`, TLSv1.3, `TLS_AES_256_GCM_SHA384` |
| `sslmode=verify-full` with the real CA | connects |
| `sslmode=verify-full` with a non-CA as root | `SSL error: certificate verify failed` |
| `host=juggerhub-database` (not in the SAN), `verify-full` | `server certificate for "database" (and 2 other names) does not match host name` |
| the same host with `verify-ca` | connects — proving it is specifically the **hostname** check that fires, which is the whole difference between `VerifyFull` and the weaker modes |
| `Trust Server Certificate` anywhere in config | absent (only the comments warning against it) |
| `ChatMessages` schema after migration | `Body` gone; `BodyCipher` `bytea` |

---

## 5 — SC-008: the privacy paragraph

Open `/privacy` in all three languages and check the messages paragraph against research
§11's table. Specifically:

- it says message text is encrypted where it is stored, and that the connection to the
  database is encrypted;
- it says plainly that JuggerHub holds the key and can therefore read messages;
- it does **not** say, or let a reader conclude, end-to-end encryption;
- it does not claim protection for hops that do not have it (research §9);
- German reads as the authoritative original, en/es as faithful translations.

---

## 6 — Automated suites

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests   # incl. the new cipher + chat encryption tests
npm --prefix frontend test                                  # incl. catalog-parity + legal-catalog
npx nx lint web --prefix frontend ; npx nx build web --prefix frontend
terraform -chdir=infra fmt -check ; terraform -chdir=infra init -backend=false ; terraform -chdir=infra validate
```

**SC-009 has an assertion, not just an eyeball**: the integration harness already captures
error logs (`JuggerHubApiFactory.ErrorLogs`). A test sends a message, corrupts it, reads the
conversation, and asserts no captured log line contains the plaintext, any base64 fragment of
the configured key, or the ciphertext.
