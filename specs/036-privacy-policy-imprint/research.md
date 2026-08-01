# Research: Privacy Policy & Imprint (036)

**Date**: 2026-07-31 · **Spec**: [spec.md](./spec.md) · **Issue**: [#92](https://github.com/jnroesch/juggerhub/issues/92)

Everything below was verified against the repository at `dba8813`, not assumed. Where a decision
diverges from an established project pattern, the divergence is stated as such.

---

## R1 — Two frontend routes, no backend at all

**Decision**: `/privacy` and `/imprint`, both **outside** the `ShellComponent`, both lazy-loaded,
both with **no guard**. No backend endpoint, no controller, no DTO, no entity.

**Rationale**: The content is static translated text. Adding an endpoint would mean adding an
`[AllowAnonymous]` hole in the 026 fallback policy for data that has no server-side dependency —
strictly more attack surface for zero benefit. Constitution Principle II's thin-controller rule is
best satisfied here by having no controller.

The 026 authenticated-only boundary is unaffected: it is enforced server-side by the global
`FallbackPolicy`, and no server call is made. There is nothing to add to the OpenAPI/`AuthController`
allowlist that features 021/026 flag as the recurring gotcha — that gotcha applies to *endpoints*,
and this feature adds none.

**Path naming**: English paths (`/privacy`, `/imprint`), matching every other route in
`app.routes.ts` (`/browse`, `/events`, `/account`). Localized aliases (`/datenschutz`,
`/impressum`) were rejected: the app has no localized routing anywhere, adding it for two pages
would be an inconsistency, and the URL is not the legal artifact — the page content is.

**Outside the shell, not in it**: the shell renders a public bar for anonymous visitors that offers
sign-in and register. On the legal pages that framing is wrong — the reader may be there precisely
because they have *not* decided to register. A full-screen layout matching the auth screens, with
the brand mark and language switcher only, is the honest presentation. This also sidesteps the
mobile bottom-nav overlap entirely (see R3).

**Alternatives rejected**:
- *Backend-served content* — no benefit; the content never varies by user or by request.
- *Routes inside the shell* — inherits a sign-up-oriented public bar and the fixed bottom nav.

---

## R2 — Prose lives in lazily-fetched per-language content files, not the main catalog

> **Amended during implementation (2026-07-31).** The original decision was a lazy Transloco
> *scope*. The files, their location, their laziness, and the language-switch behaviour are all
> unchanged; only the **loading mechanism** differs — the documents are fetched by a small
> `LegalContentService` rather than registered as a scope. See R2a below for why. Everything else
> in this section still holds.

**Decision**: The legal text lives in `frontend/apps/web/public/i18n/legal/{en,de,es}.json`,
fetched on activation of the two lazy routes.

**Rationale**: The existing loader already documents and supports exactly this
(`frontend/apps/web/src/app/core/i18n/transloco-http.loader.ts:10-11`: "a scoped path (`auth/en`)
… maps 1:1 onto the folder layout"), so this is the project's own mechanism, not a new one. The
main catalogs (`public/i18n/*.json`, 18 top-level sections) are fetched on **every** app load; a
full privacy policy is several thousand words per language and has no business being in the
critical path for a page nobody is currently reading. A scope is fetched only when the route is
visited.

It also keeps the legal text in three files that can be reviewed as documents — which matters when
the German version is legally authoritative and a reviewer needs to read it as prose rather than as
diffs scattered through a 2000-key catalog.

**⚠ Gotcha — the English fallback is actively dangerous here.** `app.config.ts` sets
`missingHandler: { useFallbackTranslation: true }` with `fallbackLang: 'en'`. For interface labels
that is exactly right (031 FR-008/FR-018: never render a blank or a raw key). For a
German-authoritative legal document it means **a missing German paragraph silently renders in
English inside the German text, with no visible signal** — producing a document that looks
complete, is legally authoritative, and is partly in the wrong language. Mitigation is a **build- or
test-time completeness check** asserting the three legal catalogs have identical key sets (see R7);
the runtime fallback is left alone, because changing it globally would break 031's guarantee for the
rest of the app.

**Alternatives rejected**:
- *Prose in the main catalog* — bloats the always-loaded payload; buries reviewable legal text.
- *Per-language Angular components or Markdown files* — bypasses Transloco, so the language
  switcher would not drive it, and the switcher is already a shipped, tested mechanism.
- *Markdown rendered at runtime* — pulls in a renderer dependency and an `innerHTML` sink for
  content that is structured enough to express as keys. Not worth the CSP/XSS conversation.

**Shape**: the catalog is structured (`legal.privacy.sections.analytics.body`), and paragraphs are
arrays of keys rather than one key containing embedded markup, so no HTML is interpolated and no
`[innerHTML]` binding is introduced.

---

## R2a — Why the scope became a direct fetch (implementation amendment)

**Decision**: a small route-provided `LegalContentService` fetches `/i18n/legal/{lang}.json` through
the existing `HttpClient`, keyed off Transloco's `langChanges$`. The `legal` Transloco **scope is
not registered**. Short footer/nav labels (`legal.privacy`, `legal.imprint`, …) stay in the main
Transloco catalogs, because the footer renders on every screen and cannot wait for a lazy fetch.

**Why the change**: two things surfaced once the page was built, and the first is decisive.

1. **Transloco has no error surface — and its fallback would produce exactly the failure this
   feature exists to prevent.** A failed scope load does not raise; it leaves the keys unresolved,
   and `useFallbackTranslation: true` then renders the **English** text in their place. On the
   German page that means a failed load silently produces an English document presented as the
   legally authoritative German one. Contract PC-7 requires a *visible error* instead, and there is
   no way to satisfy it while the load is Transloco's. Fetching the document directly turns a
   failed load into `failed()` → a rendered error state.
   The irony is the point: the same fallback that R2 already flagged as the feature's worst hazard
   is what makes the scope mechanism unusable for its own error path.
2. **Legal prose is content, not labels.** Transloco flattens catalogs on load and unflattens on
   `translateObject`; paragraph arrays survive that round trip, but the page needs the document
   shape intact and typed, and there is no reason to route it through two transformations.

**What did not change**: the files, their location and naming, the fact that they are fetched only
when a legal route activates, that the prose stays out of the always-loaded main catalogs, that the
language switcher drives the document, and every guard test in R8 — those check the JSON files, not
the loading mechanism, so they were unaffected.

**Cost accepted**: one small service instead of a framework feature, and the `legal` files are no
longer reachable via the `| transloco` pipe. Neither matters — nothing outside the two legal pages
reads them.

---

## R3 — Reachability: one shared link component, three placements

**Decision**: A single `jh-legal-links` shared component rendering the privacy, imprint and
(existing) language controls, mounted in **three** places:

| State | Placement | Clicks to reach |
|---|---|---|
| Signed out, anywhere in the shell (`/u/:handle`, landing) | new `jh-app-footer` at the end of the shell's flow column | 1 |
| Signed in, desktop **and** mobile | the same `jh-app-footer` — the shell's `<main>` already reserves `pb-[76px]` for the fixed bottom bar, so the footer sits above it and is never occluded | 1 |
| Auth / onboarding / invite-accept screens (outside the shell) | `jh-legal-links` inline at the bottom of the card column | 1 |

**Rationale**: FR-002 requires ≤2 clicks from *any* screen. The shell covers most of the app in one
placement. The screens outside it are exactly the ones a not-yet-registered visitor sees, which is
where a privacy link matters most — `sign-in`, `register`, `forgot-password`, `reset-password`,
`verify-email`, `onboarding`, and the three invite-accept routes.

**Scrolling is not a click.** A footer at the end of a long page is still one click; the two-click
budget is not consumed by reaching it. Verified against the shell markup: `<main class="min-w-0
flex-1 pb-[76px] md:pb-0">` inside `flex min-h-screen flex-col`, so a footer placed after `<main>`
inside that column lands below the content and above the fixed bottom bar on mobile.

**No new bottom-nav entry.** The mobile bottom bar is the primary navigation for five destinations;
adding a legal entry would dilute it for a page visited rarely. The footer satisfies the requirement
without touching it.

**Alternatives rejected**:
- *Avatar-menu entry only* — invisible to signed-out visitors, who are the primary audience.
- *Footer only, no auth-screen links* — leaves the registration screen, where an email address is
  actually handed over, with no privacy link. That is the one screen where its absence is worst.

---

## R4 — The imprint particulars are **committed**, not injected

**Decision**: The operator's particulars live in the committed `legal` catalogs, exactly like the
rest of the legal text. They are **not** injected at runtime from GitHub Environments.

**Rationale**: This was examined seriously, because the repository is public and git history cannot
be retracted. The 033 feature established a real runtime-injection mechanism —
`frontend/nginx.conf.template` renders `${JH_...}` placeholders via the nginx image's own
`envsubst` at container start — so the capability exists. It was still rejected:

1. **It does not solve the stated problem.** §5 DDG requires the particulars to be *published on the
   live site*. Whatever address is chosen is public, crawled, and archived by third parties within
   days. Keeping it out of git protects nothing that is not already public by legal mandate.
2. **It would make the legally-required content unreviewable.** The imprint would render empty in
   local and (unless separately configured) Dev, so nobody would see the actual published text
   before Prod — for the one page whose text is legally prescribed.
3. **It fragments a document across two mechanisms.** Half the imprint in a translated catalog, half
   substituted by nginx, with the substitution point invisible in the Angular source.
4. **The real mitigation is choosing the address**, not hiding the file. A German operator who does
   not want a home address published uses a business or service address (`c/o`), which is the
   established practice and is compatible with §5. That is an owner decision about *which* address,
   not an engineering decision about *where the string lives*.

**Consequence recorded for the owner**: the chosen address enters public git history permanently.
Choose it accordingly, before the first commit that contains it — the plan therefore keeps the
particulars in a single clearly-marked block so the decision is made once, deliberately.

**Resolved 2026-08-01 — and the objection was moot.** The owner supplied Jan Niklas Rösch,
Lattenkamp 12, 22299 Hamburg, Germany, `hello@juggerhub.com`, and pointed out that the address is
**already committed to this repository**: verified in `backend/EmailTemplates/{en,de,es}/footer.html`,
where it is the postal address in every transactional email footer. So the one substantive cost of
the committed-content decision — a permanent public git-history entry — did not exist; the entry
was already there.

Worth stating plainly rather than quietly dropping: the reasoning above was sound but the premise
was unchecked. **Before designing around the cost of publishing a datum, check whether the
repository already publishes it.** A single grep would have found this at plan time.

---

## R5 — Legal basis, disclosed as behaviour that was verified

**Decision**: Legitimate interest, no consent banner, DNT/GPC as the objection route (spec
Clarifications 2026-07-31). The policy text states the balancing test explicitly.

**Verification the policy text depends on** (each is an assertion the policy makes about the running
system, and each must hold or the policy is false):

| Policy claim | Source of truth | Status |
|---|---|---|
| Self-hosted; nothing leaves for a third-party analytics service | `frontend/nginx.conf.template` proxies to an in-cluster upstream only | verified |
| No cookie, no device storage from analytics | 033 FR-006 + `data-exclude-search` tracker config | verified in 033 |
| No viewer identifier stored | 033 FR-005, T037 | verified in 033 |
| Page paths recorded verbatim, including `/u/<handle>` and `/t/<slug>` | 033 FR-008 | verified — **and is the disclosure** |
| Query strings are **not** recorded | 033 FR-008a (`data-exclude-search`, dropped client-side) | verified in 033 |
| DNT/GPC suppresses recording entirely | 033 FR-007 | **must be re-verified in this feature** (SC-004) |

The last row is the one this feature cannot take on trust: the policy will describe DNT as a working
opt-out, so the plan schedules an end-to-end check rather than citing 033's.

**Note on FR-008a**: 033 already stopped recording query strings after discovering `/sign-in` was
logging its `returnUrl` (which carries deep links). The policy states the path/query distinction,
because a reader who knows paths are recorded will reasonably wonder about the query.

---

## R6 — Processor and cookie inventory, derived from the code

**Processors** (FR-008), verified rather than recalled:

| Processor | What it receives | Where | Evidence |
|---|---|---|---|
| **Resend** | Recipient email address + message content of transactional email, Dev/Prod only | US-headquartered; transfer basis is the owner's to state | constitution Technology Stack; `backend/Services/Email/` |
| **Microsoft Azure** | All application data at rest and in transit — AKS, in-cluster PostgreSQL, Blob Storage for media (035) | region set by `infra/` tfvars — **the plan must read the actual configured region, not assume one** | constitution Principle V; `specs/035-media-storage-abstraction/plan.md` |
| *(none for geocoding)* | — | — | no Photon/geocoder service in compose or infra; `backend/Services/Geocoding/CityService.cs` reads seeded `City` rows |
| *(none for analytics)* | self-hosted, 033 FR-009 | in-cluster | `nginx.conf.template` |
| *(none for fonts)* | Mona Sans / Hubot Sans ship via `@fontsource` | bundled | DESIGN.md Typography — **no Google Fonts request**, which is worth stating in the policy because it is a common and well-litigated German exposure the platform does *not* have |

**Device storage** (FR-011), verified:

| What | Purpose | Consent status |
|---|---|---|
| httpOnly auth/refresh cookie | signing in and staying signed in | strictly necessary — exempt |
| Locally stored language choice (anonymous visitors, 031) | remembers the chosen language | strictly necessary for a service the user requested — exempt |
| *(analytics)* | — | writes nothing |

**Data categories** (FR-004): the spec's list was compiled from `backend/Entities/` (52 entity
files) and is complete against it. Two details worth naming precisely in the policy because they are
non-obvious to a reader and verifiable in the code:
- `RefreshToken.CreatedByIp` — an originating IP address is retained per session for security
  auditing. This is personal data and is currently undisclosed.
- Chat archival snapshots (019/027) — conversations are *snapshotted*, not deleted, when a team is
  deleted or an event cancelled. A reader who assumes "team gone ⇒ messages gone" would be wrong.

**Retention** — the honest position (spec Assumptions): **no automated retention or deletion runs
anywhere in the platform today.** No scheduled job, no TTL, no purge. The policy states data is kept
until the account is deleted on request. Writing a period the system does not enforce would be a
false statement in a legal document; the correct response is a retention issue, not better wording.

---

## R7 — DESIGN.md long-form content treatment (new section)

**Decision**: Add a **"Long-form content"** section to DESIGN.md, and build the pages on existing
tokens only — no new token, no new width, no new type step.

| Aspect | Treatment | Why this and not something new |
|---|---|---|
| Measure | `jh-page-container width="sm"` → `max-w-container-sm` = **640px** | ≈70–75 characters at `body-md`. The existing `sm` step is already the right measure; `md` (860px) is too wide for sustained reading |
| Body | `body-md` (16px, line-height 1.5) with paragraph rhythm from the 4px spacing scale | DESIGN.md Typography: body is 16px, nothing below 12px |
| Headings | `h1` for the document title, `h2` per section, `h3` per subsection — Hubot Sans display face, unchanged | gives the screen-reader navigation FR-018 requires; no new hierarchy invented |
| Section rhythm | existing `section-gap`, not arbitrary margins | DESIGN.md Layout |
| Links in prose | `text-link` / `hover:text-link-hover`, underlined **in prose only** (unlike navigation links elsewhere) | in a wall of text, colour alone is a weak affordance and fails for colour-blind readers |
| Lists | `disc` / `decimal`, indented on the spacing scale, same body step | |
| "Last updated" + authoritative-language notice | `caption` step, `text-subtle`, directly under the `h1` | present but not competing with the content |
| Table of contents | anchored `h2` links at the top of the privacy policy | serves the deep-link edge case in the spec |

**Known conflict, not resolved here**: DESIGN.md demands ≥4.5:1 contrast while specifying primary
buttons as white-on-`coral-4` (3.14:1) — the open app-wide issue. **This feature is unaffected**: it
ships no primary button. Recorded so the UI review does not re-litigate it.

---

## R8 — Verification, and the guard against shipping a placeholder

**Unit (Jest)**: the two page components render, the language switch swaps the catalog, the
non-German versions render the authoritative-language notice.

**Catalog completeness test** — the mitigation for R2's fallback hazard: a test asserting
`legal/en.json`, `legal/de.json` and `legal/es.json` have **identical key sets**. Fails the build on
a missing paragraph rather than letting English silently appear inside the German document.

**Placeholder guard** — the mitigation for R4's open dependency: the same test asserts no value in
any legal catalog contains the placeholder sentinel. Until Q1 is answered the test fails, which is
the correct state: it makes "we shipped a TODO into the legally-required imprint" impossible rather
than merely unlikely. Answering Q1 turns the build green.

**E2E (Playwright)** — extends `frontend/apps/web-e2e/src/authenticated-only.spec.ts`, which today
proves gated paths *redirect*. It gains the inverse assertion for `/privacy` and `/imprint`: with
cookies cleared, the page renders, the URL does not change, and no `returnUrl` redirect occurs
(SC-008). Plus a two-click reachability check from a signed-out screen and from a signed-in one at
both viewports (SC-001), and a mobile-width check for no horizontal overflow at 320px (SC-007).

**DNT opt-out check** (SC-004): a manual quickstart step against a local stack with analytics on.
Playwright cannot set the browser's DNT signal reliably across engines, so this is documented rather
than automated — stated plainly instead of being quietly dropped.

**Not automated**: the accuracy of the legal *content* against the running system (SC-002, SC-003,
SC-005). That is a review checklist item against `backend/Entities/` and the 033 spec, not a test.
