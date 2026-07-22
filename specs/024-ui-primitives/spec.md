# Feature Specification: Shared UI primitives layer

**Feature Branch**: `024-ui-primitives`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "Shared UI primitives layer to fix app-wide visual inconsistency. A consistency audit of the Angular frontend found that there is no shared component layer: every component re-assembles buttons, cards, empty states, loading states, and error/alert blocks from raw Tailwind utilities by hand, causing drift screen-to-screen. Introduce a shared, reusable set of UI primitives (button variants, card, empty-state, loading indicator, field/form error, page container) that bake in the DESIGN.md-mandated 44px height, coral hover glow, press nudge, always-visible focus ring, canonical tokens, and warm sentence-cased voice — then migrate existing templates onto them. DESIGN.md remains the single source of truth. Behavior-preserving refactor; the standing primary-button contrast conflict is out of scope."

## Overview

A UI consistency audit of the `frontend/apps/web` Angular app found that the same
UI concepts are rendered differently across screens because there is no shared
component layer — each screen hand-assembles buttons, cards, empty states,
loading states, and error blocks from raw Tailwind utilities. The result is
visible drift and repeated violations of [DESIGN.md](../../DESIGN.md), the
project's single source of truth for visual identity.

This feature introduces a small, reusable set of **UI primitives** that encode
the DESIGN.md rules once, then migrates existing screens onto them so the
inconsistencies are removed and can no longer be reintroduced by copy-paste. It
is a **behavior-preserving refactor** — no user-facing flows, data, or
permissions change; only the visual assembly is consolidated.

### Audit findings this feature resolves

1. **Primary buttons vary** in corner radius (`rounded-md` vs `rounded-pill`),
   padding/height (some compute to ~34px tall, below DESIGN.md's mandated 44px
   touch target), hover treatment (some carry the coral glow + press nudge, many
   omit them), on-accent text token (`text-on-accent` vs raw `text-white`), and
   focus rings (many action buttons have no visible focus ring).
2. **Error/alert display** uses two unrelated visual languages — a boxed danger
   block vs bare danger text — with two different reds (`text-danger-fg` red-6 vs
   the legacy `text-danger` red-5), and `role="alert"` applied inconsistently.
3. **Empty states** use at least four container treatments (bordered card,
   sunken box, dashed-border box, bare paragraph), rotate text color through
   `text-faint`/`text-subtle`/`text-muted`, and many are bare dead-ends that
   violate DESIGN.md's "offer a next step" voice guidance.
4. **Loading states** are mostly bespoke text lines (varying color, size,
   margin, and copy) with one skeleton; wording also drifts ("invite" vs
   "invitation", inconsistent article/verbosity).
5. **Page container width** has no rule — comparable pages use
   `max-w-container-md` vs `-lg` vs `-xl` arbitrarily.
6. **Icon vs text glyph** — a literal `+` character is used as an icon in at
   least one place instead of a Lucide icon.

## Clarifications

### Session 2026-07-22

- Q: How should the shared UI primitives be delivered? → A: Angular standalone
  components (plus attribute directives where a native `<button>`/`<a>` must be
  preserved) — matches the constitution's separate `.html/.css/.ts` rule, gives
  typed variant inputs, and is the strongest guardrail against re-drift.
- Q: What is the Definition of Done for migrating existing screens? → A: Full
  app-wide migration (every screen moves onto the primitives, honoring the 100%
  success criteria), delivered as phased PRs by feature area.
- Q: What should the canonical loading indicator be? → A: A single standardized
  text-line treatment (one size/color/spacing/copy pattern); a spinner is not
  introduced and skeletons are not the standard.
- Q: Which term is canonical for an invite object across teams, parties, and
  events? → A: "invite" (noun), used consistently everywhere.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consistent, accessible actions everywhere (Priority: P1)

As a JuggerHub visitor using any screen — sign-in, browse, a team page, an admin
table, a training form — I encounter buttons that look and behave the same:
same shape, same comfortable tap size, the same warm hover and press feedback,
and a clearly visible keyboard focus outline. A supporting or destructive action
reads as clearly secondary or dangerous, not like a second primary button.

**Why this priority**: Buttons are the most repeated interactive element and the
most divergent today, including accessibility failures (sub-44px targets, missing
focus rings). Standardizing them delivers the largest, most visible consistency
and accessibility win and is independently valuable even if nothing else ships.

**Independent Test**: Navigate the app by keyboard and pointer; every primary,
secondary, and destructive action shares one shape, one minimum height (≥44px),
one hover/press behavior, and shows a visible focus ring. Verifiable per-screen
without any other primitive existing.

**Acceptance Scenarios**:

1. **Given** any screen with a primary action, **When** it renders, **Then** the
   button is at least 44px tall, uses the coral brand background with the
   canonical on-accent text token, `md` corner radius, warms with the coral glow
   on hover, and nudges down 1px on press.
