# Contract: legal catalogue shape and release guards

The `terms` document is added to the existing `frontend/apps/web/public/i18n/legal/{en,de,es}.json`
files. No new file, no new loading mechanism, no Transloco scope (036 deliberately fetches these
itself — see `LegalContentService`'s header comment).

---

## Shape

```jsonc
{
  "meta":      { /* unchanged — shared by privacy and imprint */ },
  "crossLink": {
    "toPrivacy": "…", "toImprint": "…",
    "toPrivacyLong": "…", "toImprintLong": "…",
    "toTerms": "…", "toTermsLong": "…"          // NEW
  },
  "privacy":   { /* unchanged */ },
  "imprint":   { /* unchanged */ },

  "terms": {                                     // NEW
    "title": "Terms of Use",
    "version": "2026-08-03",                     // MUST be byte-identical in all three files
    "lastUpdated": "2026-08-03",                 // this document's own date (research R4)
    "intro": [ "…" ],
    "sections": {
      "whatThisIs":      { "heading": "…", "body": [ "…" ] },
      "yourAccount":     { "heading": "…", "body": [ "…" ] },
      "behaviour":       { "heading": "…", "body": [ "…" ] },
      "yourContent":     { "heading": "…", "body": [ "…" ] },
      "whatWeMayDo":     { "heading": "…", "body": [ "…" ] },
      "endingIt":        { "heading": "…", "body": [ "…" ] },
      "noGuarantees":    { "heading": "…", "body": [ "…" ] },
      "changesAndLaw":   { "heading": "…", "body": [ "…" ] }
    }
  }
}
```

`LegalContent` gains `terms: LegalDocument`; `LegalDocument` gains optional `version?: string` and
`lastUpdated?: string`; `LegalDocumentKey` gains `'terms'`.

---

## Reading order

Declared in `TermsComponent` as an explicit array, never inferred from JSON key order — the same
rule `PRIVACY_SECTIONS` follows, so a reordered catalogue cannot quietly reshuffle a binding text.

| # | Key | Carries |
|---|---|---|
| 1 | `whatThisIs` | What JuggerHub is, who runs it, that this is an agreement between the reader and the operator |
| 2 | `yourAccount` | Account is yours to keep secure; accurate handle/name; **the guardian clause** (FR-013) |
| 3 | `behaviour` | **The community rules.** The section people are actually pointed at |
| 4 | `yourContent` | Reader keeps ownership; grants only a display permission (FR-006) |
| 5 | `whatWeMayDo` | Removal, suspension, ban — the reserved rights (FR-005) |
| 6 | `endingIt` | Self-erasure exists and is immediate; what survives it |
| 7 | `noGuarantees` | Volunteer-run, no uptime or fitness promise |
| 8 | `changesAndLaw` | Publish-only change notice (FR-014), German law, the contact address |

### Drafting constraints the text must satisfy

- **FR-004** — `behaviour` covers every surface a member can write or upload to: profile,
  team/event/training descriptions, chat messages, marketplace listings, images, and chosen
  names and handles. Written as categories of conduct, not an enumeration of features, so a new
  feature does not silently fall outside the rules (the same reasoning 036 applied to the privacy
  policy's sections).
- **FR-008** — no review timelines, no appeal procedure, no report button, no "our moderation
  team". None of those exist. `whatWeMayDo` says what may happen, not what process precedes it.
- **FR-006 / FR-009** — the privacy policy already says *"What you write and upload is yours until
  you say otherwise."* `yourContent` must grant a **display permission only**, never a broad
  content licence, or the two documents contradict each other.
- **FR-009** — `endingIt` must match what feature 037 actually does: erasure is self-service and
  immediate, messages and news posts survive shown as "A former player", and the email is released
  for re-registration. The privacy policy's `rights` section is the reference text.
- **FR-007** — the contact address is `hello@juggerhub.com`, the one already published in the
  privacy policy and imprint. No new channel is invented.
- **FR-013** — no minimum age, no age question anywhere in the product; the guardian clause in
  `yourAccount` is the whole of it.
- **Voice** — DESIGN.md: *"Legal content is precise, not stiff."* The German text is the binding
  one and is written first; `en` and `es` are translations of it, not independent drafts.

---

## Release guards

### G1 — Identical key sets across en/de/es *(already in place)*

`core/i18n/legal-catalog.spec.ts` walks the **entire parsed file** rather than a fixed document
list, so the `terms` node is covered the moment it is added. No test change needed — but this is
**verified by running it**, not assumed.

Why it matters: `app.config.ts` sets `useFallbackTranslation: true` with `fallbackLang: 'en'`. A
paragraph missing from `de.json` renders the **English text inside the legally binding German
document**, silently. A failure here is fixed by adding the missing German text — **never** by
changing the global fallback, which feature 031 relies on app-wide.

### G2 — No `__TODO__` sentinel reaches a release *(already in place)*

Same walk, same automatic coverage.

### G3 — Version parity *(new)*

A backend integration test asserts:

1. `terms.version` is **byte-identical** across `en.json`, `de.json` and `es.json`
2. that value equals `TermsOptions.CurrentVersion`

G1 compares **keys**; values are supposed to differ between translations. `terms.version` is the
one leaf that must not, and nothing existing checks it.

Implementation reuses the repo-walk already proven in
`backend/tests/…/Email/TemplateParityTests.cs` — `AppContext.BaseDirectory` upward until
`frontend/apps/web/public/i18n/legal` is found, **throwing** if it is not. Throwing rather than
skipping is the point: a guard that silently stops running is worse than no guard.

Failure of G3 means an acceptance record would name a version whose text nobody saw — the exact
failure this feature exists to prevent.
