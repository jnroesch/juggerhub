---
id: TASK-6.4
title: 'Desktop layout: Event Manage (participants & waitlist)'
status: Done
assignee:
  - '@claude'
created_date: '2026-07-06 20:17'
updated_date: '2026-07-06 21:17'
labels:
  - frontend
  - ui
  - responsive
dependencies: []
parent_task_id: TASK-6
priority: low
ordinal: 10000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
features\events\event-manage\event-manage.component.html manages participants\waitlist in a cramped max-w-2xl list. Widen and reshape for desktop at md;C:\Program Files\Git\lg; (wider container, table\grid rows) so admin management is comfortable on desktop. Preserve mobile stacking.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 At md:/lg: the management view uses a wider container and a desktop-appropriate list/table layout
- [x] #2 Promotion/withdraw/waitlist admin actions still work; existing data-testids preserved
- [x] #3 Below md the layout is unchanged
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Widen container: max-w-2xl -> add lg:max-w-4xl.
2. Turn the sections wrapper into a grid at lg: (lg:grid lg:grid-cols-2 lg:gap-xl lg:space-y-0), keeping mobile space-y-md stacking.
3. 'Awaiting approval' spans both columns (lg:col-span-2) as the priority action queue; 'Joined' and 'Waiting list' sit side by side below.
4. Row markup (label + action buttons, justify-between) and all data-testids/handlers untouched.
5. Verify: nx build prod + nx lint.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented in event-manage.component.html: container widened (lg:max-w-4xl); the sections wrapper becomes lg:grid lg:grid-cols-2 lg:items-start lg:gap-xl lg:space-y-0. 'Awaiting approval' spans both columns (lg:col-span-2) as the priority queue; 'Joined' and 'Waiting list' sit side by side below. Row markup, handlers, and data-testids (approve, promote) untouched. Mobile: unchanged single stacked column (space-y-md). Verified: nx build prod + nx lint pass. Live browser not run (needs docker stack).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Event Manage now uses a wider desktop grid: at lg: the 'Awaiting approval' queue spans full width on top, with 'Joined' and 'Waiting list' side by side below; container widened to lg:max-w-4xl. Below lg it stays the single stacked column. All admin actions and data-testids preserved. Verified with nx build (prod) + nx lint.
<!-- SECTION:FINAL_SUMMARY:END -->
