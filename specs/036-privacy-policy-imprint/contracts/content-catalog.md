# Contract: The `legal` Translation Scope (036)

Defines the shape, the rules, and the automated guards for the legal text. The structure itself is
in [../data-model.md](../data-model.md); this file is the enforceable contract.

---

## 1. Location and loading

| | |
|---|---|
| Files | `frontend/apps/web/public/i18n/legal/en.json`, `de.json`, `es.json` |
| Scope name | `legal` |
| Loaded by | the existing `TranslocoHttpLoader`, which already maps a scoped path (`legal/de`) onto `public/i18n/legal/de.json` — see its own doc comment |
| When | on activation of `/privacy` or `/imprint` only, via `provideTranslocoScope('legal')` on the route |
| Key access | `legal.privacy.sections.analytics.heading` |

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
or extra key fails the build.

The global fallback is deliberately **not** changed. Disabling it would satisfy this feature and
break 031's guarantee for the other ~2000 keys in the app. The narrow test is the right-sized fix.

---

## 5. Placeholder guard (CV-6) — the imprint particulars

Spec Q1 (the operator's name, postal address, contact, legal form) is open. The plan builds
everything else and marks those values with the sentinel `__TODO__`.

**Guard**: the same test asserts no value in any legal catalog contains `__TODO__`.

**This test fails until Q1 is answered, and that is the intended state.** The failure mode it
prevents is specific and realistic: the structure is complete, the page renders, review passes on
everything visible, and a placeholder reaches Prod inside the one document whose content is legally
prescribed. "We'll fill it in before deploy" is how that happens. A red build is not.

Once the particulars are supplied, the sentinel disappears and the build goes green with no code
change.

⚠ **Irreversible**: the values entering these files enter **public git history permanently** — this
repository is public and history cannot be retracted. Research R4 explains why runtime injection was
rejected as a false solution (the address is legally required to be published and is crawled within
days anyway). The real decision is *which* address; a `c/o` or business address is the established
German practice. Make that choice **before** the commit, not after.

---

## 6. Content accuracy

Every factual claim in the privacy policy is an assertion about the running system. These are not
checkable by a unit test and are review items (SC-002, SC-003, SC-005), listed here so the review
has a concrete target:

| Claim | Verify against |
|---|---|
| The data-category list is complete | `backend/Entities/` — see the table in [../data-model.md](../data-model.md) |
| Analytics behaviour: no cookie, no device storage, no viewer identifier, verbatim paths, no query strings | `specs/033-umami-analytics/spec.md` FR-005 to FR-009a |
| Processors: Resend (email, Dev/Prod), Azure (hosting, storage) — and nothing else | constitution Technology Stack; `infra/` for the configured region |
| No geocoding processor | no Photon service in compose or `infra/`; `backend/Services/Geocoding/CityService.cs` reads seeded rows |
| No Google Fonts request | faces ship via `@fontsource` (DESIGN.md Typography) |
| Session records retain an originating IP | `backend/Entities/RefreshToken.cs` → `CreatedByIp` |
| Chat is snapshotted, not deleted, on team delete / event cancel | features 019, 027 |
| No automated retention or deletion runs | no scheduled purge exists |
| No self-service export or account deletion exists | `backend/Controllers`, `backend/Services`, frontend — none found |
| The DNT/GPC opt-out actually works | **re-verified end to end in this feature**, not cited from 033 (research R5) |

The last row is deliberate: the policy will describe DNT as a working opt-out, so this feature
proves it rather than trusting it.
