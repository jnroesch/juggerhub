# Specification Quality Checklist: Chat Push Notifications

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-20
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

Three owner decisions were taken in the clarification round of 2026-09-20 and are recorded in the
spec's Clarifications section:

1. **Quiet delay: 30 seconds** (FR-001), configurable with that as the safe default.
2. **Payload carries a preview** (FR-019), not just the sender's name. This is a widening of
   feature 055's "name the subject, don't reproduce it" rule and was chosen over the conservative
   option with the privacy cost stated. It produces two hard obligations that are now requirements
   rather than notes: the privacy policy must describe it (FR-029) and nothing about it may be
   logged (FR-021c).
3. **Chat becomes an entry in the preferences matrix** (FR-027), Push toggle only. This amends one
   clause of 019 FR-051a — the "no new preference category" clause. The clauses that matter (no
   notification type, no Alerts row) are restated and upheld (FR-004).

Carried into planning:

- 019 needs an amendment callout, in the shape 022 / 046 / 048 each used.
- FR-029 makes a legal-copy change part of this feature's definition of done, in all three
  locales, German authoritative.
- Gate 7 (UI review checklist) is engaged: the matrix gains a row with unavailable cells, and new
  copy ships in three catalogues.
