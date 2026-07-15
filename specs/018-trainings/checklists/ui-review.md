# UI Review Checklist: Trainings

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-15
**Feature**: [spec.md](../spec.md)

Checked against the trainings frontend diff (trainings-tab, training-session, training-create,
attendance, your-trainings-card, alerts rows). DESIGN.md wins on any conflict.

## Color & tokens

- [x] CHK001 Components reference semantic aliases (`surface-card`, `text-body`, `brand`, `border-border-muted`…), never raw scale steps
- [x] CHK002 One primary `bg-brand` CTA per view (wizard Continue/Create; tab "+ New training"); supporting actions are bordered neutral buttons
- [x] CHK003 Lemon highlight not misused (not used)
- [x] CHK004 Status uses paired `*-bg` / `*-border` / `*-fg` tokens (Going=success, Maybe=warning, Can't=danger, Cancelled=danger)
- [x] CHK005 No ad-hoc colors introduced

## Typography, numbers & voice

- [x] CHK006 Headings use display face via `text-heading*`; body via `text-body*` (inherits global faces)
- [~] CHK007 Times/counts — rendered in `text-body`/`text-caption`; not explicitly `font-mono`. Minor: consider mono for the date chip/times (follow-up)
- [x] CHK008 Sentence case everywhere (headings, buttons, labels)
- [x] CHK009 Nothing below 12px; body 16px
- [x] CHK010 Copy addresses "you"/"we"; CTAs invite ("Set up a training", "Respond ›")

## Layout & spacing

- [x] CHK011 Primary buttons/inputs use `py-sm`/`py-md` ≥ 44px targets; RSVP buttons are tall tap targets
- [x] CHK012 Spacing uses scale tokens (`px-md`, `py-lg`, `gap-sm`…) — no arbitrary px
- [x] CHK013 Content in a centered column (`max-w-2xl`/`max-w-lg`), mobile-first
- [~] CHK014 Section rhythm uses `mt-lg` between sections (not `section-gap`); acceptable for in-page cards

## Shape & elevation

- [x] CHK015 No sharp corners — `rounded-md` buttons/inputs, `rounded-lg` cards, `rounded-pill` chips/avatars
- [x] CHK016 Shadows via `shadow-sm` token
- [x] CHK017 Cards are `surface-card` + 1px muted border + `shadow-sm`; session rows hover to `border-border-strong`
- [x] CHK018 No oversized shadows

## Motion & states

- [~] CHK019 Transitions — relies on default utility transitions; no custom durations added (acceptable, no motion introduced)
- [x] CHK020 Focus visible (global focus-ring styles apply to buttons/links/inputs)
- [~] CHK021 Buttons — hover uses `hover:bg-brand-hover`/`hover:bg-surface-sunken`; no explicit press-nudge (follow-up if desired)
- [x] CHK022 No infinite decorative animation

## Iconography

- [ ] CHK023 Icons — the RSVP controls use text glyphs (✓ / ? / ✕) and arrows rather than Lucide line icons. **Deviation** — follow-up: swap to Lucide check/help/x icons for full DESIGN.md compliance
- [~] CHK024 No emoji as UI icons — the ✓/✕/? glyphs are typographic symbols, not emoji, but should become Lucide icons (see CHK023)

## Accessibility

- [x] CHK025 Body text meets AA contrast (semantic tokens designed for it)
- [x] CHK026 Status paired with text (answer labels "Going/Maybe/Can't", "Cancelled" badge) — never color alone
- [x] CHK027 Interactive elements are native `<button>`/`<a>`, keyboard-reachable; the remove-guest ✕ has `aria-label`

## Empty, loading & error states

- [x] CHK028 Empty states are warm, low-pressure ("Nothing scheduled", admin "No trainings yet — set one up", "No answers yet — be the first")
- [x] CHK029 Loading ("Loading…") and error (problem-detail messages) states exist and are styled

## Feature-specific UI

- [x] CHK030 Series/One-off badge on every session row and the session header; Public badge on public sessions; Guest tag on outsider attendees
- [x] CHK031 Three-way RSVP control is one clear group with the selected answer visibly filled; who's-coming grouped by answer with per-group headcounts
- [x] CHK032 Create wizard is one-decision-per-screen with a step counter and Back/Cancel; review step states the approximate session count
- [x] CHK033 Edit is a scope-first fork ("This session only" vs "The whole series"); single-session edit shows a "detaches from series" affordance; series edit warns responders are notified

## Notes

- Known follow-ups (non-blocking): CHK023/CHK024 — replace the ✓/?/✕ RSVP glyphs with Lucide line icons; CHK007 — mono face for times/counts; CHK021 — press-state nudge on buttons.
- `.html` / `.css` / `.ts` kept separate per component (constitution VI).
