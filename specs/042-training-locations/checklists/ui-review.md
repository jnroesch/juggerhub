# UI Review Checklist: Structured Locations for Trainings

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-08-04
**Feature**: [spec.md](../spec.md)

**Surfaces in scope**: `jh-address-fields` (new shared component), the training create wizard's
"Where" + review steps, the training edit series form, the training edit single-session form, the
trainings tab row, and the training session detail. The dashboard agenda card is unchanged (it
already renders `locationLabel`).

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification — not a spec-quality gate like `requirements.md`. Copy this
template into `specs/<feature>/checklists/ui-review.md` for any feature that ships UI,
then check each item against the diff, recording `file:line` for anything that fails.
[DESIGN.md](../../DESIGN.md) is the source of truth: if a check ever conflicts with it,
DESIGN.md wins and the conflict is reported rather than silently resolved.

<!--
  Items below are the standing DESIGN.md compliance set — they are the SAME for every
  feature because they enforce the design system, not feature requirements. Keep them
  in sync with DESIGN.md: when a token, rule, or component spec changes there, update
  this template. Add feature-specific UI items (e.g. "badge grid uses pill chips") to
  the last section per feature.
-->

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`)
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [x] CHK003 Lemon `brand-highlight` is used only for small pops ("New" badges, streaks, dots) — never large fields
- [x] CHK004 Status (success/danger/warning/info) uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [x] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first

## Typography, numbers & voice

- [x] CHK006 Headings/hero use the **Hubot Sans** display face; body and UI text use **Mona Sans**
- [x] CHK007 Scores, stats, times, and counts are set in the **mono** face (tabular)
- [x] CHK008 **Sentence case everywhere** (headings, buttons, labels, nav); UPPERCASE only as a styled eyebrow
- [x] CHK009 Nothing meaningful drops below 12px (`caption`); body is 16px (`body-md`)
- [x] CHK010 Copy addresses the reader as **"you"** / the community as **"we"**; CTAs invite, never shout; no emoji in product UI

## Layout & spacing

- [x] CHK011 Interactive controls (buttons, inputs) have a **touch target ≥ 44px**
- [x] CHK012 Spacing composes from the 4px scale tokens (`space-1`…`space-13`) — no arbitrary pixel values
- [x] CHK013 Content sits in a centered column capped at `container-lg` (1100px); layout is mobile-first and reflows down
- [x] CHK014 Section rhythm uses `section-gap` (`clamp(48px, 8vw, 112px)`)

## Shape & elevation

- [x] CHK015 **No sharp corners** — radius matches element type (controls `sm`, buttons/inputs `md`, cards `lg`, media `xl`, chips/avatars `pill`)
- [x] CHK016 Shadows are the warm-tinted `xs`…`xl` tokens (`rgba(64,46,24,…)`) — never pure black, never harsh
- [x] CHK017 Cards are a white `surface-card` with a 1px muted border and soft `sm` shadow; they **lift 3px + deepen shadow on hover**
- [x] CHK018 Larger shadows are reserved for elements that genuinely float above the page

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations (120/200/320ms) and token easings (`ease-out` entrances, `ease-bounce` for toggles)
- [x] CHK020 Focus is always visible: 2px coral border + coral `focus-ring`
- [x] CHK021 Buttons darken a brand step + gain a colored glow on hover, and nudge down 1px / scale 0.99 on press
- [x] CHK022 No infinite decorative animation loops in content

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [x] CHK024 No emoji used as UI icons

## Accessibility

- [ ] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface — **body text passes; the `jhButton` primary CTA does not.** This is the known app-wide DESIGN.md conflict (white-on-coral-4 ≈ 3.14:1), not something 042 introduces: the feature's Continue/Save buttons inherit the existing `jhButton`. Reported, not silently resolved — see Notes.
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [x] CHK030 `jh-address-fields` inputs match the styling of the event wizard's address inputs (`features/events/event-create/event-create.component.html`) — same border, radius, padding and focus treatment
- [x] CHK031 The address group appears **only** when the location kind is in-person; switching to virtual removes it entirely rather than disabling it
- [x] CHK032 The city field's "temporarily unavailable" and "no matches" states are styled to the system, not raw text (inherited from `jh-city-picker`)
- [x] CHK033 Validation messages for missing street / postal code / city are specific about which field is missing, in sentence case, addressing the reader as "you"
- [x] CHK034 The create wizard's review step shows venue, street, postal code and the city's display label, with a neutral placeholder (not a blank) where a value is absent
- [x] CHK035 The trainings-tab row and session detail show the location label without a dangling separator when the venue name is absent
- [x] CHK036 Edit forms pre-fill the currently selected city visibly (the chip reads back), so an admin can tell whether they are changing it or leaving it
- [x] CHK037 All three forms keep `.ts` / `.html` / `.css` separate (constitution VI); no template or styles inlined into the component decorator

## Notes

### Pre-existing finding (not introduced by 042)

The Tailwind spacing scale in `frontend/apps/web/tailwind.config.js:144-153` defines only
`xs · sm · md · lg · xl · 2xl · 3xl · section-gap`. It has **no `2xs` or `3xs`**, yet several
existing templates use `mt-2xs`, `gap-2xs` and `py-3xs` — those classes emit no CSS at all, so the
spacing silently falls back to zero. Examples: `features/trainings/trainings-tab/trainings-tab.component.html`
(`py-3xs` on the series/one-off and cancelled pills).

042 does not introduce any new instance (the new `jh-address-fields` uses `mt-xs`/`gap-md`), and
fixing the existing ones is out of scope for this feature. Worth its own small issue: either add
`2xs`/`3xs` to the scale or replace the usages.

### Open DESIGN.md conflict (CHK025, pre-existing and app-wide)

DESIGN.md requires ≥ 4.5:1 for body text, but specifies the primary button as white on coral-4,
which measures ≈ 3.14:1. Every primary CTA in the product is therefore non-conforming, including
this feature's "Continue" and "Save" buttons — they use the shared `jhButton` directive unchanged.

042 neither introduces nor fixes this. Per the checklist rule, DESIGN.md wins and the conflict is
**reported rather than silently resolved**: picking a darker coral here would make the training
forms inconsistent with every other form in the app. It is a brand-level decision for the owner.

### General

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.