2. **Given** any interactive control, **When** I focus it with the keyboard,
   **Then** a coral focus ring is visible.
3. **Given** a destructive action (e.g. remove, revoke, ban, cancel session),
   **When** it renders, **Then** it uses a single consistent danger treatment
   distinct from primary and secondary actions.
4. **Given** any screen, **When** it renders, **Then** at most one primary
   (coral) action is present per view, per DESIGN.md.

---

### User Story 2 - Consistent status: empty, loading, and error states (Priority: P2)

As a visitor, when a list has nothing in it, is still loading, or fails to load,
the message looks and reads the same everywhere: the same container style, the
same muted text treatment, a consistent warm and encouraging tone, and — for
empty states — a next step to take where one exists.

**Why this priority**: These three states appear on nearly every list and detail
screen and are the second-largest source of drift, including voice violations
(bare dead-end empty states). Consolidating them makes the app feel coherent and
on-brand. Depends on nothing from Story 1.

**Independent Test**: Trigger empty, loading, and error conditions across
several features; each uses one shared visual treatment and consistent copy
conventions. Verifiable independently of buttons.

**Acceptance Scenarios**:

1. **Given** a list with no items, **When** the empty state renders, **Then** it
   uses one shared container and text treatment, is written in warm sentence
   case, and offers a next step when a relevant one exists.
2. **Given** data is loading, **When** the loading state renders, **Then** it
   uses one shared indicator treatment with consistent text size, color, and
   spacing.
3. **Given** a page or form action fails, **When** the error renders, **Then**
   it uses one shared danger treatment, one danger color, and is announced to
   assistive technology (`role="alert"`).
4. **Given** two screens describe the same concept, **When** their copy renders,
   **Then** shared terms are used consistently (e.g. "invite" — not
   "invitation" — for the same object).

---

### User Story 3 - Consistent page framing (Priority: P3)

As a visitor moving between comparable pages, the content column width and page
framing feel intentional and consistent rather than arbitrarily wider or narrower
from one page to the next.

**Why this priority**: Real but lower-impact polish; noticeable mainly when
moving between pages. Safe to ship last.

**Independent Test**: Compare content column widths across pages of the same
type (list pages, detail pages, form pages); each page type maps to one
documented width. Verifiable independently.

**Acceptance Scenarios**:

1. **Given** two pages of the same type, **When** both render on the same
   viewport, **Then** their content column uses the same maximum width.
2. **Given** any page, **When** it renders, **Then** its container width matches
   the documented rule for its page type.

---

### Edge Cases

- **Behavior preservation**: a migrated screen must keep the same actions,
  labels' meaning, `data-testid` hooks, routing, and disabled/busy logic — only
  the visual assembly changes. Existing component and e2e tests must still pass.
- **One-off variants**: a screen occasionally needs a legitimately different
  treatment (e.g. an icon-only button, a full-width form submit, a compact
  inline button). The primitive set must express these variations without a
  screen falling back to bespoke raw utilities.
- **Contrast conflict boundary**: the known DESIGN.md primary-button contrast
  issue (white-on-`coral-4` ≈ 3.14:1) is **not** resolved here; the primitive
  encodes today's documented tokens so the decision stays with the owner and can
  be changed in one place later.
- **Copy that should stay specific**: some loading/empty copy is intentionally
  contextual ("Loading your profile…"). Consistency means consistent *tone and
  treatment*, not forcing every message to identical wording.
- **Dark/verbose content**: primitives must not clip or overflow long labels,
  long empty-state sentences, or narrow mobile widths.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a shared, reusable primitive for buttons
  covering at least primary, secondary, and destructive (danger) variants, each
  encoding the DESIGN.md-mandated appearance and behavior (≥44px height, `md`
  radius, canonical on-accent/label tokens, coral glow on hover, 1px press nudge,
  always-visible coral focus ring).
- **FR-002**: The button primitive MUST support the variations existing screens
  need (e.g. full-width, icon-with-label, size where a smaller control is
  justified, disabled/busy) without requiring bespoke raw utility markup.
- **FR-003**: The system MUST provide a shared primitive for error/alert display
  with a single visual treatment and a single danger color, announced to
  assistive technology via `role="alert"`.
- **FR-004**: The system MUST provide a shared primitive for empty states with a
  single container and text treatment that supports an optional next-step action
  and warm, sentence-cased copy.
- **FR-005**: The system MUST provide a shared primitive for loading states using
  a single standardized text-line treatment with consistent text size, color,
  spacing, and copy pattern. A spinner is not introduced, and skeleton
  placeholders are not the standard treatment (any existing skeleton, e.g. the
  dashboard, is an intentional per-screen exception, not the primitive).
