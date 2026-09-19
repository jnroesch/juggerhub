# Quickstart: Verifying the Team Creation Wizard

## Prerequisites

The stack as usual — `docker compose up` for backend + database, and the web app served from
`frontend/`. Nothing about this feature needs new configuration, a new service, a new environment
variable or a migration. **If verification seems to need a database change, something was built
that this feature does not call for.**

You need two signed-in accounts: one to create teams with, and a second one to find in the invite
search.

## Automated checks

```bash
cd frontend
npm test          # nx test web --watch=false
npm run lint
npm run build
```

The specs that must be green, and what each is actually protecting:

| Spec | Protects |
|---|---|
| `team-create.component.spec.ts` | the step machine, the latch, the 409 recovery |
| `invite-search.component.spec.ts` | the relation switch and the optimistic flip |
| `invite-link.component.spec.ts` | create / copy / replace, and what happens when each fails |
| `team-invitations.component.spec.ts` | that extracting the search changed nothing on the existing screen |
| `catalog-parity.spec.ts` | that the new copy landed in **all three** catalogues |

The parity spec is the one that will bite: adding a key to `en.json` alone turns it red. Add to
`en.json`, `de.json` and `es.json` in the same change.

## Scenario 1 — The flow itself (US1)

1. Sign in, go to **`/teams/new`**.
2. **Expect**: one question group, not four. A progress indicator showing five steps.
3. Enter a name and a team address. **Expect**: the address is checked as you type, exactly as
   before — "checking…", then available/taken with the reason.
4. **Expect**: Continue is held while a check is in flight. Type a character and delete it again —
   **expect** it re-checks rather than sitting on a stale verdict.
5. Continue. **Expect**: the type step, progress advanced.
6. Choose **City team**. **Expect**: a city picker, and Continue held until a city is chosen.
7. Choose **Mixteam**. **Expect**: no city asked for, Continue available immediately.
8. Go **Back**. **Expect**: the name and address you typed are still there.
9. Forward to **review**. **Expect**: name, address, type and city all shown, each with a way back.
10. Press create. **Expect**: the team is created and you land on the logo step.

**The check that matters most**: create a team this way with both optional steps skipped, and
compare it against one created before this change. Name, address, type, city, your admin role,
member count — all identical (FR-027, SC-002).

## Scenario 2 — The address is taken between check and create (FR-009)

The one edge case worth engineering a test for, because it is the only path that crosses the latch
backwards.

1. In browser A, walk to the review step with the address `rheinfeuer`. **Do not press create.**
2. In browser B (the second account), create a team with the address `rheinfeuer`.
3. In browser A, press create.
4. **Expect**: you are returned to the **address step**, told the address is taken, with the name,
   type and city you entered still in place. Continue is held until a fresh check lands.
5. **Expect not**: a stranded review screen, a second team, or an English-only server sentence.

Change the address and finish. **Expect**: it works.

## Scenario 3 — The logo step (US2)

1. On the logo step after creating a team, choose an image.
2. **Expect**: it is applied and shown as the team's logo — not held as a preview.
3. Choose a *different* image. **Expect**: it replaces the first.
4. Continue, finish, and land on the team page. **Expect**: the logo is there, and on browse-teams,
   "My team" and the team chat header — everywhere feature 051 put it.
5. Create another team and **skip** the step. **Expect**: the letter placeholder, and a team
   otherwise identical.
6. Try a file that is refused (something far too large, or a `.txt` renamed). **Expect**: the step
   says so, the team is untouched, and you can try another or skip.

## Scenario 4 — The invite step (US3)

1. On the invite step, press **Create an invite link**. **Expect**: a link appears with a copy
   control and its expiry, and nothing was offered before the step's first read answered.
2. Copy it, then paste it somewhere. **Expect**: the pasted text is the link, and the control only
   said "Copied!" because it actually was. (Over plain `http` on a phone the clipboard API is
   unavailable — **expect** the refusal message there, not a false "Copied!".)
3. Open the link in a second browser as the second account. **Expect**: it admits them to the team.
4. Back on the step, press **New link**. **Expect**: a different link, and the previous one no
   longer admits anyone.
5. Type part of the second account's name or handle.
6. **Expect**: they are listed with an invite action.
7. **Expect**: a search matching nobody says so, rather than showing an empty area.
8. Invite them. **Expect**: the row changes to "invited" and cannot be invited again.
9. Search for **yourself**. **Expect**: shown as a member, with no invite action — you are the
   team's only member.
10. Finish. **Expect**: you land on the team page.
11. Go to the team's invitations screen. **Expect**: exactly the invitation you sent, pending — the
   same one that screen would have created (SC-005) — and the link you made last, shown as the
   team's link (SC-005a).

## Scenario 5 — Abandoning after creation (FR-013, SC-007)

1. Create a team and, on the **logo** step, close the tab.
2. Sign back in. **Expect**: the team exists, is yours, and is complete. You are its admin. It has
   the letter placeholder and no invitations.
3. **Expect**: the logo and invite controls are exactly where they always were, in team settings and
   the team's invitations screen — nothing about the team records that a wizard was abandoned.
4. Repeat, closing the tab on the **invite** step after sending one invitation. **Expect**: that one
   invitation is pending and nothing else differs.

## Scenario 6 — The existing screen still works (D4, D4a)

The invite search **and the invite link block** were lifted out of the team invitations screen into
shared components. Verify that screen directly:

1. Open an existing team's invitations screen.
2. **Expect**: the search behaves as it always did — same debounce, same rows, same relations, same
   invite action, and the pending list still refreshes after inviting someone.
3. **Expect one deliberate difference**: a search matching nobody now says so instead of rendering
   an empty list.
4. **Expect unchanged**: the link block in the aside — create, copy, "New link", the expiry line and
   the "anyone with it can accept" warning, all where they were.
5. **Expect two deliberate differences in that block**: nothing is offered until the first read
   answers (no flash of *Create an invite link* on a team that has one), and a copy that the browser
   refuses now says so instead of claiming success.
6. Revoke the **invite link** row in the pending list. **Expect**: the block beside it stops
   offering that link and returns to its create control — it no longer shows a link that admits
   nobody.

## Gate 7 — the UI review

Instantiate `checklists/ui-review.md` and verify it against the diff. DESIGN.md wins on conflict.
Check at least:

- **375px, German**, on the review step — the longest copy on the newest markup.
- The five progress knobs at 375px.
- The review step reading as a summary, not as a form.
- The skip on both optional steps reading as a choice, not as abandoning something unfinished.
- Empty, loading and error states on both optional steps.
