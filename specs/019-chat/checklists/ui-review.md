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

- [x] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface — *measured: own bubble 5.71:1, their bubble 9.17:1. Initially **failed at 3.14:1**; see the finding below*
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles — *measured: 13/13 focusable stops show a focus indicator; tab order reaches nav → new chat → search → conversation → details → delete → composer*

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [x] CHK030 **Own message bubbles use coral** with `text-on-accent`; others use `surface-muted` with `text-body`. The wireframe's blue is **not** adopted — see the conflict note below. *Uses `brand-active` (coral-6) rather than `brand-primary` (coral-4): coral-4 fails WCAG AA for body text at 3.14:1 — see the contrast finding*
- [x] CHK031 Message times, unread badges, and the jump-pill count are set in the **mono** face (CHK007 applied to chat's numbers)
- [x] CHK032 TEAM / PARTY inbox tags use the **eyebrow** style (the only sanctioned uppercase) as `pill`-radius chips, not ad-hoc uppercase labels
- [x] CHK033 Bubbles are `md`-radius; avatars and tags `pill`; the composer input is `md` with ≥44px height and the coral focus ring
- [x] CHK034 The conversation view has **one** coral CTA — the **send** button. Mute / hide / add / leave are secondary (`button-secondary` or ghost); **block and leave are `danger-fg`**, not coral (CHK002)
- [x] CHK035 The inbox empty state is warm and low-pressure, offering a next step and mentioning the team chat that appears on joining a team (wireframe 9a; CHK028)
- [x] CHK036 Unread badge display caps via the existing `badgeText()` helper ("9+") — the same rule as the Alerts bell, not a second convention
- [x] CHK037 Typing indicator (`•••`) and read receipts convey state with **text/icon, never color alone** (CHK026); receipts read "Sent"/"Read", not a bare tick color
- [x] CHK038 System lines are visually quiet — muted, centered, no bubble, clearly not a member message
- [x] CHK039 Link cards are `card`-spec (white `surface-card`, 1px `border-muted`, `lg` radius, `sm` shadow) and carry **no action buttons** (view-only — FR-038)
- [x] CHK040 Message bodies render as **plain text** — user content is never bound as HTML (FR-014; this is a security check as much as a design one)
- [x] CHK041 Desktop rail / conversation / details reflow to mobile pushed screens with no layout-only behaviour change (FR-045)
- [x] CHK042 Icons used (`send`, `plus`, `search`, `users`, `bell-off`, `eye-off`, `ban`, `trash-2`, `arrow-down`) are **Lucide line icons** at 16–22px (CHK023)

## Notes

### How these were verified (2026-07-16)

Be precise about method, because "checked" means different things:

- **Measured in a running browser** (Playwright against the docker-compose stack, signed in as a real
  seeded player): CHK030 (own bubble computed `background-color` = `rgb(245, 98, 58)` = coral-4 =
  `brand-primary` — see the finding below), CHK034 (send is the only coral CTA in the conversation;
  block/leave render `danger-fg`), CHK037 (receipt reads the literal text "Sent"), CHK041 (the shell
  reflows to a single pushed screen at 390px and to rail+pane at 1440px), CHK028/CHK035 (the warm
  empty state renders with its next step and the team-chat hint).
- **Verified by construction**: CHK001/CHK005 (every class is a semantic alias from
  `tailwind.config.js`; there are no raw scale steps and no new colors — see the finding below, which
  is exactly how a non-token slipped through), CHK007/CHK031 (`font-mono` on times, badges and the
  jump count), CHK008 (sentence case throughout), CHK011 (`min-h-11` on every control), CHK015 (radius
  per element type), CHK023/CHK042 (Lucide line icons, 16–22px, `currentColor`), CHK040 (bodies are
  `{{ }}` interpolation — never `[innerHTML]`), CHK036 (`badgeText()` reused, not re-implemented).
- **Inherited from the design system**: CHK006, CHK009, CHK012–CHK014, CHK016–CHK022, CHK024 — these
  follow from using the shared tokens and the same component patterns as the existing screens; no
  bespoke CSS was added (every `.css` file in the feature is a comment).
- **Measured** (a11y pass, T091): CHK025 — contrast ratios computed from *rendered* computed styles,
  not from the palette on paper. CHK027 — driven with a real keyboard: 13/13 focusable stops carry a
  visible focus indicator. (`send` is correctly absent from the tab order while `[disabled]` on an
  empty draft, and joins once there is text; Enter submits the form regardless.)

### Finding: CHK025 failed — and DESIGN.md contradicts itself

The own-message bubble first shipped as `brand-primary` (coral-4) with a white label, straight from
DESIGN.md's own button spec. Measured, that is **3.14:1** — below the **≥4.5:1 for body text** that
DESIGN.md's *own* Do's and Don'ts require. Fixed here by using **`brand-active` (coral-6, 5.71:1)** —
an existing token in the same family, so no new color and no amendment, and the bubble still reads
coral. Full table in [research.md §13](../research.md).

**Reported, not resolved — this is bigger than chat**: DESIGN.md's component spec says the primary
button is *"coral `brand-primary` background, white label"*, which is the same 3.14:1 its
accessibility rule forbids. **Every primary button in the app is therefore at 3.14:1.** Changing the
brand's primary button colour is a decision for the product owner across every screen, not something
this feature should do unilaterally. Chat fixed only its own surface, where the failure matters most
because a bubble is long-form text rather than a two-word label.

### Finding: CHK001/CHK030 failed on first build, now fixed

The own-message bubble rendered **transparent** — `bg-brand-primary` is not a real class in this
project. `tailwind.config.js` maps `brand: var(--brand-primary)`, so the class is **`bg-brand`**; the
invented name silently produced no background at all. The same mistake hit `bg-brand-primary-hover`
(→ `bg-brand-hover`) and a `2xs` spacing step that does not exist (the scale starts at `xs`).

Worth recording because of *how* it was caught: every unit test passed, the production build passed,
and lint passed. Only driving the real page and reading the computed style exposed it. An unknown
Tailwind class fails silently by design — there is no error, just a missing rule — so a coral CTA and
a transparent one are indistinguishable to everything except a browser.

### Conventions

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.

### Conflicts reported (not silently resolved)

Per constitution Gate 7 and CLAUDE.md. Full reasoning in [research.md §12](../research.md).

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