- **FR-006**: The system MUST provide a shared primitive (or documented rule) for
  page container width that maps each page type to one maximum width.
- **FR-007**: The system MUST provide a shared primitive for the card surface
  matching DESIGN.md (white surface, muted border, `lg` radius, soft shadow, hover
  lift, optional top gradient strip).
- **FR-008**: All primitives MUST source their colors, spacing, radii, shadows,
  typography, and voice from DESIGN.md tokens; introducing a new visual value
  requires adding a token to DESIGN.md first, not hard-coding it in a primitive.
- **FR-009**: Existing screens MUST be migrated to use the primitives in place of
  hand-assembled equivalents, removing the divergent bespoke markup documented in
  the audit findings.
- **FR-010**: Migration MUST be behavior-preserving: user-facing flows, labels'
  meaning, permissions, routing, `data-testid` hooks, and disabled/busy logic are
  unchanged; existing automated tests continue to pass.
- **FR-011**: Copy for shared concepts MUST be made consistent during migration.
  The canonical term for an invite object is **"invite"** (noun), used
  consistently across teams, parties, and events; "invitation" is not used.
  Legitimately contextual copy MAY remain specific.
- **FR-012**: Any place currently using a text glyph as an icon (e.g. a literal
  `+`) MUST use a Lucide icon per DESIGN.md.
- **FR-013**: The primitives MUST make the DESIGN.md rules the path of least
  resistance so that new screens adopt them by default and reintroducing the
  audited inconsistencies requires deliberately bypassing the primitives.
- **FR-014**: The feature MUST NOT change the primary-button contrast decision
  (white-on-`coral-4`); it encodes the currently documented tokens so any future
  contrast change is a single-source edit.
- **FR-015**: Per the project constitution, Angular pieces MUST keep their
  `.html`, `.css`, and `.ts` in separate files.

### Key Entities

- **UI primitive**: a reusable presentation building block (button, card,
  empty-state, loading indicator, error/alert, page container) that encapsulates
  one DESIGN.md-conformant appearance and behavior and exposes only the
  variations screens legitimately need.
- **Variant**: a named option on a primitive (e.g. button primary/secondary/
  danger; size; full-width) that stays within the design system.
- **DESIGN.md token**: the canonical color/spacing/radius/shadow/typography/voice
  value a primitive references; the single source of truth.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of interactive action buttons across the app share one corner
  radius, one minimum height of at least 44px, and one hover/press behavior.
- **SC-002**: 100% of interactive controls show a visible focus indicator when
  focused by keyboard.
- **SC-003**: 100% of page/form error messages use one visual treatment, one
  danger color, and are announced to assistive technology.
- **SC-004**: 100% of empty states use one shared treatment and warm
  sentence-cased copy; every empty state where a relevant next step exists offers
  one.
- **SC-005**: 100% of loading states use the single standardized text-line
  treatment with consistent size, color, spacing, and copy pattern (skeletons
  only where explicitly kept as an intentional exception).
- **SC-006**: Each page type maps to exactly one documented content-column width,
  with zero comparable pages differing.
- **SC-007**: Zero literal text glyphs remain in place of icons.
- **SC-008**: Zero user-facing behavior regressions — all pre-existing automated
  tests pass unchanged after migration, and manual smoke tests of migrated
  screens show identical actions and outcomes.
- **SC-009**: A new screen can present a standard button, card, empty, loading,
  and error state using only the primitives, with no bespoke raw-utility
  assembly required.

## Assumptions

- DESIGN.md remains the single source of truth; where a screen conflicts with
  DESIGN.md today, the primitive follows DESIGN.md and the conflict is reported,
  not silently resolved.
- The standing primary-button contrast conflict (white-on-`coral-4` ≈ 3.14:1) is
  a separate owner-level decision, tracked elsewhere, and explicitly out of scope
  here.
- Scope is the `frontend/apps/web` application; no backend, API, schema, auth, or
  permission changes are involved.
- **Delivery form** (clarified): primitives are Angular standalone components,
  with attribute directives only where a native `<button>`/`<a>` element must be
  preserved. Each keeps separate `.html`/`.css`/`.ts` files per the constitution.
  A Tailwind `@layer components` class layer is not the delivery vehicle.
- **Migration Definition of Done** (clarified): full app-wide migration — every
  screen moves onto the primitives, satisfying the 100% success criteria —
  delivered as phased PRs organized by feature area rather than one large change.
- "Behavior-preserving" is judged against existing component/e2e tests plus manual
  smoke checks.
- The existing token system in the app's global styles and Tailwind config
  (already sourced from DESIGN.md) is reused as the primitives' foundation.
- Contextual loading/empty copy may remain specific; consistency targets tone and
  visual treatment, plus unified terminology for shared concepts (canonical
  "invite").
