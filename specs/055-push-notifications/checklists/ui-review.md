# UI Review Checklist: Push Notifications

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-19
**Feature**: [Link to spec.md]

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

- [ ] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`)
- [ ] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [ ] CHK003 Lemon `brand-highlight` is used only for small pops ("New" badges, streaks, dots) — never large fields
- [ ] CHK004 Status (success/danger/warning/info) uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [ ] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first

## Typography, numbers & voice

- [ ] CHK006 Headings/hero use the **Hubot Sans** display face; body and UI text use **Mona Sans**
- [ ] CHK007 Scores, stats, times, and counts are set in the **mono** face (tabular)
- [ ] CHK008 **Sentence case everywhere** (headings, buttons, labels, nav); UPPERCASE only as a styled eyebrow
- [ ] CHK009 Nothing meaningful drops below 12px (`caption`); body is 16px (`body-md`)
- [ ] CHK010 Copy addresses the reader as **"you"** / the community as **"we"**; CTAs invite, never shout; no emoji in product UI

## Layout & spacing

- [ ] CHK011 Interactive controls (buttons, inputs) have a **touch target ≥ 44px**
- [ ] CHK012 Spacing composes from the 4px scale tokens (`space-1`…`space-13`) — no arbitrary pixel values
- [ ] CHK013 Content sits in a centered column capped at `container-lg` (1100px); layout is mobile-first and reflows down
- [ ] CHK014 Section rhythm uses `section-gap` (`clamp(48px, 8vw, 112px)`)

## Shape & elevation

- [ ] CHK015 **No sharp corners** — radius matches element type (controls `sm`, buttons/inputs `md`, cards `lg`, media `xl`, chips/avatars `pill`)
- [ ] CHK016 Shadows are the warm-tinted `xs`…`xl` tokens (`rgba(64,46,24,…)`) — never pure black, never harsh
- [ ] CHK017 Cards are a white `surface-card` with a 1px muted border and soft `sm` shadow; they **lift 3px + deepen shadow on hover**
- [ ] CHK018 Larger shadows are reserved for elements that genuinely float above the page

## Motion & states

- [ ] CHK019 Transitions use the `fast`/`base`/`slow` durations (120/200/320ms) and token easings (`ease-out` entrances, `ease-bounce` for toggles)
- [ ] CHK020 Focus is always visible: a 2px `border-focus` ring (`ring-focus`), with a 2px offset on a filled control
- [ ] CHK021 Buttons darken a brand step + gain a colored glow on hover, and nudge down 1px / scale 0.99 on press
- [ ] CHK022 No infinite decorative animation loops in content

## Iconography

- [ ] CHK023 Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [ ] CHK024 No emoji used as UI icons

## Accessibility

- [ ] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [ ] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [ ] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [ ] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [ ] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [ ] CHK030 The preference matrix reads correctly with **four columns at the `md` breakpoint (768px) in German**, where the labels are longest. This is the binding layout case — NOT 375px, where the matrix is stacked cards and push is simply a fourth labelled row
- [ ] CHK031 The mobile card gains its fourth row without crowding, and each row's label and switch stay on one line at 375px
- [ ] CHK032 The device section renders one clear sentence in **each of its six states**: receiving, not enabled, blocked, needs-install, enabling, unsupported
- [ ] CHK033 The **blocked** state shows no control at all — a button that cannot work is worse than the sentence explaining where to change it
- [ ] CHK034 The iPhone install steps are numbered, readable at 375px, and mention that the installed app asks for a fresh sign-in
- [ ] CHK035 Only one coral CTA is present: the device section's enable button. Turning off uses the secondary variant
- [ ] CHK036 The device section uses the **card primitive**, not a hand-rolled surface (guarded by `card-surface.spec.ts`)
- [ ] CHK037 The Push switch carries the same `role="switch"`, `aria-checked` and `aria-label` shape as the two beside it
- [ ] CHK038 The always-on group still shows no toggle of any kind
- [ ] CHK039 A delivered notification's title and body are legible on a real lock screen in German, and neither is truncated mid-word at typical widths

## Verification status (2026-09-19)

Verified from rendered screenshots at 1280px, 768px and 375px in German, against the compose
stack, plus the automated design guards:

- **CHK030 PASS.** Four columns at `md` in German fit without wrapping: `IN-APP`, `E-MAIL`, `PUSH`
  all sit on one line above aligned switches.
- **CHK031 PASS.** At 375px the matrix is stacked cards with `In-App` / `E-Mail` / `Push` as three
  labelled rows. No crowding, nothing wraps. **This confirms the correction recorded in the spec:
  the four-column squeeze the issue predicted at 375px does not exist at any width.**
- **CHK032 PARTIAL.** The `blocked` state was rendered and reads correctly. `off`, `on`,
  `enabling`, `needs-install` and `unsupported` are covered by `push-device.service.spec.ts` but
  have not been seen on a real device — deferred to the device walk.
- **CHK033 PASS.** The `blocked` state renders its sentence and no control at all.
- **CHK034 DEFERRED.** Needs an iPhone.
- **CHK035 PASS.** One coral CTA (enable); turning off uses `variant="secondary"`.
- **CHK036 PASS.** Enforced by `card-surface.spec.ts`, which caught a hand-rolled surface in the
  first draft and now passes against the card primitive.
- **CHK037 PASS.** The Push switch copies the Email block's `role`, `aria-checked` and
  `aria-label` shape verbatim.
- **CHK038 PASS.** The always-on group renders a chip, no switch; asserted in
  `notification-settings.component.spec.ts`.
- **CHK039 DEFERRED.** Needs a real lock screen.

The standing DESIGN.md set above inherits from the existing settings page, which this feature
extends rather than restyles; the two new surfaces use only existing tokens, the card primitive and
the button directive.

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.
