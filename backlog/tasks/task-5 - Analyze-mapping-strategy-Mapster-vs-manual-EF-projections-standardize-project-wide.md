---
id: TASK-5
title: >-
  Analyze mapping strategy: Mapster vs manual EF projections; standardize
  project-wide
status: To Do
assignee: []
created_date: '2026-07-05 08:00'
labels:
  - backend
  - tech-debt
  - architecture
dependencies: []
priority: low
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Decide and standardize a single entity->DTO mapping strategy across the backend. Usage is currently inconsistent: Mapster is registered (Common/MappingConfig assembly scan) with two IRegister configs (AuthMappingRegister, ProfileMapping), but there is exactly ONE runtime Mapster call in the whole codebase (AuthService: user.Adapt<AuthUserDto>()). Every list/read DTO in Profile (003), Teams (005), and Events (006) is built with manual EF `.Select(e => new Dto(...))` projections. The constitution (Principle II) states "services return entities; the controller maps entities to the response DTO with Mapster", which practice already deviates from. Pick one approach and apply it consistently (or codify a documented split), then reconcile the constitution wording.

## Manual EF projections (current dominant practice)
Pros: pulls only needed columns (Principle III); explicit security boundary (never loads tokens/emails/secrets); no reflection/startup cost; trivially handles computed DTO fields (e.g. occupiedSpots, ViewerRelationDto); one obvious place per query.
Cons: verbose/repetitive; mapping lives in services not controllers; diverges from the constitution wording; easy to drift between similar DTOs.

## Mapster (constitution intent)
Pros: less boilerplate; central config; matches the constitution wording; ProjectToType can still push projection into SQL.
Cons: auto-mapping can leak sensitive columns unless carefully configured; computed/derived fields still need custom .Map/AfterMapping; extra abstraction + startup scan; only used once today so broad adoption is net-new work; harder to see exactly what crosses the DTO boundary at a glance.

Deliverable: an ADR-style decision, the constitution wording updated to match, and either a refactor plan or a documented "projections for reads, Mapster for X" split.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Audit lists every current mapping site (Mapster vs manual projection) with counts
- [ ] #2 A decision is recorded (single strategy OR a documented split) with rationale
- [ ] #3 Constitution Principle II wording is reconciled with the chosen strategy
- [ ] #4 If a refactor is chosen, follow-up tasks are created; otherwise the split is documented in the constitution
<!-- AC:END -->
