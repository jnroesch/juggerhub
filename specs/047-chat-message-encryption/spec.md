# Feature Specification: Chat Message Encryption at Rest + Database Transport Hardening

**Feature Branch**: `047-chat-message-encryption`

**Created**: 2026-09-09

**Status**: Draft

**Input**: User description: "Encrypt chat messages at rest and harden the database transport (GH #223, owner picked options 1 + 2; option 3 end-to-end encryption is explicitly declined). (A) Transport hardening: the backend↔Postgres hop currently runs plaintext TCP inside the cluster with no SSL Mode in the connection string and no server certificate on Postgres — give Postgres a server certificate and require TLS on the connection, in every environment so parity holds. Redis is not deployed at all yet (#219), so Redis TLS/AUTH is only in scope insofar as it can be stated as a requirement for whoever lands #219. (B) Application-level encryption at rest for chat message bodies: the backend encrypts the message text before writing and decrypts on read, with a key supplied through the deployment's existing secret channel, a key-version marker per row so the key can be rotated later, and ciphertext stored in a wider column. Feature 046 removed the only server-side code that read message bodies for anything other than displaying them, so nothing needs bodies queryable in the database any more. Everything that renders messages must keep working unchanged. There is only test data in every environment, so no backfill or dual-read window is needed. The privacy policy sentence in all three legal catalogues (German authoritative) must be corrected in the same feature."

## Context & Decision

GitHub issue **#223** framed three options for what "encrypt messages" should mean for
JuggerHub. The owner picked **1 + 2** and declined **3**:

| Option | Decision |
|---|---|
| 1 — Transport hardening (TLS on the database hop) | **In scope** |
| 2 — Application-level encryption of message text at rest | **In scope** |
| 3 — End-to-end encryption (keys held by clients) | **Declined.** Not this product. See "Out of Scope". |

Today message text is stored verbatim in the database, the backend↔database hop runs
unencrypted inside the cluster, and the only protection at rest is the hosting platform's
disk encryption. The privacy policy states this plainly in all three languages. Feature
**046** removed the last piece of server-side code that needed message text to be
searchable in the database, which is what makes encryption-at-rest tractable now.

The two halves ship together because each on its own leaves the privacy statement
half-true, and the statement must be corrected exactly once.

**What changes for a reader of the privacy policy**: today, database access alone reveals
message text. After this feature, database access alone reveals nothing readable; reading
messages requires the database **and** the encryption key, which are held and backed up
separately. That is a real but bounded gain and must be described honestly — in
particular it must **not** be described as end-to-end encryption, which it is not.

## Clarifications

### Session 2026-09-09 (owner)

