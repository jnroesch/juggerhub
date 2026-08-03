# Phase 0 Research: Terms of Use with Community Rules

Six decisions. Each was resolved by reading the code rather than by convention, and two of them
overturned the first design I drafted.

---

## R1 — Where the accepted version comes from

**Decision**: The client sends the version string it displayed (`termsVersion`). The server holds
the authoritative value in `TermsOptions.CurrentVersion` and **refuses any request whose version
is not the current one** (`409`). The server records its own constant, never the submitted
string.

**Rationale**: The record's entire purpose is to evidence *what the person saw*. Three candidate
designs:

| Design | Evidence quality | Cost |
|---|---|---|
| Client sends nothing; server stamps current version | Records what the server believed. A stale cached catalogue means the record names text the person never saw — silently. | Cheapest |
| Client sends version; server trusts it | Records a client-controlled string. Trivially forged, and violates Principle I outright. | Cheap |
| **Client sends version; server validates, then records its own** | Proves the client rendered the current text. A mismatch is caught loudly at the moment it matters. | One catalogue fetch on `/register` |

The third is the only one where the record means what the spec says it means (FR-020, FR-023,
FR-025). It also satisfies "never trust the client" properly: the client's value is an *assertion
to be checked*, and the value stored is the server's.

**Consequence** — the register page must know the version, which means loading the legal
catalogue there. If that fetch fails, the acceptance control stays disabled and submission is
blocked. That is not a regression: the spec's edge case already requires that a member "must not
be pushed into agreeing to a document they were unable to read."

**Alternatives rejected**:

- *Put the version in the main translation catalogue* so `/register` needs no extra fetch. It
  would sit in a different file from the text it versions — reintroducing the exact drift the
  design is trying to eliminate, and in a file that loads on every screen.
- *A dedicated `GET /auth/terms-version` endpoint.* The client would then fetch the version from
  the same authority that validates it, so the check would always pass and prove nothing.
- *A separate tiny `version.json`.* Same drift problem as the main catalogue, with an extra file.

---

## R2 — Where the acceptance row is written

**Decision**: As a navigation property on the new `User`, persisted by the existing
`_userManager.CreateAsync(user, request.Password)` call in `RegisterAsync` — the same
`SaveChanges` that creates the account and its profile.

**Rationale**: `RegisterAsync` already does exactly this for `PlayerProfile`, with a comment
saying why: *"Create the account AND its profile atomically: the profile is set on the navigation
so Identity's CreateAsync persists both in one SaveChanges."* Attaching the acceptance the same
way makes FR-022 (a failed registration leaves no acceptance record) **structural** rather than a
compensating cleanup path. There is no window in which one exists without the other, including
the `DbUpdateException` handle-race path already caught in that method.

**Alternatives rejected**: a second `SaveChanges` after `CreateAsync` succeeds — opens a window
where the account exists un-evidenced if the process dies between the two; wrapping the pair in
an explicit transaction — unnecessary, and under Principle VII any user-initiated transaction
would have to be routed through the execution strategy for no gain here.

---

## R3 — How the record survives account deletion

**Decision**: `DeleteBehavior.Restrict` on the user FK, mirroring `AdminActionRecord`. The table
is **not** added to `AccountDeletionService.EraseOwnedDataAsync`.

**Rationale**: Reading `AccountDeletionService` settles this. Erasure does **not** delete the
`User` row — `NeutraliseAccountAsync` overwrites every identifying column (`Email`,
`NormalizedEmail`, `UserName`, `PasswordHash`, `PreferredLanguage`, …) and sets
`Status = Deleted`, while `EraseOwnedDataAsync` deletes the rows the member *owns*. So an
acceptance row keyed on `UserId` survives untouched and points at a row that identifies nobody —
precisely FR-024.

This is the same disposition the privacy policy already publishes for admin actions: *"once an
account is deleted that record no longer points at anyone."* The two documents stay consistent
without new wording.

**The failure mode to defend against** is a future maintainer reading `EraseOwnedDataAsync` as
"delete everything with this UserId" and adding this table to the list. Three guards, deliberately
redundant: the `Restrict` FK turns a naive delete into a loud failure; an integration test asserts
the record survives an erasure; and the entity carries an XML-doc warning.

**Alternatives rejected**: `Cascade` — destroys the evidence, and would make the record useless
for the one dispute where it matters most. Copying the version onto the `User` row instead of a
separate table — a single column cannot represent re-acceptance, which FR-021 exists to keep
possible.

---

## R4 — Document identity in the shared catalogue

**Decision**: The `terms` document carries its **own** `version` and `lastUpdated`, nested inside
the document node. `LegalDocument` gains two optional fields; `LegalPageComponent` prefers the
document's own values over the catalogue-level `meta.lastUpdated`.

**Rationale**: This corrected a premise in my first draft. `meta.lastUpdated` is currently
**shared by both documents** in the catalogue — editing the privacy policy changes the date shown
on the imprint. That is a tolerable wart for two informational documents. It is not tolerable for
a versioned binding contract: a member could see the terms' date change because an unrelated
privacy paragraph was edited, which makes the displayed date actively misleading and undermines
the version it sits next to (FR-003).

Fixing it only for the document that needs it is the smaller change and leaves 036's behaviour
untouched.

**Alternatives rejected**: a catalogue-level `meta.termsVersion` sibling — keeps the misleading
shared date; retrofitting per-document meta onto all three — a change to 036's rendered output
with no requirement asking for it.

---

## R5 — Cross-links between three documents

**Decision**: `LegalPageComponent`'s `siblingLink` + `siblingLabelKey` inputs are replaced by a
single `siblings: {link, labelKey}[]` input. Privacy, imprint and terms each declare the other
two.

**Rationale**: The current inputs encode "there is exactly one other document" — a shape that a
third document simply invalidates. Keeping the binary inputs and adding a second pair
(`sibling2Link`…) would encode "exactly two others" and break again on a fourth.

`jh-legal-links` needs the parallel change: it hard-codes two anchors, and gains a third. Because
all 11 placements (the app footer plus 10 off-shell screens, `/register` among them) render that
one component, adding the link there satisfies FR-010 everywhere at once rather than screen by
screen.

---

## R6 — Guarding the release

**Decision**: Three guards, two of them free.

1. **Identical key sets across en/de/es** — *already covered.* `legal-catalog.spec.ts` walks the
   entire parsed file rather than a fixed list of documents, so the `terms` node is guarded the
   moment it is added. Verified by reading the test, not assumed.
2. **No `__TODO__` sentinel reaches a release** — *already covered*, same reason.
3. **Version parity between catalogue and server** — **new.** A backend integration test walks up
   from the test assembly to `frontend/apps/web/public/i18n/legal/`, reads all three files, and
   asserts each `terms.version` equals `TermsOptions.CurrentVersion`. It also asserts the three
   catalogue values are identical to each other — the key-set guard compares *keys*, and values
   are supposed to differ between translations, so the one leaf that must not differ needs its own
   assertion.

**Rationale**: Guard 3 is cross-stack, which is normally worth avoiding — but the failure it
catches (a record naming a version whose text nobody saw) is precisely the failure this feature
exists to prevent, and it is silent otherwise. The repo-walk pattern is not invented here:
`TemplateParityTests.TemplateRoot()` already does exactly this for the email templates, and
throws rather than skips when the directory is absent. Skipping would let the guard quietly stop
running.

**Alternative rejected**: a release checklist item instead of a test. Version changes are rare
(FR-014 is publish-only), which is exactly why a human step would be forgotten — the checklist
would be read once a year, on the occasion it matters.
