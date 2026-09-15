# UI Review Checklist: Tournament Results

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-15
**Feature**: [spec.md](../spec.md)

**How it was run**: against the diff, and in a real Chromium on the rebuilt Docker stack (backend and frontend images rebuilt from this branch), **in German, at 375 px and 1280 px**, with screenshots of every new surface. That includes a link to and import from the **real tugeny.org** (25th German championship: 20 teams, 78 matches, 9 draws). The walk found five problems, all fixed and re-shot before this list was filled (see *Fixed during the walk*).

**Surfaces**:
- **Event page**: results card (`features/events/event-detail/components/event-results.component.*`)
- **Results page**: `features/events/event-results/*` (editor, paste, Tugeny card, team list)
- **Team page**: placements card (`features/teams/team-detail/placements/*`)
- **Admin**: queue and team picker (`features/admin/results/*`, `features/admin/shared/team-picker.component.*`) and the admin nav
- **Ended events**: `join-actions`

## Color & tokens

- [x] CHK001 Semantic aliases only (`surface-card`, `surface-sunken`, `text-body`, `text-heading`, `text-subtle`, `border-border-strong`, status tokens). *Note*: the picker backdrop `bg-black/40` is copied verbatim from the existing `assign-picker` dialog. It is a pre-existing pattern, not a new colour.
- [x] CHK002 One coral CTA per view. The results page has exactly one (**Save ranking**), asserted by a spec. Every other action there (paste, link, import, copy, clear) is secondary or ghost. The event card, team card and admin queue have none.
- [x] CHK003 Lemon not used.
- [x] CHK004 Status via `jh-alert` tones (danger/warning/info/success) and `text-success-fg`.
- [x] CHK005 No new colours.

## Typography, numbers & voice

- [x] CHK006 Headings use the existing display classes; body text uses Mona Sans.
- [x] CHK007 Numbers use mono or tabular figures:
  - Placement badges, per-set scores (`5 5 2 5`), the position inputs and the admin total are **mono**.
  - The phrase "Platz 3 von 20" uses **tabular numerals in the body face**. The first walk set the whole phrase, words included, in mono, which reads as code. Mono is for numbers, not sentences.
- [x] CHK008 Sentence case throughout. "ERGEBNISSE" is the styled eyebrow, matching the neighbouring event cards.
- [x] CHK009 Nothing below `caption` (12 px).
- [x] CHK010 Warm "du" voice, verified in German ("Trag für jedes Team seinen Endplatz ein", "Verknüpf es hier…"); no emoji.

## Layout & spacing

- [ ] CHK011 Touch targets ≥ 44 px: **partially, and this is a pre-existing conflict, reported**.
  - Inputs and selects are `h-11`, and the row's remove button is 44×44.
  - The `jhButton size="sm"` buttons (add a place, paste, link, import, copy, connect) are `min-h-9` = **36 px**. That is the shared primitive's `sm` size, used the same way across the app (e.g. `event-manage`, the admin catalogue).
  - DESIGN.md says ≥ 44 px for controls, so the `sm` button size conflicts with DESIGN.md app-wide. It is not introduced here; resolving it is a design-system decision.
- [x] CHK012 Spacing from the scale tokens; `h-7`/`w-6` sit on the 4 px scale.
- [x] CHK013 Centred `container-md`/`container-lg` column, mobile-first. At 375 px every card stacks.
- [x] CHK014 Page rhythm is the same as the sibling event pages (`py-lg`); no new section gaps.

## Shape & elevation

- [x] CHK015 Radii: controls `md`, cards `lg`, badges and toggles `pill`, dialog `xl`.
- [x] CHK016 Token shadows only (`shadow-sm`, dialog `shadow-xl`).
- [x] CHK017 Cards are `jh-card` without `interactive`, like every non-clickable card on these pages. They don't lift, correctly.
- [x] CHK018 Only the picker dialog floats.

## Motion & states

- [x] CHK019 `duration-fast` transitions.
- [x] CHK020 Visible focus: inputs and selects `focus:ring-focus`, remove button `focus-visible:ring-2 ring-focus`, `jhButton` built in.
- [x] CHK021 Buttons via `jhButton`.
- [x] CHK022 No looping animation.

