# UI Review Checklist: Chat

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-07-16
**Feature**: [spec.md](../spec.md)

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
- [ ] CHK020 Focus is always visible: 2px coral border + coral `focus-ring`
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

- [ ] CHK030 **Own message bubbles use coral `brand-primary`** with `text-on-accent`; others use `surface-muted` with `text-body`. The wireframe's blue is **not** adopted — see the conflict note below
- [ ] CHK031 Message times, unread badges, and the jump-pill count are set in the **mono** face (CHK007 applied to chat's numbers)
- [ ] CHK032 TEAM / PARTY inbox tags use the **eyebrow** style (the only sanctioned uppercase) as `pill`-radius chips, not ad-hoc uppercase labels
- [ ] CHK033 Bubbles are `md`-radius; avatars and tags `pill`; the composer input is `md` with ≥44px height and the coral focus ring
- [ ] CHK034 The conversation view has **one** coral CTA — the **send** button. Mute / hide / add / leave are secondary (`button-secondary` or ghost); **block and leave are `danger-fg`**, not coral (CHK002)
- [ ] CHK035 The inbox empty state is warm and low-pressure, offering a next step and mentioning the team chat that appears on joining a team (wireframe 9a; CHK028)
- [ ] CHK036 Unread badge display caps via the existing `badgeText()` helper ("9+") — the same rule as the Alerts bell, not a second convention
- [ ] CHK037 Typing indicator (`•••`) and read receipts convey state with **text/icon, never color alone** (CHK026); receipts read "Sent"/"Read", not a bare tick color
- [ ] CHK038 System lines are visually quiet — muted, centered, no bubble, clearly not a member message
- [ ] CHK039 Link cards are `card`-spec (white `surface-card`, 1px `border-muted`, `lg` radius, `sm` shadow) and carry **no action buttons** (view-only — FR-038)
- [ ] CHK040 Message bodies render as **plain text** — user content is never bound as HTML (FR-014; this is a security check as much as a design one)
- [ ] CHK041 Desktop rail / conversation / details reflow to mobile pushed screens with no layout-only behaviour change (FR-045)
- [ ] CHK042 Icons used (`send`, `plus`, `search`, `users`, `bell-off`, `eye-off`, `ban`, `trash-2`, `arrow-down`) are **Lucide line icons** at 16–22px (CHK023)

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.

### Conflicts reported (not silently resolved)

Per constitution Gate 7 and CLAUDE.md. Full reasoning in [research.md §11](../research.md).

1. **Own-bubble color — wireframe says blue, DESIGN.md says otherwise.** DESIGN.md makes coral
   `brand-primary` the primary, reserves `blue-*` for the `info` status token, and forbids introducing
   colors ad hoc. **Resolved toward DESIGN.md** (coral) with the product owner. No DESIGN.md amendment
   needed. → CHK030.
2. **Navigation — wireframe draws Home / Teams / Events / Chat / You.** The real nav (feature 008) is
   Home / Browse / My team / Alerts. **Real nav wins**; Chat is appended as a fifth destination and the
   wireframe's other tabs are not adopted.
3. **Link shapes — wireframe shows `/p/…`, `/e/…`.** Real routes are `/u/{handle}`, `/events/{id}`,
   `/trainings/sessions/{id}`, `/t/{slug}`. **Real routes win.**
4. **Emoji in the wireframe's sample messages** — that is *user-authored content*; DESIGN.md's "no
   emoji" rule governs product chrome, not what a player types. **No conflict** — recorded so it isn't
   re-litigated at review.
