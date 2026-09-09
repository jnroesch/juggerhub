---

description: "Task list for feature 047 — chat message encryption at rest + database transport hardening"
---

# Tasks: Chat Message Encryption at Rest + Database Transport Hardening

**Input**: Design documents from `/specs/047-chat-message-encryption/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included. This feature's whole value is a security property, and a security
property that is not asserted is a claim. The contract file already enumerates the cases
(C1–C11, S1–S6); those become real tests.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 (encryption at rest) · US2 (transport) · US3 (privacy policy) · US4 (key rotation)

## Path Conventions

Web application: `backend/`, `frontend/apps/web/`, `infra/`, repo-root `scripts/` and compose file.

---

## Phase 1: Setup

**Purpose**: the two things every later phase needs — somewhere for keys to come from, and
somewhere for certificates to come from. Nothing here changes behaviour.

- [X] T001 Add `certs/` to `.gitignore` so no certificate or private key can be committed (FR-018)
- [X] T002 [P] Create `scripts/dev-postgres-certs.ps1` — generates a local CA and a Postgres server certificate into `certs/local/` (`ca.crt`, `server.crt`, `server.key`) using .NET `CertificateRequest` + `ExportPkcs8PrivateKeyPem()`, no OpenSSL dependency; SANs `database`, `localhost`, `127.0.0.1`; idempotent (skips when a valid, unexpired cert already exists); prints where the files went. Research §6
- [X] T003 [P] Add `CHAT_ENCRYPTION_KEYS=` to `.env.sample` with a comment stating the format (`version:base64key`, `;`-separated, **first entry writes**), that the key is 32 bytes base64, and that losing it destroys every stored message

**Checkpoint**: `./scripts/dev-postgres-certs.ps1` produces three files under `certs/local/` and `git status` shows none of them.

---

## Phase 2: Foundational — the cipher (BLOCKS US1, US4)

**Purpose**: a complete, tested, unused component. Nothing references it yet, which is what
makes its tests mean something.

- [X] T004 Create `backend/Services/Chat/Encryption/IChatMessageCipher.cs` — `byte[] Protect(string plaintext, Guid messageId)` and `bool TryUnprotect(byte[] cipher, Guid messageId, out string plaintext)`, with XML docs stating that `TryUnprotect` never throws on bad input and that empty text is the caller's business (contract C4/C5)
- [X] T005 Create `backend/Common/ChatEncryptionOptions.cs` (options live in `Common/` by project convention — a deviation from the plan file tree) — binds the single `Chat:Encryption:Keys` string; a `Parse()` that yields ordered `(byte Version, byte[] Key)` entries and throws with a message naming the configuration key and **containing no key material** (contract S1–S5)
- [X] T006 Create `backend/Services/Chat/Encryption/AesGcmChatMessageCipher.cs` — envelope `[version:1][nonce:12][tag:16][ciphertext:n]`, `new AesGcm(key, tagSizeInBytes: 16)`, nonce from `RandomNumberGenerator`, **associated data = the message id's 16 bytes**; `Protect` uses the first configured entry; `TryUnprotect` reads any configured version and returns `false` for a short envelope, an unknown version, or a failed tag. Research §3, data-model "Envelope layout"
- [X] T007 Create `backend/Services/Chat/Encryption/ChatEncryptionServiceCollectionExtensions.cs` — `AddChatMessageEncryption(IConfiguration)`: parse, validate, register `IChatMessageCipher` as a **singleton** holding the parsed keys
- [X] T008 Wire it in `backend/Program.cs` beside the Redis fail-fast (~L426–443), matching its placement and tone — startup **throws** when no usable key is configured (FR-007). There is no switch that disables encryption
- [X] T009 [P] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatMessageCipherTests.cs` covering contract cases **C1–C11**: envelope size and layout, a fresh nonce per call, round-trip for ASCII/emoji/combining marks/newlines/2 000 chars, `Protect("")` and `TryUnprotect([])` throwing, rejection on a different message id, rejection of a single flipped byte, a short envelope returning false without throwing, an unknown version returning false, and two configured versions where the first writes
- [X] T010 [P] Add `ChatEncryptionOptionsTests` covering **S1–S6**, asserting for each failure that the exception names `Chat:Encryption:Keys` and that **no base64 fragment of the input appears in the message**
- [X] T011 Add `["Chat:Encryption:Keys"] = "1:<fixed 32-byte test key>"` to `JuggerHubApiFactory`'s in-memory configuration so the existing suite can boot