## Iconography

- [x] CHK023 Lucide line icons: `trophy` (admin nav, 16/22 px), `x` (remove, dialog close), `search`.
- [x] CHK024 No emoji.

## Accessibility

- [ ] CHK025 Contrast: **inherits the open, app-wide DESIGN.md conflict** (memory: *DESIGN.md contrast conflict*). The one primary button, "Platzierung speichern", is white on coral-4, 3.14:1, like every primary button in the product. Nothing new is below AA. Reported, not resolved here.
- [x] CHK026 Never colour alone:
  - The winner has a "Turniersieg" label.
  - A draw says "unentschieden".
  - A match winner is set in **bold**, not colour.
  - Connected rows say "Von einem JuggerHub-Admin mit diesem Team verknüpft".
- [x] CHK027 Every input and select has an `sr-only` label. The remove button has `aria-label="Zeile N entfernen"` plus a title. The picker is `role="dialog" aria-modal` and closes on Esc (spec). The admin filter toggles use `aria-pressed`.

## Empty, loading & error states

- [x] CHK028 Empty states are low-pressure: "Noch hat kein Team einen bestätigten Platz.", "Für dieses Team sind noch keine Turnierergebnisse eingetragen.", "Alle Platzierungen sind verknüpft. Gerade gibt es nichts zu prüfen."
- [x] CHK029 `jh-loading` for loading; `jh-alert` plus "Erneut versuchen" for errors. **Error never renders as empty** on the event card, team card or results page (asserted in specs).

## Feature-specific UI

- [x] CHK030 **Fifth admin tab at 375 px in German** (research R15): "Übersicht · Nutzer · Teams · Katalog · Ergebnisse" fits the bottom bar without truncation (shot `25-admin-bottom-nav-375`). No fallback under Teams was needed.
- [x] CHK031 **Ranking editor at 375 px**: team names and "Name eingeben" are fully readable (shot `40-results-editor-375-fixed`).
- [x] CHK032 **Ended events offer nothing the server refuses**:
  - no Join or Enter-party button; "Dieses Event ist vorbei." instead
  - no mercenary board (shots `27`, `42`)
- [x] CHK033 **Two teams with the same name** are told apart by address in the picker (`/t/rigor-…` vs `/t/rigor2-…`, shot `10`). That is exactly why a name is never matched automatically.
- [x] CHK034 Imported data is shown as Tugeny has it: stage names ("Group 1") stay in Tugeny's language inside the German page. That is data, not UI copy, and "K.-o.-Runde" is our own heading. The Tugeny menu item "Export Ranking for JTR" is quoted verbatim in the paste hint so organizers can find it.
- [x] CHK035 Scores: one row per side with its per-set points, which stays readable at 375 px (shot `32-matches-final-375`). Rows with fewer sets right-align rather than column-align across rows; accepted.

## Fixed during the walk

1. **375 px ranking editor was unusable**: the team select was about 90 px wide ("Rigor", "Eclips", "Stattc") and the typed name was clipped. Fixed:
   - the text "Entfernen" button became a 44×44 icon button (accessible name kept)
   - the select padding was tightened
   - the position field narrowed from 64 to 56 px
   - the option label was shortened ("Stattdessen einen Teamnamen eingeben" → "Name eingeben")
2. **Past tournament still showed the mercenary board**, including an English server sentence inside the German page ("This event isn't accepting the marketplace."). FR-023 says a past event opens no board, so it is now hidden once an event has ended. The English sentence itself is pre-existing server-built prose in the marketplace (`MarketListingService.cs:188` and two siblings), already tracked as **#179** ("API error messages are English-only in all three languages").
3. **Team list for Tugeny showed on a finished tournament**, where it is pointless. It is hidden once the event has ended.
4. **"Platz N von M" was fully mono**; it now uses tabular numerals in the body face.
5. The "type a name" option label was shortened in all three languages (part of 1).

## Notes

- `.html` / `.css` / `.ts` are separate for every new component (constitution VI).
- DESIGN.md conflicts reported, not resolved: CHK011 (`sm` button height) and CHK025 (primary-button contrast). Both are app-wide and pre-existing.
