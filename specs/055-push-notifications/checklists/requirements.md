# Specification Quality Checklist: Push Notifications

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-19
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — the spec says "delivery address", "push service", "enabled device"; keys, protocols, entity names, endpoints and the background worker are left to the plan
- [x] Focused on user value and business needs — framed as the reach the product does not have, with the nine existing producers as the scope
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — the three decisions (push column shape, payload content, device list) were taken by the owner on 2026-09-19 and are recorded under Clarifications
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified — permission is the browser's and is not given back, revoked-after-subscribe, iOS below 16.4, two browsers on one machine, shared device, in-app off with push on, stale subject, provider throttling, language change, nothing to send to
- [x] Scope is clearly bounded — FR-026/027/028 exclude chat, a device list, quiet hours, and any change to the two existing channels
- [x] Dependencies and assumptions identified — 054 merged and nothing else open; #309 depends on the delivery path this feature shapes

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- All items pass on the first validation pass; ready for `/speckit-plan`.
- **Two premises in issue #308 were corrected by reading the code before writing this spec**, and
  the plan must carry both:
  1. The issue says fan-out is centralised and that both creation methods "already consult the
     preference service before writing", implying push slots in beside email. They consult it for
     the **in-app** channel only, and the check is an early return: an event whose in-app category
     is off never reaches the end of that method at all. Email is not sent there either — it is
     sent by each of eight producing domain services next to their in-app call. So FR-015's
     independence is not free; it is the load-bearing constraint of the design.
  2. The issue names "a fourth column at 375px in German" as the binding layout case. At 375px the
     matrix is not a grid — each category is its own card with a labelled row per channel. The
     four-column grid only exists from the medium breakpoint up. The binding cases are the desktop
     grid at its narrowest and the mobile card gaining a fourth row.
- One assumption is flagged for the plan to confirm rather than repeat: that FR-024's disclosure is
  a genuine addition to the privacy policy rather than covered by existing text. Feature 054 made
  the same assumption in reverse and was wrong.
