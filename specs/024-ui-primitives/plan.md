# Implementation Plan: Shared UI primitives layer

**Branch**: `024-ui-primitives` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-ui-primitives/spec.md`

## Summary

Introduce a small set of shared, DESIGN.md-conformant UI primitives — button,
card, empty-state, loading, alert/error, page container, and a minimal icon — as
Angular standalone components/directives under `frontend/apps/web/src/app/shared/ui/`,
then migrate every screen onto them so the audited visual drift is removed and
cannot be reintroduced by copy-paste. The primitives encode the 44px touch
target, coral hover glow, 1px press nudge, always-visible coral focus ring,
canonical tokens, and warm sentence-cased voice once, in one place. This is a
behavior-preserving frontend refactor: no backend, API, schema, auth, or
permission changes. Delivery is full app-wide migration in phased PRs by feature
area (clarified). The known primary-button contrast conflict is out of scope.

## Technical Context

**Language/Version**: TypeScript 5.x, Angular 21.2 (standalone, **zoneless** — no
`zone.js`, so no `fakeAsync`/`tick` in specs), HTML, CSS.

**Primary Dependencies**: Angular 21, Tailwind CSS 3.4 (tokens mapped from
`styles.css` ← DESIGN.md), `@fontsource` (Mona Sans / Hubot Sans). **No icon
library** — Lucide icons are hand-inlined as SVG; this feature keeps that pattern
(inline SVG), adding no runtime dependency.

**Storage**: N/A (presentational; no persisted data, no entities).

**Testing**: Jest (`jest-preset-angular`) for component specs via `nx test web`;
Playwright (`apps/web-e2e`) for e2e. Existing specs/e2e are the behavior-preservation
guardrail.

**Target Platform**: Modern evergreen browsers, mobile-first (≥360px).

**Project Type**: Web application — this feature is **frontend-only**
(`frontend/apps/web`).

**Performance Goals**: No runtime perf targets. Visual states are CSS-only; motion
durations/eases come from DESIGN.md tokens (120/200/320ms).

**Constraints**: DESIGN.md is the single source of truth for tokens/voice/component
specs; new visual values require a DESIGN.md token first (FR-008). Each Angular
piece keeps separate `.html`/`.css`/`.ts` (constitution Principle VI). Controls
≥44px tall; always-visible coral focus ring (WCAG AA); migration behavior-preserving
(labels' meaning, `data-testid`, routing, disabled/busy logic unchanged).

**Scale/Scope**: ~55 component templates across ~20 feature areas migrate onto the
primitives. 7 primitives introduced.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Relevance | Status |
|------------------|-----------|--------|
| I. Security-first, never trust the client | Presentational only; no authZ/validation moves to or from the client; client checks unchanged; no secrets/exceptions surfaced | ✅ Pass (N/A) |
| II. Thin controllers / service-centric backend | No backend change | ✅ Pass (N/A) |
| III. Disciplined data access (EF/Postgres) | No data access | ✅ Pass (N/A) |
| IV. Secure auth & session | Auth screens are restyled only; no flow/token/policy change | ✅ Pass |
| V. Environment parity & containerized deploy | No infra/build-target change | ✅ Pass (N/A) |
| VI. Consistent conventions & tooling | Primitives are Angular standalone with **separate .html/.css/.ts**; only `.ps1` scripts (none added) | ✅ Pass |
| Quality Gate 7 — UI/Design compliance | This feature *enforces* DESIGN.md; instantiate `checklists/ui-review.md` and run it per migration phase; DESIGN.md wins conflicts | ✅ Pass (central to feature) |

**Result**: No violations. Complexity Tracking not required.

**Note on the standing contrast conflict**: DESIGN.md specs primary buttons as
white-on-`coral-4` (≈3.14:1, below the AA 4.5:1 it also mandates). Per the spec
this is out of scope (FR-014); the button primitive encodes today's documented
tokens so the decision remains a single-source, owner-level edit. This is a
pre-existing DESIGN.md self-conflict, not one introduced here — reported, not
resolved.

## Project Structure

### Documentation (this feature)

```text
specs/024-ui-primitives/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output — primitive/variant inventory
├── quickstart.md        # Phase 1 output — validation guide
├── contracts/
│   └── ui-primitives.md # Phase 1 output — component/directive API contracts
├── checklists/
│   ├── requirements.md  # Spec quality (from /speckit-specify)
│   └── ui-review.md      # DESIGN.md UI review (instantiated in Phase 1)
└── tasks.md             # /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
frontend/apps/web/src/
├── styles.css                       # tokens (DESIGN.md) — unchanged; may gain a
│                                     #   tiny keyframe/util only if a token demands
├── app/
│   ├── shared/
│   │   └── ui/                       # NEW — the primitives live here
│   │       ├── button/              # jhButton directive (variant/size/full)
│   │       │   ├── button.directive.ts
│   │       │   └── button.directive.spec.ts
│   │       ├── card/                # jh-card component (+ accent strip)
│   │       │   ├── card.component.{ts,html,css,spec.ts}
│   │       ├── empty-state/         # jh-empty-state (message + [action] slot)
│   │       │   ├── empty-state.component.{ts,html,css,spec.ts}
│   │       ├── loading/             # jh-loading (standardized text line)
│   │       │   ├── loading.component.{ts,html,css,spec.ts}
│   │       ├── alert/               # jh-alert (danger/success/warning/info, role=alert)
│   │       │   ├── alert.component.{ts,html,css,spec.ts}
│   │       ├── page/                # jh-page-container (width taxonomy)
│   │       │   ├── page-container.component.{ts,html,css,spec.ts}
│   │       └── icon/                # jh-icon (curated inline Lucide set)
│   │           ├── icon.component.{ts,html,css,spec.ts}
│   │           └── icons.ts         # name → SVG path data (curated)
│   ├── features/…                    # migrated screen-by-screen, by feature area
│   └── layout/…                      # top-nav/bottom-nav/shell adopt primitives
└── ...
frontend/apps/web-e2e/               # existing e2e — must stay green through migration
```

**Structure Decision**: Frontend-only. Primitives are added under a new
`app/shared/ui/` folder (today `shared/` holds only `pompfen.catalog.ts`),
following the existing `jh-`-prefixed standalone-component convention proven by
`jh-browse-shell` (typed `input()`/`output()`, content projection, separate
template/style files). No new top-level projects or backend paths.

## Complexity Tracking

> No constitution violations — section intentionally empty.
