# Phase 0 Research: Shared UI primitives layer

All spec clarifications were resolved in `/speckit-clarify` (see spec §Clarifications).
This document records the remaining design decisions the plan depends on.

## R1 — Button as an attribute directive, not a wrapper component

- **Decision**: Deliver the button primitive as an **attribute directive**
  `jhButton` applied to native `<button>` / `<a>` elements, with inputs
  `variant` (`primary` | `secondary` | `danger` | `ghost`), `size` (`md` | `sm`),
  and `full` (boolean). It sets host classes only.
- **Rationale**: Native `<button>`/`<a>` keep their semantics for free —
  `type`, `disabled`, form submission, `routerLink`, focus order, and
  `aria-*`/`data-testid` already present on the element. Migration is minimal and
  behavior-preserving (FR-010): add `jhButton variant="…"` and delete the ad-hoc
  utility soup. A wrapper component would force re-plumbing `disabled`, click
  outputs, and routerLink, risking behavior drift.
- **44px rule**: `md` (default) applies `min-h-11` (44px). `sm` exists only for
  genuinely dense/inline controls and still meets tap guidance via adequate
  padding; it is the documented exception, not a second default.
- **Encoded once**: `rounded-md`, coral brand bg + `text-on-accent`,
  `hover:bg-brand-hover hover:shadow-coral`, `active:translate-y-px`, and a
  visible coral focus ring (`focus-visible:outline-none focus-visible:ring-2
  focus-visible:ring-focus` or the DESIGN.md `focus-ring` shadow). `danger`
  covers remove/revoke/ban/cancel-session; `ghost` covers today's text-only
  actions (e.g. event-manage "Remove").
- **Alternatives**: (a) Tailwind `@layer components` `.btn` classes — rejected in
  clarify (still copy-pasteable, no typed variants). (b) Wrapper `<jh-button>` —
  rejected (semantic/behavior re-plumbing risk).

## R2 — Card, empty-state, loading, alert, page as components

- **Decision**: `jh-card`, `jh-empty-state`, `jh-loading`, `jh-alert`,
  `jh-page-container` are standalone components using content projection, mirroring
  `jh-browse-shell`.
- **Rationale**: These carry structure (a card's optional gradient strip, an
  empty-state's message + action slot, an alert's box + `role="alert"`), so a
  component that owns the wrapper markup is cleaner than a directive and removes
  the most-copied blocks. Loading is a component even though trivial, so the
  single text-line treatment (size/color/spacing/copy) lives in exactly one place.

## R3 — Alert / error consolidation

- **Decision**: One `jh-alert` component, `tone` input (`danger` default,
  `success` | `warning` | `info`), always renders the boxed treatment
  (`rounded-md border bg-*-bg border-*-border text-*-fg px-md py-sm text-body-sm`)
  and sets `role="alert"`. Form/page errors use `tone="danger"`.
