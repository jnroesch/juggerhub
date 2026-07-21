# Quickstart / Validation: "My team" home for teamless players

End-to-end checks that prove the feature works. See [contracts/me-invitations.md](./contracts/me-invitations.md) and [data-model.md](./data-model.md) for shapes; this is a run/validation guide, not implementation.

## Prerequisites

- Full stack up locally: `docker compose up` (backend, frontend, Postgres, Mailpit) from the repo root.
- Two accounts: **A** (will be teamless) and **B** (a team admin who can invite A).

## Backend checks (xUnit)

Run the API integration suite:

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
```

New/added cases to expect green:

1. **Auth required** — anonymous `GET /api/v1/profiles/me/invitations` → `401`.
2. **Scoped to caller** — with a targeted invite for A and one for another user, A's list contains only A's invite.
3. **Targeted + usable only** — a link invite, an expired targeted invite, and an accepted/declined/revoked targeted invite for A are all excluded; only the pending, unexpired targeted invite is returned.
4. **Pagination** — `take` is bounded (>100 normalized to 20); `totalCount` reflects the full usable set.
5. **List → accept round-trip** — an invite returned by `me/invitations` can be accepted via `POST /api/v1/invitations/{token}/accept` (→ `200` + slug), after which it no longer appears in `me/invitations` and A has a membership.

## Frontend checks

```powershell
# from frontend/
npx nx test web
```

- `nav-model.spec.ts`: `myTeamTarget([])` returns `/my-team` (updated from `/browse/teams`); single/many-team cases unchanged.
- `my-team.component.spec.ts`: renders (a) find + create with no invites section when teamless with no invites, (b) an invites list when teamless with invites, (c) the existing "Your teams" chooser when on ≥1 team; accept refreshes memberships + navigates to `/t/{slug}`; decline removes the row.

## Manual walkthrough

1. Sign in as **A** (on zero teams). In the top bar (desktop) **and** the bottom bar (mobile width), click **My team**.
   - **Expect**: you land on `/my-team`, the "My team" destination is highlighted (not Browse), and you see a "not on a team yet" home with **Find a team** and **Create a team** actions. *(US1 / FR-001, FR-002, FR-004–FR-007)*
2. Click **Find a team** → lands on Browse teams. Back, then **Create a team** → lands on the create form. *(FR-005–FR-007)*
3. As **B**, send A a targeted invite to team T (existing admin invite flow).
4. As **A**, reload `/my-team`.
   - **Expect**: an invitation card for T showing the team name, type/city, and B as inviter, with **Accept** and **Decline**. *(US2 / FR-010, FR-011)*
5. Click **Accept**.
   - **Expect**: you join T and are navigated into `/t/{T}`; returning to **My team** now resolves to a team, not the empty state. *(US3 / FR-012, FR-017, FR-018)*
6. Repeat with a second invite and click **Decline**.
   - **Expect**: the card disappears and you do not join. *(FR-013)*
7. Stale case: let an invite expire (or have B revoke it), then click **Accept**.
   - **Expect**: a friendly message and the stale card is removed — no raw error. *(FR-015)*
8. Confirm players on **one** team go straight to their team space and players on **many** see the existing chooser — both unchanged. *(FR-003, SC-006)*

## Success criteria mapping

| Check | Success Criteria |
|---|---|
| Step 1 (single click to actionable home) | SC-001 |
| Steps 1–2, 4 (three paths visible) | SC-002 |
| Steps 4–5 (open → accept ≤ 2 interactions) | SC-003 |
| Backend cases 2–3 (only own, usable invites) | SC-004 |
| Step 7 (stale never errors) | SC-005 |
| Step 8 (multi-team unchanged) | SC-006 |
