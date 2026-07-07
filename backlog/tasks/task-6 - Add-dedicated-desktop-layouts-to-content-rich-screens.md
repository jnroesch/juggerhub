---
id: TASK-6
title: Add dedicated desktop layouts to content-rich screens
status: Done
assignee: []
created_date: '2026-07-06 20:16'
updated_date: '2026-07-06 21:17'
labels:
  - frontend
  - ui
  - responsive
dependencies: []
priority: medium
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Most content-rich screens render as a single centered max-w-* column that never reshapes at the md: breakpoint, wasting desktop width. Only profile-public has a dedicated desktop (multi-column) layout. Add desktop layouts to the content-rich screens that need them, following DESIGN.md (mobile-first, scaling to a 1100px content column; md: is the desktop breakpoint). Form/wizard screens (auth, onboarding, create/edit forms) are intentionally single-column and are out of scope. The app frame (sidebar/top-nav) is already responsive.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Each in-scope screen reshapes into an appropriate multi-column/aside desktop layout at md: (and lg: where useful)
- [ ] #2 Layouts use existing DESIGN.md tokens and Tailwind breakpoints; no new colors/radii/fonts introduced
- [ ] #3 Mobile single-column layout is preserved below md:
- [ ] #4 profile-owner reaches parity with profile-public's desktop layout
<!-- AC:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Added dedicated desktop layouts to the content-rich screens. Delivered: 6.1 Event Details (main column + sticky action/logistics aside), 6.2 Team Details (sticky identity aside + tabbed main), 6.3 Owner Profile (two-column parity with public profile), 6.4 Event Manage (awaiting-queue full-width + Joined/Waitlist side by side), 6.5 Team Invitations (sticky invite-link aside + search/pending main). 6.6 Dashboard archived (deferred until real content). All use existing DESIGN.md tokens; mobile single-column layouts preserved. Each verified with nx build (prod) + nx lint. Live browser/Playwright verification not run (needs full docker stack).
<!-- SECTION:FINAL_SUMMARY:END -->
