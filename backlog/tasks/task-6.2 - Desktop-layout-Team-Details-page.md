---
id: TASK-6.2
title: 'Desktop layout: Team Details page'
status: Done
assignee:
  - '@claude'
created_date: '2026-07-06 20:16'
updated_date: '2026-07-06 21:07'
labels:
  - frontend
  - ui
  - responsive
dependencies: []
parent_task_id: TASK-6
priority: medium
ordinal: 8000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
features\teams\team-detail\team-detail.component.html renders roster, news and public\internal sections in a single max-w-xl column. Add a two-column desktop layout at md;C:\Program Files\Git\lg; (e.g. roster\identity aside + news\main), preserving the mobile stack below md and using DESIGN.md tokens.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 At md:/lg: the page uses a multi-column desktop layout appropriate to its sections
- [x] #2 Public vs internal section visibility rules are unchanged
- [x] #3 Below md the layout is unchanged (single stacked column)
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Widen container at lg: (max-w-xl -> lg:max-w-4xl); keep max-w-xl below lg.
2. Wrap the detail body in lg:grid with template cols [minmax(0,18rem) minmax(0,1fr)] + lg:gap-xl (default items-stretch so the sticky aside cell has scroll room).
3. Left aside (grid cell) with inner lg:sticky lg:top-[4.5rem] wrapper: the cover/identity card + the admin 'Invite people' button.
4. Main column wrapper (mt-lg lg:mt-0): the tabs nav (drop its mt-lg, wrapper owns mobile spacing), error, and the Members/Activity/News tab sections — logic untouched.
5. Preserve all conditionals (isAdmin, tab()), data-testids, and the member ⋯ dropdown. Below lg it collapses to the existing single stacked column.
6. Verify: nx lint + nx build production.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Implemented in team-detail.component.html:
- Container widens at lg: (max-w-xl -> lg:max-w-4xl); below lg unchanged.
- Body wrapped in lg:grid lg:grid-cols-[minmax(0,18rem)_minmax(0,1fr)] lg:gap-xl.
- Left aside (sticky, inner lg:sticky lg:top-[4.5rem]): identity/cover card + admin 'Invite people' button.
- Main column (mt-lg lg:mt-0): tabs nav + Members/Activity/News sections. Dropped the nav's mt-lg; the main wrapper owns the mobile gap, so mobile spacing is identical.
- All conditionals (isAdmin, tab(), CityTeam/Mixteam) and data-testids untouched; member ⋯ dropdown (absolute within its relative li) unaffected.
- Breakpoint: used lg: (not md:) because a 288px aside + content is too tight at md=768px; DESIGN.md's column comfortably fits two columns at lg. Tablet (md-lg) keeps the single stacked column.
- Mobile order is pixel-identical to before (aside=cover+invite, then main=tabs+content — same sequence).

Verification: nx build web --configuration=production pass; nx lint web pass (only pre-existing unrelated spec warning). Live browser/Playwright not run (needs full docker stack).
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Gave Team Details a dedicated desktop layout: at lg: a sticky left identity/invite aside + a wider main column holding the tabs (Members/Activity/News) and their content. Container widens to lg:max-w-4xl; below lg it collapses to the existing single stacked column with identical mobile order and spacing. Section visibility rules and all data-testids preserved. Verified with nx build (prod) and nx lint, both pass.
<!-- SECTION:FINAL_SUMMARY:END -->
