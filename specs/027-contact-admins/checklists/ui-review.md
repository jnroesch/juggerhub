# UI Review Checklist: Contact the Admins (027)

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is done.
**Created**: 2026-07-23
**Feature**: [spec.md](../spec.md)

**Scope of UI in this feature** (small, reuse-first):
- A **"Contact admins"** button on the team page ([team-detail.component.html](../../../frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html)) and the event page ([event-detail.component.html](../../../frontend/apps/web/src/app/features/events/event-detail/event-detail.component.html)).
- An **ADMINS** inbox tag ([chat-inbox.component](../../../frontend/apps/web/src/app/features/chat/chat-inbox/)) — a new case on the existing tag pill.
- The existing chat compose view reused in "inquiry mode" ([chat-compose.component](../../../frontend/apps/web/src/app/features/chat/chat-compose/)); no new layout.

## Color & tokens

- [x] CHK001 Semantic aliases only — reused `jhButton` variants and existing `surface-*`/`border-*`/`text-*` tokens; the ADMINS tag reuses the exact existing pill markup (`border-border-default … text-muted`)
- [x] CHK002 One coral CTA per view — team page keeps "Request to join"/sign-in as the primary; "Contact admins" is `variant="secondary"` (sage). Event page keeps "Manage"/join as primary; "Contact admins" is a neutral bordered button
- [x] CHK003 Lemon highlight untouched
- [x] CHK004 Status tokens unchanged (compose error reuses `danger-*`)
- [x] CHK005 No new colors

## Typography, numbers & voice

- [x] CHK006 No new type faces
- [x] CHK007 N/A — no new numeric fields
- [x] CHK008 Sentence case — "Contact admins", "New message · Admins"; tag renders UPPERCASE via the existing eyebrow style
- [x] CHK009 Nothing below caption
- [x] CHK010 Voice: "Contact admins" invites; copy addresses "you"; no emoji

## Layout & spacing

- [x] CHK011 Touch targets — team button is `jhButton` (≥44px); event button uses `py-md`; both ≥44px
- [x] CHK012 Spacing from scale tokens (`gap-sm`, `mt-sm`, `py-md`)
- [x] CHK013 No layout change; buttons sit in existing action containers
- [x] CHK014 N/A — no new sections

## Shape & elevation

- [x] CHK015 Radii from tokens (`rounded-md` buttons, `rounded-pill` tag)
- [x] CHK016–CHK018 No new shadows; reuse existing button/card treatments

## Motion & states

- [x] CHK019–CHK021 Buttons inherit `jhButton` / existing hover+press treatments; no new motion
- [x] CHK020 Focus rings inherited (`focus-visible:ring-focus`)
- [x] CHK022 No decorative loops

## Iconography

- [x] CHK023/CHK024 No new icons; no emoji

## Accessibility

- [x] CHK025 Text on tokens meets AA (reused muted-on-surface pairings already in use)
- [x] CHK026 The ADMINS tag pairs an icon-free but **text** label with the distinct name — status not by color alone
- [x] CHK027 Buttons are real `<button>`/`jhButton`, keyboard reachable, with `data-testid` and visible focus; the compose input has an `sr-only` label

## Empty, loading & error states

- [x] CHK028 Compose shows a warm start-of-conversation hint (reused)
- [x] CHK029 Compose has loading (`jh-loading`) and error (`compose-error`) states; "Contact admins" is simply hidden for admins/cancelled events rather than shown-then-erroring

## Feature-specific UI

- [x] CHK030 The **ADMINS** tag renders through the same pill component as TEAM/PARTY (`tagFor()` → `'Admins'`); the inbox row name is server-driven per viewer (team/event name for the requester, requester name for admins), so the two sides read correctly without client branching
- [x] CHK031 "Contact admins" is **hidden**, not disabled, for a viewer who is an admin of that team/event (FR-002), and for a cancelled event

## Notes

- `.html` / `.css` / `.ts` kept separate per component (constitution VI).
- No DESIGN.md conflicts found. The event-page "Contact admins" is a neutral bordered button (not sage `jhButton secondary`) to sit beside the sunken "Manage"/join block in that sidebar; consistent with neighbouring controls there.
- Avatar for inquiry rows falls back to the existing generic cluster (the tag carries the distinction); a bespoke event/team crest avatar could be a later polish but is not required by the spec.
