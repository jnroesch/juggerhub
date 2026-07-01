---
id: TASK-3
title: Optimize profile picture handling (blob storage + image processing)
status: To Do
assignee: []
created_date: '2026-07-01 08:54'
labels:
  - backend
  - profile
  - tech-debt
  - future
dependencies: []
priority: low
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The 003-profile feature stores avatars as bytea in Postgres (ProfileAvatars table) — a deliberate parity-first MVP choice with no new infra. This task tracks the future optimization: move avatar bytes to dedicated blob/object storage and add server-side image processing so large uploads don't bloat the DB or the wire.

Context/rationale (see specs/003-profile/research.md §4): the current storage sits behind IProfileService.GetAvatar/SetAvatar and the /profiles/{handle}/avatar endpoint, so the storage mechanism can change without touching callers or the frontend.

Scope ideas (refine when picked up):
- Introduce blob/object storage (e.g. Azure Blob) behind the existing IProfileService avatar seam, keeping environment parity and the constitution's no-Key-Vault rule in mind.
- Process uploads: validate, downscale/resize to a max dimension, re-encode (e.g. to WebP), strip EXIF/metadata, possibly generate thumbnail sizes.
- Enforce/adjust size limits and serve with appropriate caching headers.

Promote to Spec-Kit when picked up (architecture + storage change).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Avatar bytes are stored outside the primary Postgres row/table (blob/object storage) behind the existing IProfileService avatar seam
- [ ] #2 Large images are processed server-side (resize/re-encode, metadata stripped) before storage
- [ ] #3 Environment parity is preserved (local/Dev/Prod) and no secrets are committed
- [ ] #4 Public avatar URL and frontend behavior are unchanged for callers
<!-- AC:END -->
