# UI Review Checklist: Chat Push Notifications

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-20
**Feature**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

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
- [ ] CHK030 **Translated text fits**: no fixed-width text containers, no truncation on anything
      the reader must act on. Verified in **German at 375px and at `md`** — the binding case,
      and where every overflow this product has shipped was found
- [ ] CHK031 No unintended horizontal scroll at 375px (`scrollWidth - clientWidth <= 1`)
- [ ] CHK032 Layout survives 200% browser zoom and larger text settings without clipping

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
- [ ] CHK033 Motion honours `prefers-reduced-motion`: movement goes, feedback stays. A component
      with a meaningful reduced alternative writes it (see `card.component.css`); nobody writes
      a blanket `transition: none` / `0.01ms` kill

## Iconography

- [ ] CHK023 Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [ ] CHK024 No emoji used as UI icons

## Accessibility

- [ ] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [ ] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [ ] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles
- [ ] CHK034 Every `<img>` carries `alt` — descriptive when it carries meaning, `alt=""` when it
      is decorative. A missing attribute is not the same as an empty one
- [ ] CHK035 DOM and focus order agree with the visual order; no keyboard trap
- [ ] CHK036 Heading levels never skip (`h1` → `h2` → `h3`) — the hierarchy is the navigation for
      anyone using a screen reader, and a skipped level breaks it silently

## Browser surfaces

- [ ] CHK038 Anything this feature adds that the browser draws — a new scrollable region, a text
      input, prose links — inherits the themed selection, caret, scrollbar and underline offset
      from the base layer rather than overriding them

## Empty, loading & error states

- [ ] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [ ] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)
- [ ] CHK037 Loading is the muted `jh-loading` line with `role="status"` — **never a skeleton or a
      spinner**. `animate-pulse` placeholders are the shape this forbids (GH #337)

## Feature-specific UI

The whole UI surface of this feature is **one new row in the notification preferences matrix**
(`features/settings/notifications/notification-settings.component.html`) plus its server-owned copy.
There is no new component, no new page and no new route — the notification itself is drawn by the
operating system and is outside DESIGN.md's reach.

- [ ] CHK039 The **Chat** row renders last, after Events, and is visually identical to the other
      four rows — same card on mobile, same grid cell on desktop. It is a peer, not a special case
- [ ] CHK040 The unavailable **In-app** and **E-mail** cells read as *not offered*, never as
      *switched off*. A greyed-out switch is the failure mode: it says "you could turn this on",
      which is untrue and invites the support question the description exists to pre-empt
- [ ] CHK041 The unavailable cells are **not focusable and carry no `role="switch"`** — a screen
      reader must not announce a toggle that cannot be toggled (CHK027, CHK035)
- [ ] CHK042 The unavailable cells have an accessible name saying why, not a bare em dash. A lone
      "—" is announced as nothing at all
- [ ] CHK043 On **mobile**, the Chat card shows three labelled rows like every other card, with two
      of them reading as unavailable — the label is not dropped, because a missing row is
      indistinguishable from a rendering bug
- [ ] CHK044 **Desktop matrix at `md` (768px) in German with five rows** — the binding case. The
      four-column `[1fr_5rem_5rem_5rem]` grid was laid out for four rows of shorter copy; German
      "Chat-Nachrichten" plus its description must not push the toggle columns or wrap into them
- [ ] CHK045 The Chat description explains the empty cells in the reader's own language, in the
      product voice ("you"/"we"), and **does not name a feature number or an internal concept**
- [ ] CHK046 Copy exists in **all three catalogues** (en/de/es) and in the server-owned
      `CategoryCopy` dictionary, added in one change — `catalog-parity.spec.ts` goes red otherwise,
      and the server dictionary has no parity guard at all, so its three entries are checked by eye
- [ ] CHK047 Turning the Chat toggle off changes **nothing inside chat** — the badge, the inbox and
      the conversation are untouched (FR-028b). Verified in the browser, not only in a test

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep
  `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than
  resolving it silently.

### Pre-existing conflict, reported not resolved

**CHK037 already fails on this page and this feature does not fix it.**
`notification-settings.component.html` renders its loading state as two `animate-pulse` skeleton
blocks, which is exactly the shape CHK037 forbids (GH #337). It predates this feature, sits outside
the diff, and swapping it for `jh-loading` would be an unrelated change to a shared screen. Recorded
here so the gate is not marked clean on a page that is not.

### Out of the design system's reach

The notification itself — lock-screen heading, body text, icon, collapse behaviour — is drawn by
the operating system from the four fields the server sends. DESIGN.md does not govern it and cannot.
What *is* reviewable about it is the copy, and that is checked in the browser walk
([quickstart.md](../quickstart.md) scenario 7) rather than here.
