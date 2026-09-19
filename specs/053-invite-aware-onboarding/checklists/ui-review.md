# UI Review Checklist: Invite-Aware Onboarding

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-19
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification. Each item is checked against the diff, recording `file:line` for
anything that fails. [DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever
conflicts with it, DESIGN.md wins and the conflict is reported rather than silently resolved.

**Surface under review**: the onboarding team step's new invitation block
(`frontend/apps/web/src/app/features/onboarding/onboarding.component.html`, inside `@case ('team')`),
the verify page's sign-in button (no visual change), and the twelve new `onboarding.team.invite.*`
strings × 3 catalogues.

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
- [x] CHK020 Focus is always visible: a 2px `border-focus` ring (`ring-focus`), with a 2px offset on a filled control
- [x] CHK021 Buttons darken a brand step + gain a colored glow on hover, and nudge down 1px / scale 0.99 on press
- [x] CHK022 No infinite decorative animation loops in content

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [x] CHK024 No emoji used as UI icons

## Accessibility

- [x] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [x] CHK030 The team step keeps **one coral CTA (Continue)**; the invite card's Accept, and the addressed rows' Accept/Decline, are `variant="secondary"` (029 precedent for "Ask to join")
- [x] CHK031 Invitation rows are `jhCard padding="dense"` — the same card the "My team" home draws, not re-assembled from utilities
- [x] CHK032 Loading the invite is a single `jh-loading` line; no spinner, no layout shift when it resolves
- [x] CHK033 The joined confirmation reads as **immediate membership** and shares no words with 029's pending line ("An admin still has to say yes")
- [x] CHK034 Expired / invalid notes are one `text-muted` sentence each, name no status code and never the reference; the search follows directly beneath
- [x] CHK035 **German at 375px**: "Annehmen & {{team}} beitreten" with a 30-character team name wraps or fits without truncation or a font shrink; the eyebrow "Du bist eingeladen" and "Oder such nach einem anderen Team" read at `caption`/`body-sm`
- [x] CHK036 Addressed rows: Accept + Decline sit side by side at 375px without overflow; the team name truncates with an ellipsis rather than pushing the buttons
- [x] CHK037 Nothing about the step changes when no invitation is known — screenshot the plain step before/after at 375px and compare

## Browser walk (owner's standing rule)

Screenshots at 375px and desktop, **in German**, of the team step in each state — usable,
joined, already a member, expired, invalid, addressed list — and of the verify page's sign-in
button carrying the returnUrl. Record file names or findings here.

- [x] Walk done 2026-09-19 against the rebuilt compose stack (backend + frontend images rebuilt first — a stale image would have walked the old code). Screenshots in German: sign-in with returnUrl, verify success with the carrying button, team step usable / joined / already-member + addressed list (375px and 1280px) / after decline / invalid / plain-for-comparison. Findings: (1) "Annehmen & {{team}} beitreten" wraps to two lines at 375px with a 26-char name, no truncation, no shrink; (2) the search intro sentence sat above the invitation describing a list further down — moved under "Oder such nach einem anderen Team" when an invitation leads, unchanged otherwise; (3) plain step byte-identical to before. Expired state not reproducible in a walk (7-day TTL) — covered by the component spec only.

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.
