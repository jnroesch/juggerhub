# UI Review Checklist: Onboarding Team Search

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-24
**Feature**: [spec.md](../spec.md)

**Scope of this review**: the `@case ('team')` block of
`frontend/apps/web/src/app/features/onboarding/onboarding.component.html`. No other screen changed.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`) — all classes are semantic (`text-ink`, `text-subtle`, `bg-surface-card`, `bg-surface-sunken`, `border-border-strong`, `text-brand`)
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view** — Continue is the only default `jhButton`; "Ask to join …" and "Try again" are both `variant="secondary"`
- [x] CHK003 Lemon `brand-highlight` used only for small pops — not used here
- [x] CHK004 Status uses the paired `*-bg` / `*-border` / `*-fg` tokens — both failure messages go through `jh-alert` (tone `danger` by default), which owns that token triple
- [x] CHK005 No new colors introduced ad hoc

## Typography, numbers & voice

- [x] CHK006 Display face for headings, Mona Sans for body — heading reuses the step's existing `text-heading-lg`; no font overrides added
- [x] CHK007 Counts set in the **mono** face — `<span class="font-mono">{{ team.playerCount }}</span>`, matching the browse rows
- [x] CHK008 **Sentence case everywhere** — "Find your team", "Search teams…", "Ask to join Berlin Jugger", "Try again", "Asking…"
- [x] CHK009 Nothing meaningful below `caption`; body is `body-md` — the "Beginners"/"Asked" pills use `text-caption` (12px), which is the floor, not below it
- [x] CHK010 Reader addressed as "you", community as "we"; CTAs invite; no emoji — "We've asked Berlin Jugger to let you in — an admin still has to say yes."

## Layout & spacing

- [x] CHK011 Touch target ≥ 44px — `min-h-11` (44px) on both the search input and every result row; `jhButton` supplies its own
- [x] CHK012 Spacing composes from the scale tokens — `mt-lg`, `mt-sm`, `gap-sm`, `px-xs`, `py-sm`; no arbitrary pixel values
- [x] CHK013 Mobile-first, reflows down — the block sits inside onboarding's existing `max-w-sm` column and adds no fixed widths
- [x] CHK014 Section rhythm — N/A; this is a step within an existing flow, not a page with sections

## Shape & elevation

- [x] CHK015 **No sharp corners** — search field `rounded-md` (input), rows `rounded-sm` (inset control), initial chip `rounded-md`, pills `rounded-pill`
- [x] CHK016 Warm-tinted shadows — none added
- [x] CHK017 Card treatment — N/A; rows are list items separated by `border-b`, matching the browse list, not cards
- [x] CHK018 Larger shadows reserved for floating elements — none added

## Motion & states

- [x] CHK019 Token durations/easings — `transition-colors duration-fast` on rows, `transition-shadow duration-fast` on the search field
- [x] CHK020 Focus always visible — `focus-within:border-brand focus-within:ring-2 focus-within:ring-focus` on the search field; `focus-visible:ring-2 focus-visible:ring-focus` on rows; `jhButton` carries its own
- [x] CHK021 Button hover/press behaviour — inherited from `jhButton`; not reimplemented
- [x] CHK022 No infinite decorative loops — `jh-loading` is a text line, not an animation

## Iconography

- [x] CHK023 **Lucide line icons only** — the search glyph uses `<jh-icon name="search" [size]="18">` (the shared feature-024 primitive) rather than the hand-inlined SVG that the older `browse-shell` still carries. No new ad-hoc SVG was added.
- [x] CHK024 No emoji as UI icons

## Accessibility

- [x] CHK025 WCAG AA body contrast — `text-ink` / `text-subtle` on `surface-card`, the tuned sand ramp; no new pairings invented
- [x] CHK026 Status never by color alone — every state carries text; the "Beginners" and "Asked" pills are labelled, not colour-coded
- [x] CHK027 Keyboard-reachable with visible focus and labels — rows are real `<button>`s; the search input has `aria-label="Search teams by name"`; selection state is exposed via `aria-pressed`; `jh-loading` announces through its own `role="status"`; `jh-alert` through `role="alert"`

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step — "No teams match that — try a different name." and "No teams are looking for new players right now — search by name to find any team."
- [x] CHK029 Loading and error states exist and are system-styled — `jh-loading` (never a spinner, with a "Still looking…" patient line) and `jh-alert` + a secondary "Try again"

## Feature-specific UI

- [x] CHK030 Result rows are visually identical to the teams browse list — initial chip, name, `city ·` mono player count, "Beginners" pill (compare `browse-teams.component.html:20-41`)
- [x] CHK031 Empty and error are **visibly and verbally distinct**: only the error carries `jh-alert` + a retry action; the empty states carry neither
- [x] CHK032 The pending-request confirmation never implies membership — "an admin still has to say yes"; asserted by test, not just by reading
- [x] CHK033 Continue and "I'm not on a team yet" are never `[disabled]` in any state, and neither is bound to any team state
- [x] CHK034 No feature-004 placeholder artefact survives — the search field is enabled, and no sample team or "coming soon" copy remains (asserted by test)

## Notes

- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): `.html` / `.css` / `.ts`
  stay separate — this feature edited the existing `.ts` and `.html` and added no styles.
- **Standing conflict, not introduced here**: DESIGN.md requires ≥4.5:1 body contrast while the
  primary button is specified as white on `coral-4` (3.14:1). Every primary button in the app is
  affected. This step adds **no** new primary button — Continue already existed — so the conflict is
  neither worsened nor resolved here. It remains the owner's brand decision.
