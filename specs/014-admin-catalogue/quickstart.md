# Quickstart & Validation: Admin catalogue management

A run/validation guide proving the feature works end-to-end. Implementation detail
lives in `tasks.md`; API shapes are in [contracts/admin-catalogue-api.md](contracts/admin-catalogue-api.md).

## Prerequisites

- Local stack via docker-compose (backend + Angular + PostgreSQL + Mailpit), as in
  the repo README. Migrations auto-apply on startup; **this feature adds none**.
- An admin account. The e2e stack designates `admin@test.de` (password
  `Str0ng!Passw0rd`) via `ADMIN_EMAILS` → `PlatformAdmin` role sync (feature 013).
  Locally, add your email to `ADMIN_EMAILS` in `.env`.

## Build / test commands

```powershell
# Backend
dotnet build backend/JuggerHub.sln
dotnet test  backend/tests/JuggerHub.Api.IntegrationTests

# Frontend
nx test web
nx lint web
nx build web

# End-to-end (admin area) — isolated e2e compose
docker compose -f docker-compose.e2e.yml up --build   # or: nx e2e web-e2e
```

## Manual validation (signed in as an admin, at `/admin/catalogue`)

1. **Browse (US1)** — Toggle Badges ⇄ Achievements; confirm each type shows icon,
   name, description, applies-to, a **grant count**, and Active/Retired. Switch the
   All/Active/Retired filter. Shrink to phone width → the table becomes cards.
2. **Create (US2)** — New type → pick Kind, name, description, applies-to (try
   selecting neither → rejected). Save → it appears Active with `granted 0`. For a
   players badge, open a player's Assign picker → the new badge is offered.
3. **Edit (US3)** — Open the new type → form is pre-filled, shows grant count +
   created date, Kind is locked. Change the description → catalogue reflects it.
4. **Icon (US4)** — Open the icon chooser → drag/select a square PNG → preview at
   32/40/56 (circle for a badge, rounded square for an achievement) → save → icon
   shows in the list and picker. Replace it → new image everywhere. Remove it →
   placeholder returns. Try a .txt or an oversized file → refused, icon unchanged.
5. **Retire + reinstate (US5)** — Retire an active type → amber confirm spelling
   out what happens → confirm → it shows Retired and leaves the Assign picker; a
   holder's profile still shows the award. Reinstate from the catalogue → it is
   Active again and back in the picker. Confirm there is no permanent-delete action.
6. **Team awards (US6)** — Admin nav → Teams → search a team → open it → Assign a
   team-applicable badge → it appears on the team's public page (`/t/{slug}`).
   Revoke it → removed. The picker offers only team-applicable, non-retired types
   and marks ones already held.

## Automated coverage (Definition of Done)

- **Backend (xUnit)**: reinstate (204/404, flips isRetired, award survives);
  remove-icon (204, hasIcon false, public icon 404s); list DTO returns
  grantedCount (active-only) + createdAt; admin teams search/detail shape +
  `PlatformAdmin` 401/403 for non-admins.
- **Frontend (`nx test web`)**: `recognition-admin.service` new methods hit the
  right routes/bodies; catalogue component list/filter/toggle + modal open/close +
  validation; extracted Assign picker grants for both player and team subjects.
- **e2e (`admin-area.spec.ts`)**: create → edit → set icon → retire → reinstate on
  the catalogue, and a team grant → visible on `/t/{slug}` → revoke round trip.

## Gate 7 — UI review

Instantiate `specs/014-admin-catalogue/checklists/ui-review.md` from
`.specify/templates/ui-review-checklist-template.md` and verify the catalogue,
modals, and team surfaces against DESIGN.md (warm tokens, sentence case, no emoji,
rounded, mobile-first, visible focus, empty/loading/error states) before sign-off.
