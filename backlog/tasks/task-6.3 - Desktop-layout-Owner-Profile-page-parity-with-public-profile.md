---
id: TASK-6.3
title: 'Desktop layout: Owner Profile page (parity with public profile)'
status: Done
assignee:
  - '@claude'
created_date: '2026-07-06 20:16'
updated_date: '2026-07-06 21:11'
labels:
  - frontend
  - ui
  - responsive
dependencies: []
parent_task_id: TASK-6
priority: medium
ordinal: 9000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
features/profile/profile-owner/profile-owner.component.html is single-column while its public twin profile-public already has a two-column md: layout. Bring the owner/edit profile view to desktop parity with a comparable two-column layout, keeping edit affordances and the mobile stack below md.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Owner profile uses a two-column desktop layout consistent with profile-public at md:
- [x] #2 All edit controls remain usable and correctly grouped in the desktop layout
- [x] #3 Below md the layout is unchanged (single stacked column)
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Mirror profile-public's md: two-column layout for parity:
1. Container: max-w-xl -> add md:max-w-4xl (keeps mobile width, widens at md like public).
2. Keep the top bar (back + edit/cancel toggle) full-width above the grid.
3. Wrap in md:grid md:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)] md:gap-xl md:mt-lg.
   - Left column: identity header (md:mt-0), teams, plays (incl. pompfe-selector in edit mode), badges — with existing hr separators.
   - Right column: <section class=md:border-l md:border-border md:pl-xl> Recent activity, with a mobile-only hr (md:hidden) so the mobile separator before activity is preserved.
4. Keep error/saved messages, the sticky save bar (edit mode), and the 'View public profile' link full-width below the grid.
5. Do not touch any edit logic (editing(), form, save/cancel) or data-testids.
6. Verify: nx build prod + nx lint.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented in profile-owner.component.html, mirroring profile-public:
- Container: max-w-xl + md:max-w-4xl (mobile unchanged, widens at md like public).
- Top bar (back + edit/cancel) stays full-width above the grid.
- md:grid md:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)] md:gap-xl md:mt-lg:
  * Left column: identity header (md:mt-0), teams, plays (pompfe-selector in edit mode), badges — existing hr separators kept.
  * Right column: <section md:border-l md:border-border md:pl-xl> Recent activity; added a mobile-only <hr md:hidden> so the badges->activity separator is preserved on mobile.
- Error/saved messages, the sticky save bar (edit mode), and 'View public profile' link stay full-width below the grid.
- No edit logic or data-testids touched (editing(), form, save/cancel, pompfe-selector, avatar-input all intact). Mobile order/spacing identical.
Verification: nx build web --configuration=production pass; nx lint web pass. Live browser/Playwright not run (needs full docker stack).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Brought the owner profile to desktop parity with profile-public: at md: a two-column layout (left: identity/teams/plays/badges; right: recent activity with md:border-l), container widened to md:max-w-4xl. Edit mode (form, pompfe-selector, sticky save bar) and all data-testids preserved; below md it stays the existing single stacked column with a mobile-only separator retained. Verified with nx build (prod) and nx lint, both pass.
<!-- SECTION:FINAL_SUMMARY:END -->
