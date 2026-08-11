# Specification Quality Checklist: Team-internal "What's happening" section

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-11
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — five questions asked and answered 2026-08-11 (spec §Clarifications, §Resolved Decisions D1–D5)
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

### Grounding findings that changed the spec

Discovered by reading the code, not assumed from the issue:

- **Two of issue #178's premises are wrong.** The team detail surface is *not*
  anonymous-reachable — feature 026 made it authenticated-only, so "public" here means
  *signed-in*. And the event-shaped item DTO is shared with the **profile** surfaces only; the
  admin surface has its own separate shape.
- **Two candidate kinds are not derivable at all.** Memberships are hard-deleted and roles
  overwritten in place, so departures, removals and role changes leave no trace. This is why
  they are excluded (D1) rather than deferred for convenience.
- **A per-session training kind would flood the section.** `RecurrenceExpander.MaxSessions` is
  **520**; one weekly recurring training writes up to 520 session rows in a single save, all
  sharing a timestamp. Drove D3 and SC-004.
- **The members-only paginated activity endpoint has no caller in the app.** It exists but no
  screen reaches it. Recorded as out of scope (D4/FR-018) rather than assumed working.
- **The team page already has a teaser→route pattern** (the Trainings card links to
  `/t/{slug}/trainings`). Offered as the full-history option; the owner declined it (D4).

### The reshaping

The feature was respecified mid-clarification. The original direction — merge everything into
the existing section — was rejected by the owner in favour of **two separate sections** (D5).
The merged design's three hardest problems dissolved with it: the per-kind visibility matrix,
the team-only-training leak path, and the collision between a recency cutoff and the existing
event history. The earlier draft's FR-011..FR-015 (visibility gating per kind) and User Story 3
(paginated full feed) are gone, not deferred.

### Carried into planning

- **FR-019/FR-020 are the main UI risk**: for members, one award is described in both the
  standing-collection card and the dated section. Instantiate `checklists/ui-review.md` from
  `.specify/templates/ui-review-checklist-template.md` and settle the visual distinction against
  DESIGN.md (constitution quality gate 7).
- **FR-017's rename** touches en/de/es and must not collide with the dashboard's own
  "Was ist los" wording — SC-010 is the check.
- **SC-008** states a performance budget exists but leaves the number to the plan, where the
  four-source read cost can be measured against a real query shape.
- **Follow-up issue** for departures and role changes per D1 — raise it so the exclusion is
  tracked outside this spec.
