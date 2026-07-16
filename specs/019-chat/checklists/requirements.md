# Specification Quality Checklist: Chat

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-16
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

- All checklist items pass as of the 2026-07-16 clarification session. 15/16 → 16/16.

### Resolved: the three [NEEDS CLARIFICATION] markers (FR-049 – FR-051)

Carried into `/speckit-clarify` rather than guessed, and answered by the product owner on
2026-07-16. See the spec's Clarifications section.

1. **FR-049 — DM reach** → **anyone can DM anyone.** No shared-context precondition. The
   product owner chose open reach over the recommended shared-context restriction. Two
   consequences were written into the spec rather than left implicit: blocking (US5) is now
   load-bearing safety rather than a nicety, and new-conversation/send rate limiting
   (FR-049a) was added so open reach cannot become a mass-DM tool. The residual risk — an
   unwanted first message can land before block is available — is accepted and recorded in
   Assumptions.
2. **FR-050 — message delete/edit** → **delete own message only, with a tombstone; no edit.**
3. **FR-051 — chat vs the Alerts inbox** → **Chat badge only.** Chat introduces no new
   `NotificationType`/`NotificationCategory`; it reuses feature 010's realtime *transport*
   only, not its notification store.

Everything else that was open was decided with the product owner before drafting and is
recorded in Assumptions: unfurl is view-only (no inline RSVP), auto-chat membership mirrors
the roster (mute, not leave), blocking is in scope and reporting is not, and own-message
bubbles follow DESIGN.md's coral rather than the wireframe's blue.

### Design conflict reported (not silently resolved)

Per constitution Quality Gate 7 and CLAUDE.md ("If UI requirements conflict with DESIGN.md,
report the conflict"): the wireframe draws own-message bubbles in **blue**. DESIGN.md makes
coral the primary brand color, reserves blue for the `info` status token, and forbids
introducing colors ad hoc. Resolved **toward DESIGN.md** (coral own-bubbles) with the product
owner. No DESIGN.md amendment is required.
