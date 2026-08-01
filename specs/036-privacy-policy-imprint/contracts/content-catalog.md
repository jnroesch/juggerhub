# Contract: The `legal` Translation Scope (036)

Defines the shape, the rules, and the automated guards for the legal text. The structure itself is
in [../data-model.md](../data-model.md); this file is the enforceable contract.

---

## 1. Location and loading

| | |
|---|---|
| Files | `frontend/apps/web/public/i18n/legal/en.json`, `de.json`, `es.json` |
| Loaded by | `LegalContentService` (route-provided), via the shared `HttpClient` |
| When | on activation of `/privacy` or `/imprint` only, following `TranslocoService.langChanges$` |
| Key access | typed — `content().privacy.sections.analytics.heading` |

> **Amended during implementation (research R2a).** This was planned as a Transloco *scope*. It is
> not one: Transloco has no error surface, so a failed scope load would have rendered the **English**
> text inside the legally authoritative German document — the exact failure §4 below exists to
> prevent — and PC-7 requires a visible error instead. The files, their location, their laziness,
> the language-switch behaviour and every guard below are unchanged; they check the JSON, not the
> loader.

The prose is **not** in the main `public/i18n/{lang}.json` catalogs. Those are fetched on every app
load; several thousand words × 3 languages does not belong in the critical path of a page almost
nobody opens (research R2).

Short **labels** — the footer links, the page titles used in navigation — *do* live in the main
catalogs under `legal.*`, because the footer renders on every screen and cannot wait for a lazy
scope.

---

## 2. Authoritative language

| Language | Status | Renders `meta.authoritativeNotice` |
|---|---|---|
| `de` | **Authoritative** — the binding text | No |
| `en` | Informational translation | **Yes** |
| `es` | Informational translation | **Yes** |

The notice states in the reader's own language that the German version governs in case of
divergence (FR-019). It is rendered in the meta line under the `h1`, at the `caption` step — visible
without competing with the content.

---

## 3. Value rules

| # | Rule | Rationale |
|---|---|---|
| CV-1 | Every leaf value is a plain string. Paragraphs are **array entries**, never one string with embedded markup | No HTML is interpolated; no `[innerHTML]` sink is introduced (Constitution I) |
| CV-2 | No leaf value contains `<` | Enforced mechanically, so CV-1 cannot erode over time |
| CV-3 | Interpolation params (`{{ }}`) are used only for the last-updated date and the operator name | Keeps legal sentences whole and translatable |
| CV-4 | `meta.lastUpdated` is an ISO `YYYY-MM-DD` string, formatted at render time by `transloco-locale` | 031 FR-009 — the date reads correctly per locale |
| CV-5 | The three files have **identical key sets** | See §4 |
| CV-6 | No value contains the placeholder sentinel `__TODO__` | See §5 |

---

## 4. Completeness (CV-5) — why this is a hard guard

`app.config.ts` sets `missingHandler: { useFallbackTranslation: true }` with `fallbackLang: 'en'`.
For interface labels this is exactly right and is required by 031 (FR-008/FR-018: never render a
blank or a raw key).

For a **German-authoritative legal document it is a hazard**. A paragraph missing from `de.json`
does not render blank and does not log in production — it renders the English text, inside a German
document, with no visible signal. The result looks complete, is the legally binding version, and is
partly in the wrong language.

**Guard**: a Jest test asserting the three catalogs have identical key sets, recursively. A missing
or extra key fails the build. Implemented in
`frontend/apps/web/src/app/core/i18n/legal-catalog.spec.ts`.

The global fallback is deliberately **not** changed. Disabling it would satisfy this feature and
break 031's guarantee for the other ~2000 keys in the app. The narrow test is the right-sized fix.

Note that the direct-fetch loader (research R2a) removes the *runtime* half of this hazard — a
missing German key now renders nothing rather than English — but not the reason for the test. A
silently absent paragraph in a legal document is still a defect; the test is what catches it before
anyone reads the document and doesn't notice what isn't there.

---

## 5. Placeholder guard (CV-6) — the imprint particulars

Spec Q1 (the operator's name, postal address, contact, legal form) is open. The plan builds
everything else and marks those values with the sentinel `__TODO__`.

**Guard**: the same test asserts no value in any legal catalog contains `__TODO__`.

**This test failed until Q1 was answered, and that was the intended state.** The failure mode it
prevents is specific and realistic: the structure is complete, the page renders, review passes on
everything visible, and a placeholder reaches Prod inside the one document whose content is legally
prescribed. "We'll fill it in before deploy" is how that happens. A red build is not.

**Resolved 2026-08-01.** The particulars were supplied, the sentinel disappeared, and the build went
green **with no code change** — the guard worked as designed, start to finish.

On the git-history warning that stood here: it was moot. The address was **already committed** to
this public repository, in all three transactional email footers
(`backend/EmailTemplates/{en,de,es}/footer.html`), so the imprint added no exposure. The rule the
guard enforces is unchanged and still applies to anything added later: values in these files are
permanent and public.

---

## 6. Content accuracy

Every factual claim in the privacy policy is an assertion about the running system. These are not
checkable by a unit test and are review items (SC-002, SC-003, SC-005), listed here so the review
has a concrete target:

| Claim | Verify against |
|---|---|
| Every data category the platform holds falls inside one of the policy's categories | `backend/Entities/` — see the table in [../data-model.md](../data-model.md) |
| Analytics behaviour: no cookie, no device storage, no viewer identifier, verbatim paths, no query strings | `specs/033-umami-analytics/spec.md` FR-005 to FR-009a |
| Processors named: Azure (hosting, storage, `westeurope`), Resend (email) | constitution Technology Stack; `infra/envs/*.tfvars` |
| Session records retain an originating IP | `backend/Entities/RefreshToken.cs` → `CreatedByIp` |
| Conversations can outlive the team/event they belonged to | features 019, 027 (snapshot archival) |
| Messages are not end-to-end encrypted | `backend/Entities/ChatMessage.cs` — content stored in plain columns |
| Members can enter details about non-members | `backend/Entities/EventContact.cs` — name, phone, email |
| The DNT/GPC opt-out actually works | **re-verified end to end in this feature**, not cited from 033 (research R5) |

The last row is deliberate: the policy describes DNT as a working opt-out, so this feature proves
it rather than trusting it.

**The audit is exhaustive; the prose is not.** Since 2026-08-01 the policy is written in categories
rather than per feature (spec Clarifications), so this review checks *coverage* — that nothing the
platform holds falls outside a stated category — rather than that each feature is named. A new
feature normally needs **no policy edit**; it needs a check that it fits an existing category, and
a new category only if it genuinely does not.

**Claims removed, and why they must not come back** (FR-004a): "no third-party analytics service",
"no advertising network", "no geocoding processor", "no Google Fonts", "no automated retention runs
today", "there is no self-service export or deletion". Each was verified and true when written, and
each would have gone false without anyone noticing — a legal document asserting something that
quietly stopped being true. Durable commitments ("we don't sell your data") are fine and stayed.
The single exception is the no-consent-banner reasoning, where the absence of device storage *is*
the disclosure.
