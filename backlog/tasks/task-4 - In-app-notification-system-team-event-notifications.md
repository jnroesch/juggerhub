---
id: TASK-4
title: In-app notification system (team & event notifications)
status: To Do
assignee: []
created_date: '2026-07-02 12:43'
labels:
  - feature
  - backend
  - frontend
  - notifications
dependencies: []
references:
  - specs/005-team-space/spec.md
priority: medium
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A reusable in-app notification system so users get notified inside the app about team and event activity. Motivated by Team Space (005-team-space), where targeted team invites are delivered by email only for now; this task adds an in-app notification surface (inbox + unread badge) and delivery, reusable across features (team invites, role changes, team news, future event/training updates). Complements, not replaces, transactional email.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Authenticated users have an in-app notifications surface (inbox + unread badge) reachable from the app shell
- [ ] #2 A targeted team invite generates an in-app notification that links to the invite accept/decline screen
- [ ] #3 Notification model is extensible to other sources (team role changes, team news, event/training updates)
- [ ] #4 Notifications support mark read/unread and are paginated (never unbounded), authorized server-side per-user
- [ ] #5 Integrates with existing transactional email without duplicating it; no cross-user data leakage
<!-- AC:END -->
