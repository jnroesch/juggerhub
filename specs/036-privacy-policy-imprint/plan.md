# Implementation Plan: Privacy Policy & Imprint

**Branch**: `036-privacy-policy-imprint` | **Date**: 2026-07-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/036-privacy-policy-imprint/spec.md` · GitHub issue [#92](https://github.com/jnroesch/juggerhub/issues/92)

## Summary

Ship two publicly reachable content pages — a complete privacy policy and a German imprint — plus
the navigation that makes them reachable from anywhere, in all three supported languages, with
German authoritative.

**This is a frontend-only feature. No backend code, no endpoint, no entity, no migration.** The
content is static translated text; adding a server surface for it would mean punching an
`[AllowAnonymous]` hole in the 026 fallback policy for data with no server-side dependency. The 026
boundary is therefore untouched, and the OpenAPI/`AuthController` allowlist gotcha that features
021/026 record does not apply here — that gotcha is about endpoints, and this feature adds none.

Four things get built: two lazy, unguarded routes outside the shell; a `legal` Transloco scope
carrying the prose in `en`/`de`/`es`; a shared `jh-legal-links` component mounted in a new app
footer and inline on every screen that lives outside the shell; and a **Long-form content** section
added to DESIGN.md, which has none.

The feature is remedial rather than preventative: 033 analytics is merged and deployed to Dev and
Prod (`b38cee4`, `47288e6`), and it records page paths verbatim (033 FR-008), so paths naming a
member profile or team are already being stored with no disclosure anywhere in the product.

## Technical Context

**Language/Version**: TypeScript 5.x, Angular 21 (zoneless), Node 22 · **no .NET change**

**Primary Dependencies**: `@jsverse/transloco` 8.x (existing), Tailwind CSS (existing), Angular
Router. **No new runtime dependency is introduced** — in particular no Markdown renderer.

**Storage**: None. This feature persists nothing and reads nothing from the database.

**Testing**: Jest (`frontend/apps/web`) for component + catalog tests; Playwright
(`frontend/apps/web-e2e`) for the signed-out reachability and responsive assertions.

**Target Platform**: Browser (mobile-first, 320px up), served by the existing nginx container.

**Project Type**: Web application — frontend only for this feature.

**Performance Goals**: The legal catalogs must not enter the initial payload. They are a lazy
Transloco scope, fetched only when a legal route is visited (research R2). Initial-bundle size is
unchanged for every other route.

**Constraints**:
- Both routes render for a visitor with **no session and no auth request issued** (SC-008).
- ≤2 clicks from any screen, signed in or out, desktop or mobile (FR-002).
- No horizontal scroll at 320px; screen-reader-navigable heading structure (FR-018, SC-007).
- German authoritative; `en`/`es` carry a visible divergence notice (FR-019).

**Scale/Scope**: Two routes, one footer, one shared link component, three catalog files, one
DESIGN.md section, ~9 screens gaining an inline link.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Constitution v1.4.0.*

| Principle / Gate | Verdict | Notes |
|---|---|---|
| **I — Security-first, never trust the client** | ✅ PASS | No new server surface, so no new authorization decision to get wrong. No user input is accepted; no `innerHTML` sink is introduced (research R2 — prose is structured keys, not embedded markup). The 026 server-side `FallbackPolicy` is unchanged. |
| **II — Thin controllers, service-centric backend** | ✅ N/A | No backend code. |
| **III — Disciplined data access** | ✅ N/A | No entity, no query, no migration. |
| **IV — Secure auth & session** | ✅ PASS | Untouched. The pages must **not** trigger an auth request; the plan asserts this (SC-008) rather than assuming it. |
| **V — Environment parity** | ✅ PASS | Identical in local/Dev/Prod. Research R4 explicitly **rejected** per-environment injection of the imprint particulars, which would have made the legally-required content differ by environment. |
| **VI — Conventions & tooling** | ✅ PASS | Separate `.html`/`.css`/`.ts` per component; no `.sh` added. |
| **VII — Resilient by default** | ✅ PASS with one note | The catalog fetch is the only network call. It goes through the existing `HttpClient` with 028's `retryInterceptor` already registered — a GET, so retry-safe, and inheriting shared infrastructure rather than hand-rolling. **Note**: if the scope fetch fails, the page must degrade to a visible error, never to a blank legal page. Covered in the contract. |
| **Gate 7 — UI/design compliance** | ⚠️ ACTION | DESIGN.md has **no long-form content treatment**; FR-017 requires defining one. This is an addition to DESIGN.md, not a deviation from it — research R7 builds it entirely from existing tokens (no new width, type step, or colour). A `checklists/ui-review.md` is instantiated from the template. |
| **Gate 8 — Resilience** | ✅ PASS | No new outbound integration. |

**No violations. Complexity Tracking is empty.**

One pre-existing, unrelated conflict is noted so the UI review does not re-open it: DESIGN.md's
≥4.5:1 contrast rule versus its white-on-`coral-4` primary button (3.14:1) is an open app-wide
issue. This feature ships no primary button and is unaffected.

## Project Structure

### Documentation (this feature)

```text
specs/036-privacy-policy-imprint/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — R1..R8 decisions
├── data-model.md        # Phase 1 — deliberately empty of entities; content shape instead
├── quickstart.md        # Phase 1 — how to run and validate
├── contracts/
│   ├── routes.md        # Route + reachability contract
│   └── content-catalog.md  # legal scope key structure + completeness rules
├── checklists/
│   ├── requirements.md  # spec quality (exists)
│   └── ui-review.md     # instantiated at implementation time (Gate 7)
└── tasks.md             # Phase 2 — /speckit-tasks, NOT created here
```

### Source Code (repository root)

```text
frontend/apps/web/
├── public/i18n/
│   ├── en.json                       # + `legal.*` nav/footer labels only (short)
│   ├── de.json                       # + same
│   ├── es.json                       # + same
│   └── legal/                        # NEW — lazy Transloco scope, the prose itself
│       ├── en.json
│       ├── de.json                   # AUTHORITATIVE version
│       └── es.json
└── src/app/
    ├── app.routes.ts                 # + /privacy, /imprint (unguarded, lazy, outside shell)
    ├── features/legal/               # NEW
    │   ├── legal-page.component.{ts,html,css}      # shared long-form shell for both docs
    │   ├── privacy/privacy.component.{ts,html}
    │   └── imprint/imprint.component.{ts,html}
    ├── layout/
    │   ├── app-footer/               # NEW — jh-app-footer, mounted in the shell
    │   │   └── app-footer.component.{ts,html,css}
    │   └── shell/shell.component.html             # + <jh-app-footer />
    ├── shared/ui/legal-links/        # NEW — jh-legal-links, used by footer + off-shell screens
    │   └── legal-links.component.{ts,html,css}
    └── features/{auth/*,onboarding,teams/invite-accept,events/event-invite-accept,parties/party-invite-accept}/
                                       # + <jh-legal-links /> inline (9 screens, see contracts/routes.md)

frontend/apps/web-e2e/src/
└── authenticated-only.spec.ts        # + the inverse assertions for /privacy and /imprint

DESIGN.md                             # + "Long-form content" section (research R7)
```

**Structure Decision**: Frontend-only, following the existing Nx layout. Pages go under
`src/app/features/legal/` like every other feature area; the footer goes under `src/app/layout/`
beside `top-nav`/`bottom-nav`; the reusable link cluster goes under `src/app/shared/ui/` beside the
024 primitives, because it is mounted from both `layout/` and `features/` and belongs to neither.

## Phase 1 design decisions

The full reasoning is in [research.md](./research.md); the load-bearing outcomes:

1. **Routes** (R1): `/privacy`, `/imprint` — English paths matching every other route, lazy, no
   guard, **outside** the shell. Outside because the shell's anonymous public bar pushes sign-in and
   register, which is the wrong framing for a reader who has not decided to register — and because
   it sidesteps the fixed mobile bottom bar.
2. **Content** (R2, amended by R2a during implementation): per-language document files at
   `public/i18n/legal/{lang}.json`, fetched lazily when a legal route activates. Keeps several
   thousand words per language out of the always-loaded catalog and keeps the authoritative German
   text reviewable as a document. Originally planned as a Transloco *scope*; changed to a direct
   fetch because Transloco has no error surface — a failed scope load would have rendered the
   **English** text inside the legally authoritative German document, which is the exact failure
   this feature exists to prevent, and PC-7 requires a visible error instead. Files, location,
   laziness, language-switch behaviour and all guard tests are unchanged.
3. **Reachability** (R3): one `jh-legal-links` component in three placements — a new `jh-app-footer`
   inside the shell (covering signed-out and signed-in, desktop and mobile, in one place because
   `<main>` already reserves `pb-[76px]` for the bottom bar), and inline on the nine screens that
   render outside the shell. The registration screen is the single most important of these: it is
   where an email address is actually handed over.
4. **Imprint particulars** (R4): **committed**, not runtime-injected. The 033 nginx `envsubst`
   mechanism exists and could carry them, but it would protect nothing (the address is legally
   required to be published and is crawled within days), would make the legally-prescribed content
   unreviewable before Prod, and would split one document across two mechanisms. The real mitigation
   is choosing an address the owner is content to publish permanently — a `c/o` or business address
   is the established German practice. **The choice enters public git history irreversibly**, so it
   is made once, deliberately, in a single marked block.
5. **Legal basis** (R5): legitimate interest, no banner, DNT/GPC as the objection route. Every claim
   the policy makes about analytics is traced to a verified 033 requirement — except the DNT
   opt-out, which this feature re-verifies rather than citing, because the policy describes it as
   working.
6. **Inventory** (R6): processors are Resend (email, Dev/Prod) and Azure (everything at rest); there
   is deliberately no geocoding processor (030's Photon is not deployed), no third-party analytics,
   and **no Google Fonts** — the faces ship via `@fontsource`, which is worth stating since it is a
   well-litigated German exposure the platform does not have. Two non-obvious disclosures the code
   forces: `RefreshToken.CreatedByIp` retains a per-session IP address, and chat conversations are
   **snapshotted, not deleted**, when a team is deleted or an event cancelled.
7. **Retention** (R6): no automated retention or deletion runs anywhere in the platform today. The
   policy says so. Writing a period the system does not enforce would be a false statement in a
   legal document.
8. **Design** (R7): a new DESIGN.md **Long-form content** section built entirely from existing
   tokens — `container-sm` (640px ≈ 70–75 characters) for measure, `body-md` for prose, `h1`/`h2`/`h3`
   for a screen-reader-navigable hierarchy, and underlined in-prose links (a departure from
   navigation links elsewhere, because colour alone is a weak affordance in a wall of text).

## Risks & gotchas

| Risk | Why it bites | Mitigation |
|---|---|---|
| **Transloco's English fallback silently fills gaps in the German legal text** | `useFallbackTranslation: true` is correct for UI labels (031 FR-008) but for a German-*authoritative* document it means a missing paragraph renders in English with no visible signal — a document that looks complete, is legally binding, and is partly in the wrong language | A test asserting the three legal catalogs have **identical key sets**. Fails the build rather than shipping a mixed-language legal document. The global fallback is left alone — changing it would break 031 for the whole app |
| **A placeholder ships into the legally-required imprint** | Q1 (the operator's particulars) is still open; the structure can be built and reviewed without it, and "we'll fill it in before deploy" is exactly how it reaches Prod | The same test asserts no legal catalog value contains the placeholder sentinel. It **fails until Q1 is answered** — which is the correct state, not a broken build |
| **The policy asserts behaviour that isn't true** | It is a legal document making factual claims about a running system. A claim that drifts is a false statement, not a stale doc | Every claim is traced to a verified source in research R5/R6; the review checklist re-checks against `backend/Entities/` and the 033 spec; the DNT opt-out is re-verified end to end rather than cited |
| **A legal page triggers an auth request and redirects** | The whole point is reachability with no session. The 026 guard patterns are pervasive and easy to copy by reflex | Routes carry **no guard** and make **no** API call; the e2e spec asserts no redirect and no `returnUrl` |
| **The footer is occluded by the fixed mobile bottom bar** | It is `fixed` and 76px tall; a naively-placed footer disappears underneath it | Footer goes after `<main>` in the shell's `flex-col`; `<main>` already carries `pb-[76px] md:pb-0`. Asserted at 320px in e2e |
| **Legal prose bloats the always-loaded catalog** | The main catalogs are fetched on every app load; several thousand words × 3 languages is a real regression for a page almost nobody opens | Lazy scope (R2), fetched only on the legal routes |
| **The scope fetch fails and the page renders blank** | A blank privacy policy is worse than an error — it looks like a policy with nothing in it | The page must show a visible error state (Principle VII); covered in `contracts/routes.md` |

## Open dependency

**Q1 — the operator's imprint particulars** (spec Open Questions). Everything in this plan can be
built, reviewed, and merged without it: routes, footer, links, design treatment, the privacy policy
in all three languages, and the imprint's structure and translated labels. Only the imprint's
particulars block, and the placeholder-sentinel test keeps that block from reaching Prod
unnoticed.

## Complexity Tracking

No constitution violations. This section is intentionally empty.
