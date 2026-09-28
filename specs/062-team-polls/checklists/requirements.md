# Specification Quality Checklist: Team Polls — a Team Can Ask Its Members a Question

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-28
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

- The four questions #365 left open went to the owner before the spec was written. They are
  recorded under Clarifications (session 2026-09-28): the team page in its own card, admins only,
  named or anonymous chosen per poll and fixed, and no link to parties. No [NEEDS CLARIFICATION]
  markers were needed.
- The secondary choices are defaults, listed at the top of Assumptions so `/speckit-clarify` can
  revisit them. They cover single or multi-choice per poll, the optional close date, locking at
  the first answer, and changing or withdrawing an answer. They also cover closing early with no
  reopen, keeping the author out of their own *Needs you*, and leaving the question out of device
  notices. The rest are notices under *Team news* and the numeric limits.
- "Server" appears in requirements (FR-006, FR-018, FR-033) because the never-trust-the-client
  boundary *is* the user-visible guarantee for who may act and what anonymity hides. It does not
  choose an implementation.
- Four findings from reading the product are stated in Context so planning does not re-derive
  them:
  - the team page is signed-in-public, so polls must be refused to non-members;
  - anonymity must hold in every response, not only on screen;
  - 061's rename rewrite reaches only alerts that carry the team's address;
  - the Terms of Use sentence listing what outlasts an account deletion.
