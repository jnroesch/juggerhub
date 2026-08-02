# Specification Quality Checklist: Transactional Email Templates & Notification Preference Gating

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

## Validation Notes

**Iteration 1 findings (resolved before first commit of the spec):**

1. *Implementation detail leakage* — the first draft named concrete classes and methods
   (`EmailTemplateService`, `GetEnabledRecipientsAsync`, `RawHtml`) throughout the
   requirements. These were rewritten as capability statements. The Problem Context still
   names the four affected messages by their user-facing trigger rather than by method,
   which keeps the section readable for non-technical stakeholders while remaining
   traceable to #109.

2. *Untestable requirement* — an early "emails should look consistent" requirement was
   replaced with FR-001/FR-002/FR-003, each asserting a specific observable element
   (header, footer reason, call-to-action plus fallback link).

3. *Unbounded scope* — the German/Spanish body-translation question was explicitly assigned
   to the Assumptions and Out of Scope sections rather than left implicit, since #84 owns it.

**Clarifications resolved by the requester before drafting** (no markers needed):

- Event cancellation gets a **new "Events" category** with a real toggle, rather than being
  always-on or folded into "Invites & roster changes".
- Event cancellation **does** gain an in-app notification type, making the Email toggle safe.
- Privacy/imprint footer links are delivered **in this feature**, not deferred.

**Iteration 2 — `/speckit-clarify` session 2026-08-01 (3 questions asked, 3 answered):**

All three answers were integrated; see the spec's `## Clarifications` section. Two changed
existing spec text rather than only adding to it:

1. *Scope increase* — German and Spanish bodies for the four new templates moved from
   Out of Scope into scope (FR-009a). Twelve template files rather than four. The
   Assumptions bullet that deferred them to #84 was replaced, the Out of Scope bullet was
   narrowed to the pre-existing invitation/team-news bodies only, and SC-002 was
   strengthened from "where a translation exists" to a 12-combination, zero-fallback check.
2. *Behaviour change* — FR-005 now requires the party news email to truncate to the same
   140-character excerpt as team news, replacing today's full-body behaviour.

The subject-escaping answer surfaced a correctness boundary worth recording: FR-006 was
narrowed to bodies, and FR-010 now states explicitly that subjects must **not** be escaped.
Escaping them would render encoded entities visibly in the inbox — a plausible-looking
mistake that a "sanitize everything" reading of FR-006 would have produced.

Authoring translated templates also introduced a new failure mode, captured as an edge case
and FR-026a: template fallback is per-file, not per-placeholder, so a German template
missing the call-to-action would ship a German email with no way to act on it.

**Open risk carried into planning** (not a spec gap):

- FR-006 changes rendering behaviour for *existing* templated emails, not only the four
  being migrated. Any current template that intentionally relies on markup passing through
  must be identified during planning and explicitly designated per FR-007, or it will start
  rendering its markup literally.

## Notes

- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
