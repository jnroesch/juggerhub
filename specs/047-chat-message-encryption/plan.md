# Implementation Plan: Chat Message Encryption at Rest + Database Transport Hardening

**Branch**: `047-chat-message-encryption` | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/047-chat-message-encryption/spec.md` | **Issue**: [#223](https://github.com/jnroesch/juggerhub/issues/223)

## Summary

Chat message text stops being readable from the database, and the database connection stops
being readable on the wire. GH #223 offered three options; the owner picked **1 + 2** and
declined **3 (end-to-end encryption)** outright.

**The whole feature is: one column, one service, one migration, one connection string, three
certificates, one boolean on a DTO, one i18n key × 3, and one corrected privacy paragraph × 3.**
No new endpoint, no new entity, no new table, no new dependency, no product behaviour change
a player can see — except that an undecryptable message now says so instead of taking the
thread down with it.

**⚠ The issue's own suggested mechanism did not survive contact with the code.** #223 proposes
*"an EF value converter on `Body` keeps every projection unchanged"*. It cannot: a converter
has no access to the request culture (so it cannot produce FR-009's localized placeholder),
throws inside `ToListAsync()` materialisation (so one bad row fails the whole conversation —
the behaviour the owner rejected), and never sees the row id (so the ciphertext cannot be
bound to it). Instead **`ChatMessage.Body` (`varchar(2000)`) is replaced by
`ChatMessage.BodyCipher` (`bytea`)** and decryption happens in the chat services' existing
DTO-mapping step, where the culture and the id are both already in hand. The rename is the
safeguard: five call sites are forced to acknowledge it, and a future projection that leaks
ciphertext into a DTO is visibly wrong rather than invisibly right. Research §1.

**Why now**: feature 046 deleted `SearchMessagesAsync` — the `ILIKE` over `ChatMessages.Body`
and the last server-side code that needed bodies queryable in SQL. `bytea` now makes that
irreversible by construction: `ILike` over a byte array does not compile (FR-013).

**Two load-bearing facts that are easy to get wrong**:

1. **Empty means empty.** System lines and deleted messages store a **zero-length array**,
   never the encryption of `""` — 29 bytes of envelope would be indistinguishable from a
   short message, and "the content is genuinely gone from the row" (019 data-model R12) would
   stop being observable. `ChatDeleteTests` L72 carries straight over as `Assert.Empty`.
2. **PostgreSQL's key-file permission rule is not "0600".** Owned by the DB user ⇒ no group
   or world bits at all; owned by **root** ⇒ group *read* is allowed. That is why a
   Kubernetes secret at `default_mode = "0640"` with `fs_group = 70` works, `0644` crash-loops
   the database with a permissions error that says nothing about TLS, and a Windows bind
   mount cannot be used directly at all (local copies the key inside the container first).
   Research §6.

**Owner decisions (spec Clarifications, 2026-09-09)**: Postgres hop only — Redis TLS/AUTH is
*stated* for whoever lands #219, not built here; **`SSL Mode=VerifyFull`**, not `Require`,
since a certificate is needed either way and verification then costs only distributing the CA;
an undecryptable message shows a **per-message placeholder** rather than failing the request;
and **several key versions configurable at once, first entry writes**, so rotating later is a
config change rather than a code change.

**⚠ Principle VII is NOT engaged.** No outbound call is added — encryption is local CPU, and
TLS parameters go onto a connection the app already makes. Reaching for
`AddJuggerHubResilience`, a retry policy or a breaker here is review-rejectable (research
§10). The existing `EnableRetryOnFailure` is unchanged; a failed certificate check is not a
transient fault and must not be retried.

**Gate 7 IS engaged** — a new message state renders in the conversation and the privacy
paragraph changes in three languages → `checklists/ui-review.md`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 + Nx (frontend), HCL / Terraform (infra)

**Primary Dependencies**: EF Core 10.0.11, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3,
`System.Security.Cryptography.AesGcm` (BCL — **no new package**), cert-manager (already
installed by `infra/modules/platform`), Transloco

**Storage**: PostgreSQL 18. One column replaced on `ChatMessages`; one migration; no new table

**Testing**: xUnit + Testcontainers (`postgres:18-alpine`) for the backend; Jest for the
frontend. Deliberate parity gap: the Testcontainers database is **not** given a certificate —
TLS here is connection-string configuration, and every line of new *code* is still exercised
(research §8)

**Target Platform**: Linux containers — docker-compose locally, AKS for Dev and Prod

**Project Type**: Web application — `backend/` + `frontend/` + `infra/`

**Performance Goals**: AES-GCM on a ≤ 8 KB payload is microseconds; a 20-message page adds 20
such operations. No measurable change to any endpoint. TLS adds one handshake per pooled
connection, not per query

**Constraints**: no new dependency; no Key Vault (Principle V); `.ps1` only (Principle VI);
certificates and keys never committed; no message text in any log

**Scale/Scope**: ~5 backend files + 1 migration + 1 new service folder, 4 frontend files,
3 i18n catalogues + 3 legal catalogues, `docker-compose.yml`, `infra/locals.tf`,
`infra/modules/app/*`, `infra/variables.tf`, `.github/workflows/deploy.yml`, one new `.ps1`

## Constitution Check

*Constitution v1.4.0. Checked before Phase 0 and re-checked after Phase 1 — both passes below.*

| Gate | Verdict |
|---|---|
| **I — Security-first, never trust the client** | **Core to the feature.** Encryption is entirely server-side; no key, no ciphertext and no envelope detail ever crosses the client boundary. Startup validation refuses to run unencrypted rather than degrading (FR-007). Exception messages name the *configuration key* and never key material. The decrypted value is still bound as text by the client, so 019 FR-014's stored-XSS closure is untouched |
| **II — Thin controllers, service-centric** | No controller changes at all. The cipher is a DI'd singleton behind `IChatMessageCipher`; decryption happens in the existing chat services. DTOs stay explicit `.Select` projections — one selects `BodyCipher` instead of `Body` |
| **III — Disciplined data access** | `BaseEntity`/UUIDv7 unchanged, and the UUIDv7 id is what the ciphertext is *bound to* (research §3). Projections and `AsNoTracking` unchanged. Keyset pagination on `Id` unchanged. No new list endpoint, so no new pagination surface |
| **IV — Auth & sessions** | Untouched |
| **V — Environment parity & reproducible deployments** | TLS is configured **identically** in local, Dev and Prod — same connection-string parameters, same CA-file path `/etc/juggerhub/certs/ca.crt`, differing only in certificate material and credentials. The encryption key flows `.env` → compose locally and GitHub Environments → `kubernetes_secret_v1.app` deployed, exactly like `Jwt__SigningKey`. **No Key Vault** |
| **VI — Conventions & tooling** | The certificate generator is `.ps1`. Frontend keeps `.html`/`.css`/`.ts` separate — the change is two lines of template plus a model field |
| **VII — Resilient by default** | **NOT ENGAGED**, deliberately, and recorded as such (research §10). No network call is added. `EnableRetryOnFailure` stays as-is |
| **Gate 7 — UI/design** | **ENGAGED**: a new message state renders in the conversation, and prose changes in six catalogues → `checklists/ui-review.md` |
| **Gate 8 — Resilience** | **NOT ENGAGED** — see VII |

**Violations**: none. Complexity Tracking is therefore omitted.

**One recorded deviation, not a violation**: the integration-test Postgres container is not
given a certificate (research §8). Principle V governs local/Dev/Prod; a throwaway test
container on a random localhost port is none of those, minting a certificate per run would be
significant machinery to assert something no test asserts, and the connection-string half is
verified by hand in `quickstart.md` §4 instead.

## Project Structure

### Documentation (this feature)

```text
specs/047-chat-message-encryption/
├── plan.md                          # This file
├── spec.md
├── research.md                      # Phase 0 — §1 is why the issue's converter idea fails
├── data-model.md                    # Phase 1 — the one column, the envelope, the migration
├── quickstart.md                    # Phase 1 — how to prove each success criterion
├── contracts/
│   ├── chat-message-cipher.md       # IChatMessageCipher: C1–C11, S1–S6
│   └── chat-api-delta.md            # the one added DTO field
├── checklists/
│   ├── requirements.md              # spec quality (done)
│   └── ui-review.md                 # Gate 7 (instantiated at implementation)
└── tasks.md                         # /speckit-tasks output — not created by /speckit-plan
```

### Source Code (repository root)

```text
backend/
├── Entities/ChatMessage.cs                          # Body(string) → BodyCipher(byte[]); XML doc rewritten
├── Data/
│   ├── AppDbContext.cs                              # L1015: HasMaxLength(2000) → bytea, IsRequired
│   ├── DevDataSeeder.cs                             # L255-260: seed through the cipher
│   └── Migrations/*_EncryptChatMessageBodies.cs     # drop Body, add BodyCipher
├── Services/Chat/
│   ├── Encryption/
│   │   ├── IChatMessageCipher.cs                    # NEW — the seam
│   │   ├── AesGcmChatMessageCipher.cs               # NEW — envelope, AAD, TryUnprotect
│   │   ├── ChatEncryptionOptions.cs                 # NEW — "version:base64;…", first entry writes
│   │   └── ChatEncryptionServiceCollectionExtensions.cs  # NEW — parse + validate + fail fast
│   ├── ChatMessageService.cs                        # L100-110 encrypt on send; L286/340 project
│   │                                                #   BodyCipher; L413 decrypt in ToDto;
│   │                                                #   L468 clear to []
│   └── ChatConversationService.cs                   # L456/499 inbox preview: decrypt or ""
├── Dtos/Chat/ChatDtos.cs                            # MessageDto gains IsUnavailable
└── Program.cs                                       # register + validate the cipher (beside the Redis guard)

frontend/apps/web/
├── src/app/core/models/chat.models.ts               # ChatMessage.isUnavailable
├── src/app/features/chat/chat-conversation/
│   └── chat-conversation.component.html             # L112 tombstone branch gains a sibling
├── public/i18n/{en,de,es}.json                      # + chat.conversation.messageUnavailable
└── public/i18n/legal/{de,en,es}.json                # L129 — the messages paragraph

infra/
├── locals.tf                                        # connection_string += SSL Mode + Root Certificate
├── variables.tf                                     # + chat_encryption_keys (sensitive)
└── modules/app/
    ├── main.tf                                      # cert-manager Issuer/Certificate ×3; postgres
    │                                                #   cert mount + fs_group; backend CA mount;
    │                                                #   app-secrets += Chat__Encryption__Keys
    ├── analytics.tf                                 # Umami DATABASE_URL += ?sslmode=require
    └── variables.tf                                 # + chat_encryption_keys

scripts/dev-postgres-certs.ps1                       # NEW — local CA + server cert into certs/local/
docker-compose.yml                                   # postgres ssl=on wrapper; cert + CA mounts;
                                                     #   backend conn string; umami sslmode
.env.sample / .gitignore / infra/README.md           # the key, the certs, the runbook note
.github/workflows/deploy.yml                         # TF_VAR_chat_encryption_keys (dev + prod block)
```

**Structure Decision**: the existing web-application layout. The only new directory is
`backend/Services/Chat/Encryption/`, which sits inside the chat service namespace rather than
in a general `Services/Security/` — this cipher is chat's, its associated data is a
`ChatMessage.Id`, and extending it to other columns later (explicitly out of scope) is the
moment to generalise it, not before.

## Implementation Sequence

Six phases. Each ends somewhere the suite is green, so a phase can be a commit.

| # | Phase | What lands | Verification |
|---|---|---|---|
| 1 | **Cipher** | `Services/Chat/Encryption/*`, options parsing, startup validation, DI | Unit tests C1–C11 + S1–S6 (contract). Nothing else references it yet |
| 2 | **Column** | Entity, `AppDbContext`, migration, `DevDataSeeder`, the five call sites, `MessageDto.IsUnavailable` | Backend suite green; a test reads the raw row and asserts the plaintext is absent (SC-001) |
| 3 | **Failure path** | `TryUnprotect` → `IsUnavailable`, the warning log, i18n key ×3, the template branch, model field | Backend test corrupts a row and asserts the page still returns with one flagged message (SC-006); frontend spec asserts the placeholder renders; `catalog-parity.spec.ts` green |
| 4 | **Transport** | `dev-postgres-certs.ps1`, compose wiring, cert-manager manifests, `locals.tf`, mounts, Umami `sslmode` | `docker compose up` connects with `VerifyFull`; `terraform fmt -check` + `validate`; quickstart §4 by hand |
| 5 | **Secrets & runbook** | `variables.tf` ×2, `app-secrets`, `deploy.yml` ×2 blocks, `.env.sample`, `.gitignore`, `infra/README.md` key-backup note | `terraform validate`; a deliberate missing-key start fails (SC-005) |
| 6 | **Disclosure** | The messages paragraph in `legal/{de,en,es}.json` — German written first | `legal-catalog.spec.ts` green; every claim checked against §11's table; UI review checklist |

Phase 1 before phase 2 is not arbitrary: the cipher's tests are the only place the envelope
is asserted directly, and writing them against a finished, unused component is what stops
"the tests pass" from meaning "the services agree with themselves".

## Risks and the shape of getting it wrong

| Risk | Mitigation |
|---|---|
| **A projection is added later that puts ciphertext in front of a player** | The property is named `BodyCipher` and typed `byte[]`; a DTO string field will not accept it. Research §1 |
| **`ILIKE` over message text creeps back** | Impossible against `bytea` without a schema change. Data-model D3 |
| **Deleted/system rows get an envelope instead of `[]`** | `Protect("")` throws (contract C4) and `ChatDeleteTests` asserts `Assert.Empty`. Data-model D2 |
| **Postgres crash-loops on certificate permissions** | The rule, the exact modes and the `fs_group` are written down in research §6, with the local copy-inside-the-container wrapper for Windows bind mounts |
| **`Host=postgres` is not in the certificate SAN** | `VerifyFull` checks the host *as written*; the `dnsNames` list in research §6 includes the short name, and quickstart §4 fails loudly if it does not |
| **`Trust Server Certificate=true` gets added to silence a handshake error** | Called out in research §7 as forbidden; it would silently undo FR-016 |
| **The key is lost and every message with it** | FR-024: the backup note goes in `infra/README.md` where a restore is performed, not only in a spec |
| **Terraform plan fails on a fresh cluster** | `kubernetes_manifest` needs cert-manager's CRDs at *plan* time — the two-phase apply already documented in `infra/README.md`, inherited with the same `depends_on` |
| **Dev's already-initialised PVC** | Avoided entirely: nothing here uses `/docker-entrypoint-initdb.d/` and `pg_hba.conf` is left alone (research §8) |

## Spec drift and residuals, recorded

- **FR-015 / SC-003 — the server does not *refuse* plaintext.** `ssl = on` enables TLS; it
  does not require it. Every client we ship asks for TLS and the backend verifies, but a
  client that insisted on plaintext would still be accepted. Forcing it means replacing
  `pg_hba.conf` on an initialised Dev volume — a plausible way to break the database for a
  gain only against our own misconfiguration. Research §8.
- **FR-009 — the inbox preview does not distinguish unavailable from deleted.** Both preview
  as an empty string. A second flag on `LastMessageDto` was judged not worth the contract
  change; the conversation itself shows the placeholder.
- **FR-019 — Redis is stated, not built.** The requirement goes in the spec and as a comment
  on #219. Until it lands there is no backplane hop in any deployed environment, because
  there is no backplane.
- **Test-harness parity** — see the Constitution Check deviation above.
- **Umami's hop is encrypted but not verified** (`sslmode=require`). It carries analytics, not
  messages, and giving Prisma the internal CA is disproportionate.

## What changed during implementation

Recorded rather than quietly absorbed. None of it changes a requirement; all of it is the plan
meeting the code.

| Planned | Built | Why |
|---|---|---|
| `ChatEncryptionOptions` in `Services/Chat/Encryption/` | `backend/Common/ChatEncryptionOptions.cs` | Every other options class lives in `Common/`. Following the existing convention beats matching a file tree drawn before looking |
| Keys parsed **at registration**, beside the Redis guard | Parsed when the singleton is **constructed**, forced by `app.Services.ValidateChatMessageEncryption()` right after `builder.Build()` | `Program.cs` already documents that a test host layers configuration in *after* composition — a value read during registration never sees it, which broke every integration test at once. Validation still happens at startup, and now **before** the migrations: refusing to start on a missing key should not first alter anyone's schema |
| — | `backend/Data/DesignTimeDbContextFactory.cs` | The startup guard also blocked `dotnet ef migrations add`. Scoping the fix to the tools beats relaxing the guard: an escape hatch inside the guard is a code path that starts the application without encryption, and something would eventually take it |
| — | `IChatMessageCipher.VersionOf` | The decrypt-failure log needs the key version — the one actionable fact in a failed row — without the call site indexing into the envelope |
| — | `ChatMessageSeed` test helper; `ChatEncryptionStartupTests` | Tests that seed rows directly can no longer assign a string. The startup suite is SC-005, which had no home in the original task list |
| `Row` unchanged apart from the body | `Row` gained `ConversationId` | The warning log names the conversation as well as the message; `ProjectOneAsync` had no other way to know it |
| Link card left alone | Suppressed when `isUnavailable` | A card beside a placeholder would advertise the content of the message we are telling the reader we cannot show |
| Certificate lifetimes left at cert-manager's defaults | Explicit **10-year CA / 5-year server** durations | **PostgreSQL does not re-read `ssl_cert_file` by itself.** At the default 90-day cadence cert-manager would renew, the kubelet would update the file, and Postgres would keep serving the old certificate until the pod happened to restart — self-healing invisibly inside the 30-day overlap and otherwise refusing connections three months after an apply. Long lifetimes make rotation a deliberate act instead of an unset timer; solving it properly is **GH #239** |
| Local certificates generated by hand | Generated by the `SessionStart` hook (pwsh, with an openssl fallback), and copied into new worktrees by the `WorktreeCreate` hook | The files are gitignored, so a fresh clone had none and `docker compose up` failed at the database with `install: can't stat '/certs/server.key'` — which does not read as "run a script". The remote sandbox was affected too, since its hook already seeds `.env` |

## Out of scope (restated so it is not re-litigated at implementation)

End-to-end encryption (#223 option 3, declined by the owner); encrypting any other text
column; performing a key rotation or building tooling for one; deploying Redis (#219);
database backups (deferred from 015); session-replay masking of on-screen chat (038); and any
change to who may read which conversation.

---

**Phase 0**: [research.md](./research.md) — complete, no NEEDS CLARIFICATION remaining.
**Phase 1**: [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md) — complete.
**Phase 2**: `/speckit-tasks`.