- Q: The transport half covers two hops — application↔database and application↔realtime
  backplane. The backplane is not deployed anywhere (#219). How far does this feature
  reach? → A: **Database only.** Give the database a certificate and require verified
  encryption in all three environments. The backplane requirement is *stated* here
  (FR-019) and carried to #219; this feature does not deploy it. Rejected: absorbing #219
  into this feature; saying nothing about the backplane at all.
- Q: The database must carry a server certificate either way. Should the application also
  verify it, or merely encrypt the traffic? → A: **Verify.** The application validates the
  certificate against a known authority and that the hostname matches, and refuses
  anything else. Encrypt-only was rejected: since a certificate is required regardless,
  verification costs only distributing the authority certificate, and without it any
  process that can win the database's name inside the cluster reads everything.
- Q: What should a reader see if a stored message cannot be decrypted (a key retired
  without its predecessor, a corrupted row)? → A: **A neutral placeholder for that message
  only.** The conversation still opens and every other message renders. Failing the whole
  request was rejected — one bad row must not silence a thread for everyone in it. The
  failure is recorded for the operator, without the ciphertext.
- Q: How ambitious should key handling be? → A: **Several key versions configurable at
  once, one designated as current for writing.** Every row records its version. Rotating
  later is then a configuration change rather than a code change. A single key with a
  version marker only was rejected for exactly that reason.
- Q: If the encryption key is missing or unusable at startup, should the application run
  without encryption? → A: **No.** It refuses to start, the same way a missing realtime
  backplane already does. Silently degrading to plaintext is the failure mode this
  feature exists to remove.
- Q: Does this cover other free-text the platform stores (news posts, team and event
  descriptions, marketplace listings, contact details)? → A: **No — chat messages only.**
  The same mechanism can be extended later; #223 says messages first.
- Q: Existing message rows in Dev/local? → A: **No migration of data.** Every environment
  holds test data only; databases may be cleared. No backfill, no dual-read window, no
  backward compatibility with plaintext rows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A database copy reveals no message text (Priority: P1)

Someone who obtains a copy of the database — a stolen backup, a dump handed to a
contractor, a curious operator with query access — can read every other column but cannot
read what players wrote to one another. The message rows are there, in order, with their
senders and timestamps, but the text itself is unintelligible without a key that does not
live in the database.

**Why this priority**: This is the sentence in the privacy policy that #223 is about. It
is the only part of the feature that changes what a database compromise reveals, and it is
the half that requires the product to change rather than the deployment.

**Independent Test**: Send a message through the product, then read the stored row
directly with a database client. The text the player typed does not appear. Reopen the
conversation in the product; the message reads normally.

**Acceptance Scenarios**:

1. **Given** a player sends "meet at the north pitch at 18:00", **When** an operator selects
   that row directly from the database, **Then** the stored value contains none of that
   text and is not human-readable.
2. **Given** the same message, **When** the sender or the recipient opens the conversation
   in the product, **Then** the message renders exactly as it was typed.
3. **Given** a conversation with messages, **When** a member views their chat inbox,
   **Then** the last-line preview shows the real message text, unchanged from today.
4. **Given** a member receives a message while the conversation is open, **When** it
   arrives live, **Then** it appears immediately and reads correctly, as today.
5. **Given** a sender deletes their own message, **When** an operator inspects the row,
   **Then** neither the text nor any encrypted form of it remains — the row is empty, as
   today.
6. **Given** a message containing a link to a JuggerHub team, event or training, **When**
   it is sent, **Then** the link card resolves and renders exactly as today.

---

### User Story 2 - Database traffic is encrypted and the server is verified (Priority: P2)

The connection between the application and the database is encrypted and authenticated in
every environment. Someone able to observe traffic inside the cluster sees no message
text, no credentials and no personal data; someone able to impersonate the database on the
network is refused rather than trusted.

**Why this priority**: Cheap, self-contained, and worth doing regardless of the
encryption-at-rest half — but on its own it does nothing about database access, which is
what the privacy statement is about. It follows P1 rather than leading it.

**Independent Test**: Capture traffic on the database port while the application is in
use and confirm nothing intelligible appears. Separately, point the application at a
database presenting an untrusted certificate and confirm it refuses to connect.

**Acceptance Scenarios**:

1. **Given** the application is running normally, **When** traffic on the database port is
   observed, **Then** no query text, column value or credential is readable.
2. **Given** the database presents a certificate that does not chain to the expected
   authority, **When** the application starts, **Then** it fails to connect and reports a
   connection error rather than proceeding unencrypted.
3. **Given** any of the three environments (local, Dev, Prod), **When** the application
   connects, **Then** encryption is required and verified identically — the environments
   differ only in certificate material, never in whether verification happens.

---

### User Story 3 - The privacy policy states what is actually true (Priority: P2)

A reader of the privacy policy learns, accurately, what protects their messages: the text
is encrypted where it is stored, so database access alone does not reveal it; the platform
can still read messages because it holds the key; and this is deliberately not end-to-end
encryption.

**Why this priority**: Shipping P1 without this leaves a published statement that is
false in the reader's favour but false nonetheless, and understating protection is still
a misdescription. It ships in the same release as P1.

**Independent Test**: Read the messages paragraph of the privacy policy in all three
languages and check each claim against the deployed behaviour.

**Acceptance Scenarios**:

1. **Given** the feature is deployed, **When** a reader opens the privacy policy in
   German, English or Spanish, **Then** the messages paragraph says message text is
   encrypted where it is stored and describes what still allows the platform to read it.
2. **Given** the same paragraph, **When** it is read closely, **Then** it does not claim
   or imply end-to-end encryption.
3. **Given** the three language versions, **When** they are compared, **Then** they make
   the same claims, with German as the authoritative text.

---

### User Story 4 - The key can be replaced without a rebuild (Priority: P3)

If the encryption key is ever suspected of exposure, the operator can introduce a new key
without losing access to messages already stored, because every stored message records
which key version protects it.

**Why this priority**: Rotation is not exercised in this feature, but the ability to
rotate later must be designed in now — retrofitting a version marker onto rows already
written is far more expensive than writing it from the start.

**Independent Test**: Inspect stored messages and confirm each one carries an identifiable
key version. Configure a second key version alongside the first and confirm messages
written under the older version still read correctly.

**Acceptance Scenarios**:

1. **Given** a stored message, **When** it is inspected, **Then** the key version that
   protects it is identifiable without trial decryption.
2. **Given** two key versions configured at once, **When** messages written under either
   version are read, **Then** both read correctly.
3. **Given** two key versions configured, **When** a new message is written, **Then** it
   is protected by the version designated as current.

---

### Edge Cases

- **A message cannot be decrypted** (key replaced without its predecessor, corrupted
  row): the conversation still opens; that single message shows a neutral
  message-unavailable placeholder in the reader's language; every other message renders
  normally. The event is recorded for the operator without including the ciphertext or
  any key material.
- **The key is missing or unusable at startup**: the application refuses to start with a
  clear operator-facing error. It never falls back to storing text unencrypted.
- **System lines and deleted messages carry no text** today (a membership-change line, or
  a message the sender withdrew). They must continue to store nothing, so that "the
  content is genuinely gone" stays a fact about the row and not a rendering convention.
- **The longest allowed message** (2 000 characters, and characters that occupy more than
  one byte) must still be storable after encryption expands it; the limit players
  experience is unchanged.
- **The key is lost entirely**: every stored message becomes permanently unreadable. This
  is accepted and is why the key must be retained separately from the database, and why
  losing the database and the key together is a total loss of chat history.
- **A database backup is taken** (none exist yet): it contains ciphertext, so a backup
  restored without the key yields no readable messages. The restore procedure must treat
  the key as a required companion artefact.
- **Realtime delivery across replicas**: message text passing through the realtime
  backplane is not covered by encryption at rest. Until the backplane hop is itself
  encrypted and authenticated, that remains an in-cluster plaintext hop, and the privacy
  statement must not claim otherwise.

## Requirements *(mandatory)*

### Functional Requirements

**Encryption at rest**

- **FR-001**: The system MUST encrypt chat message text before it is stored, using an
  authenticated encryption scheme, so that the stored value reveals neither the text nor a
  way to alter it undetectably.
- **FR-002**: The system MUST decrypt message text transparently on every read path that
  exists today — the conversation view, the inbox last-line preview, the realtime push,
  and any single-message response — with no change to what a player sees.
- **FR-003**: The encryption key MUST NOT be stored in the database, in the source
  repository, or in any artefact derived from either.
- **FR-004**: The encryption key MUST be supplied through the same secret channel the
  platform already uses for deployed secrets, and through the local environment file for
  local development, so no new secret-management dependency is introduced.
- **FR-005**: Every stored message MUST record which key version protects it, so a key can
  be introduced or retired later without trial decryption and without a data migration.
- **FR-006**: The system MUST support more than one configured key version at a time, of
  which exactly one is designated for writing; messages written under any configured
  version MUST remain readable.
- **FR-007**: The system MUST refuse to start when no usable key is configured, rather
  than storing message text unencrypted.
- **FR-008**: Messages that carry no text today — system lines, and messages the sender
  has deleted — MUST continue to store nothing, with no ciphertext standing in for the
  absent text.
- **FR-009**: A message that cannot be decrypted MUST NOT prevent its conversation from
  loading; it MUST render as a neutral unavailable-message placeholder while every other
  message renders normally.
- **FR-010**: A decryption failure MUST be recorded for the operator with enough context to
  locate the row, and MUST NOT include the ciphertext, the key, or any part of either.
- **FR-011**: The message length limit players experience MUST be unchanged (2 000
  characters), and a message at that limit — including characters that occupy more than one
  byte — MUST be storable.
- **FR-012**: Link detection in a message MUST continue to operate on the text the player
  typed, at the moment of sending, and MUST continue to store only the kind and identifier
  of the linked item.
- **FR-013**: No part of the platform may read stored message text by matching, sorting or
  filtering on it in the database. This is already true after feature 046 and MUST remain
  true; any future need for that is a re-opening of this decision, not an implementation
  detail.
- **FR-014**: Message text MUST NOT appear in application logs, error responses, or
  telemetry, including when encryption or decryption fails.

**Transport**

- **FR-015**: The application MUST require an encrypted connection to the database in every
  environment; an unencrypted connection MUST fail rather than proceed.
- **FR-016**: The application MUST verify the database server's identity against a known
  certificate authority and refuse a connection it cannot verify.
- **FR-017**: The database MUST present a server certificate in every environment, provided
  by the same mechanism in each, so that local, Dev and Prod differ only in certificate
  material.
- **FR-018**: Certificate material MUST be treated as deployed configuration, following the
  platform's existing secret and configuration channels, and MUST NOT be committed to the
  repository.
- **FR-019**: The realtime backplane hop MUST be encrypted and authenticated once that
  component is deployed. It is not deployed today; this feature MUST record that
  requirement where whoever deploys it will encounter it, and MUST NOT claim the hop is
  protected until it is.

**Disclosure**

- **FR-020**: The privacy policy MUST state that message text is encrypted where it is
  stored, in all three languages, with German authoritative.
- **FR-021**: The privacy policy MUST state plainly that the platform can still read
  messages because it holds the key, and MUST NOT claim or imply end-to-end encryption.
- **FR-022**: The three language versions MUST make the same claims; no language may
  describe a protection the others omit.
- **FR-023**: Any other published statement about how message text is protected MUST be
  reviewed in this feature and corrected if this change makes it inaccurate.

**Operational**

- **FR-024**: The key MUST be retained outside the database, and the requirement to keep it
  MUST be documented where an operator restoring a database will encounter it — losing the
  key destroys every stored message.
- **FR-025**: Existing stored messages MUST NOT be migrated. Every environment holds test
  data only; databases may be cleared. No compatibility with previously stored plaintext is
  required or provided.

### Key Entities

- **Chat message**: unchanged in meaning. Its text is now stored in a protected form that
  also carries the identifier of the key version protecting it. Every other attribute —
  sender, conversation, ordering, deletion state, link reference, system-line fields — is
  untouched.
- **Encryption key set**: operator-supplied configuration naming one or more key versions
  and designating which is used for new messages. Lives entirely outside the database.
- **Database server certificate**: operator-supplied material allowing the application to
  verify it is talking to the real database.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of messages sent through the product, the text a player typed is
  absent from the stored row when read directly from the database.
- **SC-002**: 100% of the message-rendering paths that worked before this change work
  identically after it — conversation view, inbox preview, live delivery, deletion, link
  cards — with no visible difference to a player.
- **SC-003**: Observing traffic on the database port during normal use yields zero
  intelligible query text, column values or credentials.
- **SC-004**: An application configured against a database whose certificate cannot be
  verified fails to connect, in 100% of attempts, in every environment.
- **SC-005**: An application started with no usable encryption key fails to start, in 100%
  of attempts, and stores no message text.
- **SC-006**: A conversation containing one undecryptable message still loads and displays
  every other message, with the affected one shown as unavailable.
- **SC-007**: 100% of stored messages carry an identifiable key version, and messages
  written under a non-current configured version remain readable.
- **SC-008**: Every claim in the messages paragraph of the privacy policy, in all three
  languages, is verifiable against the deployed behaviour, and none of them asserts
  end-to-end encryption.
- **SC-009**: No message text, ciphertext or key material appears in any log line or error
  response produced by the encryption, decryption, or connection paths.

## Out of Scope

- **End-to-end encryption** (issue #223 option 3). Explicitly declined by the owner. It
  would require client-held keys, key exchange and multi-device handling for a web
  application with no native keystore, rekeying on every roster change in group, team and
  party chats, and would remove the server-side inbox preview, link cards, the former-player
  placeholder and readable archived snapshots. It is a different product, and this
  feature's disclosure text must not drift toward implying it.
- **Encrypting other stored text** — news posts, team and event descriptions, marketplace
  listings, stored contact details. The same mechanism could be extended to them later;
  #223 sequences messages first.
- **Exercising a key rotation.** This feature makes rotation possible (FR-005, FR-006); it
  does not perform one or build tooling for one.
- **Deploying the realtime backplane** (issue #219). FR-019 states the requirement that
  applies when it lands; it does not deploy it.
- **Database backups** (deferred from feature 015). FR-024 records what a future backup
  procedure must account for; it does not build one.
- **Session-replay masking of on-screen chat history** (feature 038). Message text rendered
  on screen is still captured by session recording; that is a separate lever and a separate
  decision, noted here so it is not mistaken for something this feature closes.
- **Any change to who may read which conversation.** Membership, blocking, hiding and
  archival rules are inherited from features 019, 022, 027 and 046 unchanged.

## Assumptions

- Every environment holds test data only, so databases may be cleared rather than
  migrated. Stated by the owner on 2026-09-09.
- The platform's existing secret channel (deployment environment secrets for Dev and Prod,
  the local environment file for local development) is the delivery mechanism for both the
  encryption key and the certificate material; no dedicated key-management service is
  introduced, consistent with the constitution.
- Chat message text is the only stored value this feature protects; other text fields keep
  their current storage.
- Feature 046 has removed all database-side matching on message text, and nothing
  reintroduces it.
- The realtime backplane is not deployed in any environment today, so no message text
  currently crosses it outside local development.
- An operator holding both the database and the key can read messages. This is understood
  and accepted as the boundary of what options 1 + 2 achieve; it is what the disclosure
  text must convey.
