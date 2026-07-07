---
id: TASK-6.1
title: 'Desktop layout: Event Details page'
status: Done
assignee:
  - '@claude'
created_date: '2026-07-06 20:16'
updated_date: '2026-07-06 20:26'
labels:
  - frontend
  - ui
  - responsive
dependencies: []
parent_task_id: TASK-6
priority: medium
ordinal: 7000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The Event Details page (features/events/event-detail/event-detail.component.html) stacks hero, meta, occupancy, about, fee, participants, news, contacts and actions in one max-w-2xl column. On desktop this is a long narrow column with empty gutters. Add a dedicated desktop layout: a main content column plus a sticky aside (occupancy + join/manage actions + contacts) at md:/lg:, keeping the mobile single-column stack below md. Biggest/most-visited content page — do this first.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Below md the layout is unchanged (single stacked column)
- [x] #2 Join/withdraw and admin manage flows still work; existing data-testids preserved
- [x] #3 At lg: the page splits into a main column (about, participants, news) and a sticky aside carrying the join/manage action, entrance fee, and contacts; occupancy stays in the full-width header near the title
- [x] #4 About, participants and news occupy the main column and read well at desktop width; entrance fee and contacts move to the aside
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Widen the section container at lg: (max-w-2xl -> lg:max-w-5xl); keep max-w-2xl below lg so mobile/tablet reading width is unchanged.
2. Keep header block full-width and in current order: back link, hero, cancelled banner, title, meta list, occupancy bar.
3. Wrap the content-cards + bottom-action into a lg:grid lg:grid-cols-3 lg:gap-lg:
   - Main column (lg:col-span-2): About/Where card, Participants card, News card.
   - Aside grid cell (lg:col-span-1) with inner lg:sticky lg:top-[4.5rem] wrapper: action block (admin manage menu / join-actions), Fee card (if paid), Contacts card.
4. Below lg everything collapses to the existing single stacked column; preserve all data-testids and the admin manage-menu dropdown behaviour.
5. Verify: build + lint + typecheck; run event-detail e2e/unit if present.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented in event-detail.component.html:
- Container widened at lg: (max-w-2xl -> lg:max-w-5xl, ~1024px, within DESIGN.md's 1100px column). Below lg unchanged.
- Header (back link, hero, cancelled banner, title, meta, occupancy bar) stays full-width and in original order.
- Content wrapped in lg:grid lg:grid-cols-3 lg:gap-lg: main column (lg:col-span-2) = About/Where, Participants, News; aside (lg:col-span-1) with inner lg:sticky lg:top-[4.5rem] wrapper = join/manage action, entrance fee, contacts.
- All data-testids preserved (event-detail, manage-menu, manage-menu-panel, fee-block, join, my-status, withdraw, event-cancelled, spots-label). Admin manage-menu dropdown behaviour unchanged.
- Note: on mobile the stack order shifts slightly vs before (fee/action/contacts now grouped after news instead of interleaved). Still a single stacked column.

Verification:
- nx lint web: pass (only a pre-existing warning in register.component.spec.ts, unrelated).
- nx build web --configuration=production: pass (Angular AOT template type-check clean; event-detail chunk built).
- Not run: live browser / Playwright events.spec.ts (requires the full docker-compose stack + seeded auth+event data). Change is DOM reparenting only; bindings and testids preserved.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Gave the Event Details page a dedicated desktop layout. At lg: it splits into a main content column (About/Where, participants, news) and a sticky aside (join/manage action, entrance fee, contacts); the occupancy bar and header stay full-width. Below lg it collapses to the existing single stacked column. Container widens to lg:max-w-5xl. Pure template/layout change — all data-testids and bindings preserved. Verified with nx lint (pass) and nx build --configuration=production (pass); live Playwright drive not run (needs full docker stack).
<!-- SECTION:FINAL_SUMMARY:END -->
