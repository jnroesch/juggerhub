# Specification Quality Checklist: Terms of Use with Community Rules

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### Iteration 1 — 2026-08-03

Two `[NEEDS CLARIFICATION]` markers were raised, both on points with no defensible default that
change what the binding document says. Both were resolved by the owner in the same session:

- **FR-013 (minimum age)** — resolved: **no minimum age and no age gate**. The document carries a
  guardian-responsibility clause instead; the platform never asks for an age. Explicitly
  document-side only, with no UI check.
- **FR-014 (how the terms change)** — resolved: **publish only**. The page and its date are the
  notice. No notification, announcement, or re-acceptance step is promised, since none exists.

### Iteration 2 — 2026-08-03

All items pass. Both markers replaced with concrete requirements; the accepted limitations of
each choice are recorded in **Assumptions** rather than left implicit, and both now also appear
in **Out of Scope** so planning cannot re-open them as work.

### Deliberately not flagged

The following were resolved by informed guess and recorded in **Assumptions** rather than
consuming a clarification slot: governing law (German, follows the imprint), availability
disclaimers (volunteer-run, follows the privacy policy's framing), whether a ban erases
content (it does not — feature 013 makes a ban a retained soft-delete), and the contact route
(the operator address already published in the privacy policy and imprint).

### What implementation revealed (2026-08-03)

Three things the spec and plan did not anticipate, all resolved without changing scope:

1. **`frontend/node_modules` was absent from this worktree**, so `npx nx test` silently resolved a
   different checkout and reported passing results for files it never read. Two early "green"
   runs in this session were meaningless because of it. Fixed with `npm ci`; the reliable
   invocation is `frontend/apps/web && ../../node_modules/.bin/jest --config jest.config.cts`.
   **This is a live trap for anyone working in a fresh worktree** — the `WorktreeCreate` hook
   seeds `.env` but not dependencies.
2. **`TermsOptions` went to `backend/Common/`**, not `backend/Services/Terms/` as the plan said.
   Every other `*Options` class lives in `Common/`; the code outranks the plan.
3. **The `Restrict` FK raises SQLSTATE `23001` (`restrict_violation`), not `23503`.** The survival
   test now asserts the narrower code, which additionally proves the behaviour is `Restrict`
   rather than `NoAction`.

### Scope-boundary note

The specification reserves rights in **FR-005** that the product cannot exercise through any
interface today. This is intentional and owner-decided, and is recorded explicitly in the
**Out of Scope** section together with **FR-008**, which forbids the document from describing
moderation tooling that does not exist. Planning must not quietly close this gap by building
moderation surfaces; it should stay a recorded follow-up.