**Checkpoint**: `dotnet test` green. The cipher is complete and nothing in the product calls it yet.

---

## Phase 3: US1 — A database copy reveals no message text (P1) 🎯 MVP

**Goal**: the text a player typed is absent from the row, and everything that renders
messages works exactly as before.

**Independent test**: send a message, read the row with `psql`, see no fragment of it; open
the conversation and read it normally. Quickstart §1–§2.

- [X] T012 [US1] `backend/Entities/ChatMessage.cs` — replace `public string Body` with `public byte[] BodyCipher { get; set; } = [];` and **rewrite the XML remark**: the old one says the body is "stored verbatim", which becomes false. The replacement states it is an authenticated-encryption envelope, that the decrypted value is still never markup (019 FR-014 unchanged), and that only `IChatMessageCipher` produces it. Data-model D1/D5
- [X] T013 [US1] `backend/Data/AppDbContext.cs` L1015 — `entity.Property(m => m.BodyCipher).IsRequired();`, no `HasMaxLength` (data-model D3)
- [X] T014 [US1] Generate migration `EncryptChatMessageBodies` (`dotnet ef migrations add`) — drops `Body`, adds `BodyCipher` (`bytea`, NOT NULL, default empty). Add a comment on `Down` saying it restores the column but **cannot restore content**; no backfill and no plaintext compatibility (FR-025)
- [X] T015 [US1] `backend/Services/Chat/ChatMessageService.cs` **send path** (~L100–110) — construct the `ChatMessage` first (its UUIDv7 `Id` is assigned by `BaseEntity`'s field initialiser), then `BodyCipher = _cipher.Protect(trimmed, message.Id)`. `ChatLinkParser.Parse(trimmed, …)` still runs on the **plaintext**, before encryption (FR-012). The 2 000-character check stays on `trimmed` (FR-011)
- [X] T016 [US1] `backend/Services/Chat/ChatMessageService.cs` **delete path** (~L468) — `message.BodyCipher = [];` (never `Protect("")`). `LinkKind`/`LinkTargetId` clearing is unchanged. Data-model D2
- [X] T017 [US1] `backend/Services/Chat/ChatMessageService.cs` **system lines** (~L497) — `BodyCipher = []`
- [X] T018 [US1] `backend/Services/Chat/ChatMessageService.cs` **projections** (~L286, ~L340) — the private `Row` record's `string Body` becomes `byte[] BodyCipher`; both `.Select(...)` sites project `m.BodyCipher`
- [X] T019 [US1] `backend/Services/Chat/ChatMessageService.cs` **`ToDto`** (~L413) — decrypt here, where the culture and the row id are both in hand: deleted or system ⇒ `""`; otherwise `TryUnprotect(r.BodyCipher, r.Id, out var text)`. Success ⇒ the text. Failure ⇒ handled in US1-adjacent phase 5 (leave a single `TODO(T028)` returning `""` so this phase compiles and the suite is green)
- [X] T020 [US1] `backend/Services/Chat/ChatConversationService.cs` inbox preview (~L456 project, ~L499 render) — project `m.BodyCipher`; a deleted last message still previews `""`; otherwise decrypt for the preview
- [X] T021 [US1] `backend/Data/DevDataSeeder.cs` (~L255–260) — seed through the cipher rather than assigning a string
- [X] T022 [US1] `backend/tests/.../Chat/ChatDeleteTests.cs` L72 — `Assert.Equal(string.Empty, row.Body)` becomes `Assert.Empty(row.BodyCipher)`; the assertion's meaning is unchanged and the comment should say so
- [X] T023 [P] [US1] Add `ChatMessageEncryptionTests` — send a message through the API, then read the raw row via the DbContext and assert the plaintext is **absent** from `BodyCipher` and that the first byte is the configured version (SC-001)
- [X] T024 [P] [US1] Extend the same suite: a **round-trip through the API** for emoji, newlines and a 2 000-character message; the inbox preview shows the real text; a deleted message leaves `BodyCipher` empty and previews empty; a link message still resolves its card (SC-002)
- [X] T025 [US1] Run the full backend suite and fix fallout. Every remaining reference to `ChatMessage.Body` must be gone — `grep -rn "\.Body" backend --include=*.cs | grep -i chat` should return nothing but the unrelated `SmtpEmailSender` hit

**Checkpoint**: backend suite green; quickstart §1 and §2 pass by hand; SC-001 and SC-002 met.

---

## Phase 4: US4 — The key can be replaced without a rebuild (P3)

**Goal**: prove FR-005/FR-006 rather than assert them. Small, and it belongs next to US1
because it is the same code path.

**Independent test**: two configured versions; rows written under either read correctly; new
rows carry the first version. Quickstart §3.

- [X] T026 [P] [US4] Add a test that configures `"2:<k2>;1:<k1>"`, writes a row under version 1 directly, sends a new message through the API, and asserts: both read correctly, and the new row's first byte is `2` (SC-007)
- [X] T027 [P] [US4] Add a test that a row whose version byte is **not** configured reads as unavailable rather than throwing — the rotation-gone-wrong case, which is also the FR-009 path

---

## Phase 5: US1/US4 failure path — an unreadable message does not take the thread down

**Goal**: FR-009/FR-010. Removes the `TODO(T028)` left in T019.

**Independent test**: corrupt one row's ciphertext; the conversation still opens with one
placeholder bubble. Quickstart §3.

- [X] T028 [US1] `backend/Dtos/Chat/ChatDtos.cs` — `MessageDto` gains `bool IsUnavailable` after `IsDeleted`. `LastMessageDto` is **not** changed (contracts/chat-api-delta.md)
- [X] T029 [US1] `ChatMessageService.ToDto` — replace the TODO: on `TryUnprotect` returning false, emit `Body = ""`, `IsUnavailable = true`, `LinkCard = null`, and log **one warning** naming conversation id, message id and key version — never the ciphertext, never the key, never the plaintext (FR-010/FR-014)
- [X] T030 [US1] `frontend/apps/web/src/app/core/models/chat.models.ts` — `readonly isUnavailable: boolean;` beside `isDeleted`
- [X] T031 [US1] `frontend/apps/web/src/app/features/chat/chat-conversation/chat-conversation.component.html` (~L112) — a sibling branch to the deleted tombstone rendering `chat.conversation.messageUnavailable`, reusing the existing italic/opacity treatment. No new component, no new token
- [X] T032 [US1] Add `chat.conversation.messageUnavailable` to **`en.json`, `de.json` and `es.json` in the same commit** — `catalog-parity.spec.ts` goes red otherwise. English: "This message can't be displayed."
- [X] T033 [P] [US1] Backend test: corrupt a stored row, request the conversation, assert **200** with every other message intact and exactly one carrying `isUnavailable: true` and an empty body (SC-006)
- [X] T034 [P] [US1] Backend test for SC-009: after that request, assert no captured log line in `JuggerHubApiFactory.ErrorLogs` contains the plaintext, the ciphertext, or any base64 fragment of the configured key
- [X] T035 [P] [US1] Frontend spec: a message with `isUnavailable` renders the placeholder and not an empty bubble; a deleted one still renders the deleted tombstone

**Checkpoint**: both suites green; quickstart §3 passes; SC-005, SC-006, SC-007 and SC-009 met.

---

## Phase 6: US2 — Database traffic is encrypted and the server is verified (P2)

**Goal**: FR-015–FR-018. Configuration and infrastructure only; no application code changes.

**Independent test**: `pg_stat_ssl` shows the backend connected over TLS; pointing at the
wrong CA or a name not in the SAN fails. Quickstart §4.

- [X] T036 [US2] `docker-compose.yml` — the `database` service gains the certificate mount and the `command` wrapper that copies the key inside the container (`install -o postgres -g postgres -m 600 …`, then `exec docker-entrypoint.sh postgres -c ssl=on …`). The re-entry through `docker-entrypoint.sh postgres` is load-bearing; research §6 explains why
- [X] T037 [US2] `docker-compose.yml` — the `backend` service mounts `./certs/local/ca.crt` read-only at `/etc/juggerhub/certs/ca.crt`, and its `ConnectionStrings__DefaultConnection` gains `;SSL Mode=VerifyFull;Root Certificate=/etc/juggerhub/certs/ca.crt`. **`Trust Server Certificate` must appear nowhere**
- [X] T038 [US2] `docker-compose.yml` — append `?sslmode=require` to Umami's `DATABASE_URL` (research §8). Leave the `psql` helper containers alone; they negotiate on their own
- [X] T039 [US2] `infra/modules/app/main.tf` — three cert-manager manifests: a self-signed `Issuer`, a CA `Certificate` into `postgres-ca`, and a CA `Issuer` issuing a server `Certificate` into `postgres-tls` with `dnsNames = ["postgres", "postgres.<ns>.svc", "postgres.<ns>.svc.cluster.local"]`. **`postgres` (the short name in the connection string) must be present** or `VerifyFull` fails. `depends_on = [helm_release.cert_manager]`, same two-phase-apply constraint as the existing ClusterIssuers
- [X] T040 [US2] `infra/modules/app/main.tf` — the Postgres StatefulSet mounts `postgres-tls` at `default_mode = "0640"` with `security_context { fs_group = 70 }` (verified: `postgres:18.3-alpine` runs uid/gid 70) and starts with `-c ssl=on -c ssl_cert_file=… -c ssl_key_file=…`. **`0644` crash-loops the database** with a permissions error that mentions nothing about TLS — research §6
- [X] T041 [US2] `infra/modules/app/main.tf` — the backend Deployment mounts `postgres-ca`'s `ca.crt` read-only at `/etc/juggerhub/certs/`, the same path compose uses, so the connection string is character-identical across environments (Principle V)
- [X] T042 [US2] `infra/locals.tf` — append `;SSL Mode=VerifyFull;Root Certificate=/etc/juggerhub/certs/ca.crt` to `connection_string`. No tfvars change, no new sensitive value in state
- [X] T043 [US2] `infra/modules/app/analytics.tf` — append `?sslmode=require` to Umami's `DATABASE_URL`
- [X] T044 [US2] Run `terraform -chdir=infra fmt -check`, `init -backend=false`, `validate` — the 015 CI gates
- [X] T045 [US2] Work through quickstart §4 by hand against local compose, including **all three** negative checks (wrong CA, hostname not in SAN, and grepping that `Trust Server Certificate` appears nowhere). This is the section with no automated equivalent — the Testcontainers database is deliberately certificate-free (research §8)

**Checkpoint**: `docker compose up` connects with `VerifyFull`; `pg_stat_ssl` shows TLS 1.3; Terraform validates; SC-003 and SC-004 met.

---

## Phase 7: Secrets & runbook

**Purpose**: the key has to reach Dev and Prod, and someone restoring a database has to know
it exists.

- [X] T046 [P] `infra/variables.tf` and `infra/modules/app/variables.tf` — `chat_encryption_keys`, `sensitive = true`, no default
- [X] T047 `infra/modules/app/main.tf` — `"Chat__Encryption__Keys" = var.chat_encryption_keys` in `kubernetes_secret_v1.app`, beside `Jwt__SigningKey`. **Not** the ConfigMap
- [X] T048 `infra/main.tf` — pass the variable through to the app module
- [X] T049 `.github/workflows/deploy.yml` — `TF_VAR_chat_encryption_keys: ${{ secrets.CHAT_ENCRYPTION_KEYS }}` in the dev job **and** in the commented-out prod block, so re-enabling prod does not start life missing a secret
- [X] T050 `infra/README.md` — a short "chat message encryption key" section: the format, that it is generated once per environment, that it lives only in GitHub Environments, and — the part that matters (FR-024) — **a restored database without its key contains no readable messages**. This goes where a restore is performed, not only in a spec

**Checkpoint**: `terraform validate` green; deliberately removing the key from `.env` fails startup (SC-005).

---

## Phase 8: US3 — The privacy policy states what is actually true (P2)

**Goal**: FR-020–FR-023. **German is written first and is authoritative**; en/es are
translations of it.

**Independent test**: read the messages paragraph in all three languages against
research §11's table. Quickstart §5.

- [X] T051 [US3] `frontend/apps/web/public/i18n/legal/de.json` L129 — rewrite the messages paragraph. It **may** say the text is encrypted where it is stored and that the database connection is encrypted; it **must** say plainly that JuggerHub holds the key and can therefore read messages; it **must not** claim or let a reader conclude end-to-end encryption, and must not claim protection for hops that lack it (the backplane, research §9). Keep the existing sentence about a conversation outliving its team or event
- [X] T052 [US3] `frontend/apps/web/public/i18n/legal/en.json` L129 — translate the German
- [X] T053 [US3] `frontend/apps/web/public/i18n/legal/es.json` L129 — translate the German
- [X] T054 [P] [US3] Re-run the FR-023 sweep: `grep -rn "encrypt\|verschlüssel\|cifrad" -i frontend/apps/web/public/i18n/` — confirm L129 in the three legal catalogues is still the **only** place the product describes how message text is protected, and that nothing else went stale
- [X] T055 [US3] Run `legal-catalog.spec.ts` — its DM-1 key-parity guard turns red if the three files drift

**Checkpoint**: all three read as the same document; SC-008 met.

---

## Phase 9: Polish & gates

- [X] T056 Instantiate `specs/047-chat-message-encryption/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify each item against the diff — the placeholder bubble and the legal prose are the surfaces. DESIGN.md wins on any conflict (Gate 7)
- [X] T057 [P] Full verification: `dotnet test`, `npm --prefix frontend test`, `npx nx lint web`, `npx nx build web`, `terraform fmt -check`/`validate`. Report what ran and what failed — never claim a check that was not run
- [X] T058 [P] Comment on GH **#219** recording FR-019: whoever deploys Redis must give it TLS and AUTH, because the backplane carries message DTOs that this feature does **not** protect
- [X] T059 Update GH **#223** with what shipped, what was declined (option 3), and the recorded residuals: `pg_hba` untouched so the server still accepts a plaintext client; the test harness is certificate-free; the inbox preview does not distinguish unavailable from deleted; Umami is encrypted but unverified
- [X] T060 Re-read [plan.md](./plan.md)'s "Spec drift and residuals" against what was actually built and correct it if implementation diverged. Drift is reported, not silently absorbed

---

## Dependencies

```text
Phase 1 (setup)
   └─► Phase 2 (cipher)           ← blocks everything that encrypts
          ├─► Phase 3 (US1)       ← MVP
          │      ├─► Phase 4 (US4)
          │      └─► Phase 5 (failure path — removes T019's TODO)
          └─► Phase 7 (secrets)   ← needs the option shape from T005

Phase 6 (US2, transport)  — independent of Phases 2–5; needs only T001/T002
Phase 8 (US3, policy)     — text depends on Phases 3 and 6 being TRUE, so it lands last
Phase 9 (gates)           — after everything
```

- **US1 is the MVP.** Phases 1–3 alone deliver "a database copy reveals no message text",
  which is the sentence #223 is about.
- **US2 is genuinely independent** and could ship first or separately; it is second only
  because on its own it changes nothing about database access.
- **US3 must be last.** It is a published claim, and it may not precede the behaviour it
  describes.

## Parallel opportunities

- T002 ‖ T003 (setup)
- T009 ‖ T010 (cipher tests, different files)
- T023 ‖ T024 (US1 tests) and T026 ‖ T027 (US4 tests)
- T033 ‖ T034 ‖ T035 (failure-path tests, two backend + one frontend)
- Phase 6 in parallel with Phases 3–5 — different files entirely, and the only shared
  artefact is `certs/local/` from T002
- T046 ‖ T054 ‖ T058

## Implementation strategy

Six commits, one per phase group, each landing on a green suite: **cipher → column →
failure path → transport → secrets → disclosure**, then the gates. Writing the cipher's
tests against a component nothing calls yet is deliberate — it is what stops "the tests
pass" from meaning "the services agree with themselves".

**Total: 60 tasks** — US1 14, US2 10, US3 5, US4 2, setup/foundational 11, secrets 5,
polish 5, plus the 8 shared/verification tasks counted within their phases.
