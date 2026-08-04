# Specification Quality Checklist: Browse Public Trainings

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-04
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

**Validation run 1 (2026-08-04)** — 15 of 16 items pass. No spec rewrite was needed on this pass.

Evidence for the items most at risk on a feature this close to existing code:

- *No implementation details* — the known-implementation facts from issue #145 (the shared
  location-label helper, the effective-visibility expression, the distance cache, the existing
  browse components) are deliberately held out of the requirements and stated as behaviour instead:
  FR-009 says "the same rule the rest of the product already uses", FR-003 says "a session's own
  visibility setting overrides the series default in both directions". Those implementation facts
  are recorded in issue #145 and belong in `plan.md`.
- *Scope is clearly bounded* — every out-of-scope item is a testable requirement (FR-030 to FR-032)
  rather than prose, so a violation fails a check instead of a reading.
- *Success criteria technology-agnostic* — SC-010 mentions "existing tests continuing to pass",
  which is a verification method rather than a technology, and no SC names a framework, endpoint,
  or component.

**Validation run 2 (2026-08-04, after `/speckit-plan`)** — 16 of 16 items pass.

The one outstanding marker, on **FR-024**, was closed during Phase 0 research (R1) rather than by an
owner answer: order within a distance by date, and default the range's upper bound to two weeks
ahead under nearest-first, shown as a removable filter chip. FR-024 now states that behaviour and
carries a note recording how it was decided.

⚠ **This is a decision, not an answer.** It is tracked in `plan.md` under Open Decisions #1 with both
alternatives intact, and reversing it costs one conditional plus one translated label. Treat the
checklist item as passing because the spec is no longer ambiguous — not as evidence the owner has
signed off on this particular remedy.

**One further item worth an owner's eye**, recorded in `plan.md` → Spec Drift rather than as a
checklist failure: FR-006 says "not yet ended", while the implementation will use "not on an earlier
day" (`SessionDate >= today`, UTC), because that is what every other trainings query in the product
already does. A session that finished this morning stays listed until midnight.
