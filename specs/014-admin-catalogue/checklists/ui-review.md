# UI Review Checklist: Admin catalogue management

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)

Verified against the diff for the catalogue (`features/admin/catalogue/*`), the shared
Assign picker (`features/admin/shared/*`), and the teams area (`features/admin/teams/*`,
`features/admin/team-detail/*`, shell nav). DESIGN.md is the source of truth.

## Color & tokens

- [x] CHK001 Components reference semantic aliases (`surface-card`, `text-body`, `bg-brand`, `border-border-muted`, `bg-warning-bg`…) — no raw scale steps
- [x] CHK002 One coral `bg-brand` CTA per view (catalogue → "New type"; team detail → "Assign"; picker → "Grant"); row actions + retire use bordered/`warning` styles
- [x] CHK003 Lemon `brand-highlight` not used (no large fields)
- [x] CHK004 Status uses paired tokens: Active `success-bg/fg`, Retired `warning-bg/fg`, errors `danger-bg/border/fg`
- [x] CHK005 No new colors introduced — existing tokens only

## Typography, numbers & voice

- [x] CHK006 Headings use the display face (`text-h3/h4`); body/UI text uses the body face (inherited globally)
- [x] CHK007 Counts (grant count, member/award counts, preview sizes) set in the mono face (`font-mono`)
- [x] CHK008 Sentence case everywhere ("New type", "Create type", "Save changes", "Retire type", "Applies to", "Badges")
- [x] CHK009 Nothing below 12px (`text-caption`); body is `body-md`
- [x] CHK010 Warm, inviting copy ("Find a team to assign…", "Create the first one…"); plain-spoken retire confirm; no emoji

## Layout & spacing

- [x] CHK011 Primary CTAs and inputs are ≥44px (`h-11`). *Note:* dense in-table row actions (Edit/Icon/Retire/Reinstate) are compact, intentionally matching the existing admin tables (`admin-user-detail` revoke) — see Notes.
- [x] CHK012 Spacing composes from the 4px scale tokens (`gap-sm/md`, `px-md`, `py-sm`, `mt-md`)
- [x] CHK013 Content sits in the admin shell's centered `container-lg` column; mobile-first (table → cards at `md`)
- [x] CHK014 N/A — admin pages are dense data surfaces inside the shell; no marketing `section-gap` rhythm

## Shape & elevation

- [x] CHK015 No sharp corners — `rounded-md` (controls/inputs), `rounded-lg` (cards), `rounded-pill` (chips/toggle), icon frame `rounded-full` (badge) / `rounded-md` (achievement)
- [x] CHK016 Shadows use warm tokens (`shadow-sm`, `shadow-xl` on modals, `shadow-coral` on CTAs)
- [x] CHK017 Cards are white `surface-card` + 1px muted border + soft `sm` shadow; mobile team cards lift on hover (`hover:shadow-md`)
- [x] CHK018 Larger shadow (`shadow-xl`) reserved for the floating modals

## Motion & states

- [x] CHK019 Transitions use `duration-fast` + `transition-colors`
- [x] CHK020 Focus visible: inputs use `focus-visible:border-border-focus` + `focus-visible:ring-2 focus-visible:ring-focus`
- [x] CHK021 Buttons darken a brand step + gain a coral glow on hover (global button styling reused)
- [x] CHK022 No infinite decorative animation loops

## Iconography

- [x] CHK023 Lucide line icons only (plus, search, close, medal, upload, group), 16–22px, `currentColor`
- [x] CHK024 No emoji as UI icons

## Accessibility

- [x] CHK025 Body text uses the AA-tuned sand ramp on `surface-card`; amber retire uses the `warning` pair
- [x] CHK026 Status never by color alone — "Active"/"Retired" carry text labels
- [x] CHK027 Native `<button>`/`<input>` with labels; modals `role="dialog"` + `aria-modal` + `aria-label`; close buttons have `aria-label`; Escape closes each modal
- [x] CHK028 Empty states offer a next step ("Create the first one with New type"; "Try a different name or city")
- [x] CHK029 Loading and error states are styled (loading text + `danger` alert with "Try again")

## Feature-specific UI

- [x] CHK030 Catalogue toggle is a segmented `pill` control; status filter uses `pill` chips (matching the admin users filter)
- [x] CHK031 Desktop table folds into mobile cards; retired rows are de-emphasised (`opacity-60`) and carry a text "Retired" pill
- [x] CHK032 Icon preview renders at 32/40/56 px, masked to a circle (badge) or rounded square (achievement) — matching how icons appear elsewhere
- [x] CHK033 Retire confirm is amber (`warning`), not red — it is reversible; the copy spells out exactly what changes
- [x] CHK034 The shared Assign picker is visually identical for player and team subjects (same modal, tabs, "Given" marker)

## Notes

- **CHK011 (touch targets)**: primary CTAs (`New type`, `Assign`, `Grant`, `Save`, pager, search) and all inputs use `h-11` (44px). Compact row-action buttons in the catalogue table/cards (`px-sm py-xs text-caption`) are below 44px, matching the established admin pattern (`admin-user-detail` revoke, `admin-users` filters). Consistent with the existing admin surface; if the design later wants larger mobile row actions, raise it against DESIGN.md.
- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): every component keeps `.html` / `.css` / `.ts` separate.
- No conflicts with DESIGN.md were found.
