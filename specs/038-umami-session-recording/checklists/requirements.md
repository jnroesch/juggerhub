# Specification Quality Checklist: Umami Session Recording

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-01
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

All five owner decisions are recorded in Clarifications (Session 2026-08-01) with their
trade-offs written into Assumptions rather than left implicit:

1. **No consent banner** — legitimate interest, DNT/GPC as the objection route.
2. **Delivery follows 033's pattern**, not the raw script tag as supplied.
3. **Masking covers typed input only** (FR-006/FR-006a) — displayed text, including chat
   message history, is captured.
4. **30-day retention** (FR-012), automatic.
5. **No member notification** (FR-020) — the updated policy page is the only disclosure.

Two open items remain, neither blocking planning: session sampling (assumed all sessions)
and where the retention job runs (a design question for `/speckit-plan`).

**Risks carried forward into planning, all recorded in the spec rather than resolved:**

- Decisions 1, 3, and 5 compound. Recording that captures displayed message content,
  without consent and without telling members it started, is the weakest combination of
  the options offered. Each was declined individually; the combination is what planning
  should keep visible.
- FR-006a reaches member-to-member communication: the author of a message captured in
  another member's recording cannot object, since the recording is made by the reader's
  browser. Called out in Edge Cases and Assumptions → "Displayed text is captured, and
  chat is the hard case".
- FR-012 is new capability. No automated retention mechanism exists anywhere in the
  platform today (recorded in 036), so the 30-day promise has nothing existing to
  configure — FR-012a makes recording contingent on that mechanism actually working.

**Content-quality note**: the spec names rrweb and browser storage once, in Assumptions
("Verified rather than assumed"), as evidence for the FR-021 amendment. That is a
recorded observation about the deployed component, not a design choice, and it is
deliberately kept out of the requirements.
