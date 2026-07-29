# Specification Quality Checklist: Self-Hosted Umami Analytics

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-28
**Updated**: 2026-07-28 (after clarification round 1)
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

All checklist items pass. Both open questions were resolved by the owner on 2026-07-28.

### Resolved: privacy disclosure (was FR-010)

Deferred to a dedicated full privacy-policy feature; moved to Out of Scope and tracked as
a follow-up. The interim consequence is recorded in the "Deferred disclosure" assumption
rather than left implicit: between this feature shipping and that one landing, the product
measures EU visitors with no privacy disclosure anywhere.

### Resolved: page-path recording (FR-008)

Owner chose verbatim full paths over route-pattern grouping, with the trade-off put to them
explicitly beforehand. The spec was then corrected so it no longer asserts privacy
properties it does not have:

- FR-005 was narrowed to the **viewer** (it previously implied no identifying data at all).
- FR-008 was inverted from "paths MUST be reduced" to "paths recorded verbatim", with the
  consequence stated inline.
- The "no consent banner required" assumption was **removed**. It was conditioned on no
  personal data being processed, which FR-008 no longer supports — a URL containing a
  username is generally personal data under EU law. Replaced with a narrower assumption
  covering only device storage (FR-006), which still holds.
- SC-005 and SC-012 were rewritten; SC-012 previously asserted the opposite of the decision.
- Two consequences were added to the Edge Cases: the dashboard becomes a member-level
  disclosure surface, and the page list gains a long one-visit tail.

**Standing risk for `/speckit-plan` to carry forward**: FR-008 and the deferred disclosure
compound each other. Personal data processing without a disclosure is the combination worth
re-checking before this reaches production, as distinct from reaching Dev.

### Deliberate naming exception

The product name *Umami* appears in the Input and Assumptions. Not leaked implementation
detail — the owner selected the product as part of the request, and the privacy
characteristics in FR-006/FR-007 are inherent properties of that choice, which is what
makes those requirements traceable rather than arbitrary.

### Informed guesses recorded as assumptions (not raised as questions)

- Retention indefinite by default; FR-003 sets a 12-month floor.
- Dashboard access owner-only, via the analytics product's own accounts, deliberately not
  wired to the platform's admin allowlist.
