# Specification Quality Checklist: Privacy Policy & Imprint

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-31
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain — **1 open: the imprint particulars (Q1). Legal basis and language treatment resolved 2026-07-31.**
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

- The clarifications were the decisions issue #92 explicitly identified as requiring the owner ("The non-analytics sections need the owner's input or legal review; they cannot be drafted from the codebase alone"), not gaps in the specification. Two are resolved and recorded under Clarifications: **legitimate interest with no consent banner**, and **German authoritative with en/es informational translations**.
- The one remaining open item is the operator's imprint particulars. Structure, routing, the data inventory, the design treatment, and the translations do not depend on it — only the Prod deploy does. The plan must also decide whether those particulars live in a committed file or in per-environment configuration, since this repository is public and git history is not retractable.
- Verified against the running system while drafting: 033 is merged and deployed (`b38cee4`, `47288e6`), so the disclosure gap is live; no self-service export or account deletion exists; no footer component exists; DESIGN.md has no long-form content treatment; Resend and Azure are the processors in Dev/Prod; the 030 geocoder is not deployed.
