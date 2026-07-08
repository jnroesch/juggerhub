---
id: TASK-4
title: In-app notification system (team & event notifications)
status: In Progress
assignee: []
created_date: '2026-07-02 12:43'
updated_date: '2026-07-08 08:15'
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
- [x] #1 Authenticated users have an in-app notifications surface (inbox + unread badge) reachable from the app shell
- [x] #2 A targeted team invite generates an in-app notification that links to the invite accept/decline screen
- [x] #3 Notification model is extensible to other sources (team role changes, team news, event/training updates)
- [x] #4 Notifications support mark read/unread and are paginated (never unbounded), authorized server-side per-user
- [x] #5 Integrates with existing transactional email without duplicating it; no cross-user data leakage
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Routed through Spec-Kit (specs/010-notifications): spec, plan, data-model, contracts, research, quickstart, tasks. Backend: Notification entity (jsonb payload, per-recipient indexes, dedupe), NotificationService + SignalR hub (/hubs/notifications, per-user group), NotificationsController (list/unread-count/read/read-all). Producers: targeted invite (inline accept/decline), role change, and new admin-only team-news POST + roster fan-out. Frontend: notification.service (REST + lazy SignalR + signals), rebuilt /alerts inbox, notification-row, bell/tab unread badge, team-space news composer.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Delivered on branch 010-notifications. Confirmed decisions with owner: SignalR realtime (lazy-loaded to keep initial bundle < budget), producers = invite + role-change + team-news, inline invite accept/decline reusing existing invitation endpoints. Spec drift: team-news POSTING did not exist (only reading) — added a minimal admin-only POST /teams/{slug}/news to give the news notification a trigger (recorded in spec Assumptions; belongs to 005-team-space long-term). Verification: 6 new notification integration tests + 38 Teams tests pass against real Postgres; frontend build (492.8kB initial, under budget) + 67 unit tests pass. Realtime push + browser UI to be smoke-tested via specs/010-notifications/quickstart.md. Not yet committed/PR'd (awaiting user).
<!-- SECTION:NOTES:END -->