- **Canonical error color**: `danger-fg` (red-6) for text on `danger-bg` — the
  legacy bare `text-danger` (red-5) usage is retired during migration (resolves
  audit finding 2's "two reds"). `role="alert"` is now guaranteed by the
  primitive (fixes inconsistent application).
- **Inline vs boxed**: a single boxed treatment covers both today's "boxed" and
  "bare text" error sites; bare danger text is not reintroduced. Truly inline
  per-field validation hints (rare here) may use a documented compact modifier,
  still `danger-fg`.

## R4 — Loading treatment (clarified: standardized text line)

- **Decision**: `jh-loading` renders one text line: `text-body-sm text-muted`,
  default label `Loading…`, `align` input (`left` default | `center`), consistent
  top margin owned by the component (screens stop hand-setting `mt-md`/`mt-lg`).
  Contextual labels stay allowed (`label="Loading your profile…"`).
- **Skeletons**: not the standard. The dashboard's existing `animate-pulse`
  skeleton is kept as an intentional, documented per-screen exception (spec
  FR-005), not folded into the primitive.
- **Copy**: `text-muted` (sand-6) is the canonical muted tone, replacing the
  `text-subtle`/`text-faint` scatter for status text.

## R5 — Empty-state treatment & voice

- **Decision**: `jh-empty-state` renders one container — a centered bordered card
  (`rounded-lg border border-border-muted bg-surface-card p-lg text-center`) — with
  an optional `heading` input, a projected default message slot, and an optional
  `[action]` slot for a next-step control (typically a `jhButton`). Muted text via
  `text-muted`.
- **Voice**: messages are warm and sentence-cased; where a relevant next step
  exists, screens pass an `[action]` (DESIGN.md "offer a next step"). SC-004 is
  satisfied per-screen; whether a next step is "relevant" stays an authoring
  judgment (spec's low-impact Outstanding item), guided by DESIGN.md.
- **Container variants**: the sunken-box and dashed-border variants seen today are
  consolidated into this one treatment unless a list needs an inline empty row
  (e.g. `@empty` inside a bordered list) — for those, a compact `inline` modifier
  reuses the same tokens without the outer card.

## R6 — Page container width taxonomy (resolves deferred item)

- **Decision**: `jh-page-container` with `width` input mapping page **type** →
  DESIGN.md container token:

  | Page type | Width | Token | Examples |
  |-----------|-------|-------|----------|
  | Focused single-column / forms | `sm` | 640px | auth, onboarding, create/edit, invite-accept, chat-new, party-create |
  | Standard content (detail, list, settings, feed) | `md` | 860px | team-detail, settings, alerts, inbox, party-manage/news, see-all lists |
  | Dense / dashboard / admin | `lg` | 1100px | dashboard, admin shell + pages |
  | Two-pane chat shell | `xl` | 1320px | chat shell |

- **Rationale**: DESIGN.md says content scales up to `container-lg` (1100px) as the
  main column; forms read best narrower; dashboards/admin are multi-column; the
  chat two-pane needs the widest. This codifies current *good* usage and removes
  the arbitrary `md`/`lg`/`xl` scatter (audit finding 5). Component also owns the
  horizontal page padding + centering so screens stop re-declaring it.

## R7 — Icons: keep inline SVG, add a minimal `jh-icon`; kill the "+" glyph

- **Decision**: No icon-library dependency (matches current inline-SVG pattern and
  the constitution's dependency discipline). Add a small `jh-icon` component with a
  **curated** map of the Lucide glyphs the app actually uses (DESIGN.md's common
  set: `compass, users, calendar-days, search, bell, plus, map-pin, trophy, swords,
  user-plus, arrow-right, check, sparkles`, plus any found in migration), rendering
  a 2px-stroke `currentColor` SVG at a token size (16–22px). Inputs: `name`, `size`.
- **Required fix**: the literal `+` used as an icon (trainings-tab "+ New training")
  becomes `<jh-icon name="plus" />` + label (FR-012, SC-007).
- **Rationale**: centralizes stroke/size/`aria-hidden` and prevents future text-glyph
  icons, without adding a runtime dep or hand-copying SVG per site.
- **Alternative**: `lucide-angular` package — rejected (new dependency for glyphs we
  already inline; heavier than needed).

## R8 — `text-white` vs `text-on-accent`, and token hygiene

- **Decision**: On-brand label text is always `text-on-accent`; raw `text-white`
  on brand surfaces is retired during migration (audit finding 1). Primitives never
  hard-code hex or raw palette steps — only semantic tokens (FR-008).

## R9 — Migration order (phased PRs, behavior-preserving)

- **Decision**: Ship primitives first (no screen change), then migrate by feature
  area, each phase its own PR gated by `nx test web` + Playwright e2e + the
  `checklists/ui-review.md` pass:

  1. **P-A** Primitives + specs + demo/validation harness (no screen changes).
  2. **P-B** Auth + onboarding.
  3. **P-C** Browse + profile.
  4. **P-D** Teams + my-team.
  5. **P-E** Events + parties + marketplace.
  6. **P-F** Trainings.
  7. **P-G** Chat.
  8. **P-H** Admin (catalogue/users/teams/overview/detail/shell/assign-picker).
  9. **P-I** Dashboard + alerts + settings + layout (nav/shell) + terminology sweep
     ("invite") + final audit verification (SC-001…SC-009).

- **Rationale**: Smallest safe increments; each area is independently testable and
  reversible. Terminology and final 100%-coverage verification land last, once all
  screens use primitives.

## R10 — Verification of "no drift" (how SCs become testable)

- **Decision**: Add primitive unit specs (variant → class/host assertions, 44px,
  focus ring, `role="alert"`). For app-wide SCs, use a lightweight lint/grep guard
  in the final phase: assert zero occurrences of the retired patterns
  (raw `text-white` on buttons, bare `text-danger` error text, `rounded-pill` on
  brand action buttons, literal `+ ` icon text, `text-faint`/`text-subtle` for
  status) outside the primitives themselves. This makes SC-001…SC-007 checkable in
  CI and keeps re-drift from silently returning (FR-013).
- **Alternative**: full visual-regression snapshots — deferred (heavier; existing
  e2e + the grep guard cover behavior + drift for now).
