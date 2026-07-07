---
id: TASK-6.5
title: 'Desktop layout: Team Invitations list'
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
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
features\teams\team-invitations\team-invitations.component.html is a max-w-xl list. Give it a desktop-appropriate wider\table-style layout at md;C:\Program Files\Git\lg;, preserving the mobile stack. Lower priority than the detail pages.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 At md:/lg: the invitations list uses a wider desktop layout
- [x] #2 Accept/decline/resend actions still work; existing data-testids preserved
- [x] #3 Below md the layout is unchanged
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Widen container: max-w-xl -> add lg:max-w-4xl.
2. Keep header (back + 'Invite people' h1) and error full-width.
3. lg:grid lg:grid-cols-3 lg:gap-xl inside the @else block:
   - Left aside (lg:col-span-1) with inner lg:sticky lg:top-[4.5rem]: the 'Share your invite link' block.
   - Main (lg:col-span-2): 'Or add someone directly' (search + results) and 'Sent to people' (pending list).
4. Preserve all data-testids (invite-link, copy-link, rotate-link, create-link, user-search, invite-<handle>, pending-invites, revoke-<id>) and handlers. Mobile order (link, search, pending) unchanged.
5. Verify: nx build prod + nx lint.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented in team-invitations.component.html: container widened (lg:max-w-4xl); header + error stay full-width. lg:grid lg:grid-cols-3 lg:gap-xl inside the @else: left aside (lg:col-span-1, inner lg:sticky lg:top-[4.5rem]) holds the 'Share your invite link' block; main (lg:col-span-2) holds 'Or add someone directly' (search+results) and 'Sent to people' (pending). Removed an initial lg:items-start so the aside cell stretches and the sticky link works. All data-testids (invite-link, copy-link, rotate-link, create-link, user-search, invite-<handle>, pending-invites, revoke-<id>) and handlers preserved. Mobile order (link, search, pending) unchanged. NB: this screen's real actions are copy/rotate/create link, invite, and revoke (no accept/decline/resend as the AC generically worded). Verified: nx build prod + nx lint pass. Live browser not run (needs docker stack).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Team Invitations now uses a desktop 3-col grid: a sticky left aside with the shareable invite-link block + a wider main column holding the player search and the pending-invites list; container widened to lg:max-w-4xl. Below lg it stays the existing single stacked column (link, search, pending). All invite/revoke actions and data-testids preserved. Verified with nx build (prod) + nx lint.
<!-- SECTION:FINAL_SUMMARY:END -->
