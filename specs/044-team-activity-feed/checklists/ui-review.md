# UI Review Checklist: Team-internal "What's happening" section

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-08-11
**Feature**: [spec.md](../spec.md) — feature 044, GH #178

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

- [ ] CHK030 **The awards overlap reads as two framings, not two happenings** (FR-019/FR-020). For a member, one award now appears in both the new dated card and the existing "Badges & achievements" card. The latter must still read unmistakably as a *standing collection* / trophy shelf, the former as a *log*. **This is the feature's main UI risk.**
- [ ] CHK031 The "Badges & achievements" card gained **no dates and no date-ordering** (FR-019a) — it is unchanged by this feature
- [ ] CHK032 The new card renders a **visible empty state** when nothing happened in the window (FR-014) — it is *not* hidden, unlike the dashboard's `jh-activity-list`, and *not* styled as an error (DESIGN.md: "Error vs. empty — they are different and must look different")
- [ ] CHK033 The empty-state copy means **"nothing lately"**, not "this team has never done anything" — the team's event history is in the card above and must not be implicitly denied
- [ ] CHK034 The two headings are **distinguishable in all three languages**, and the German one is **"Was passiert gerade"**, never "Was ist los" — that string belongs to the dashboard (`home.activityTitle`) and reusing it recreates the exact confusion #178 reports (SC-010)
- [ ] CHK035 The renamed events card names **events** in en/de/es ("Recent events" / "Letzte Events" / "Eventos recientes") and its contents, cap of 6, and ordering are untouched (FR-016/FR-017)
- [ ] CHK036 Every entry kind renders **without horizontal scrolling at 375px**, including the longest German wording — a long training name plus "… wurde abgesagt" is the binding case (SC-007). No font shrink, no truncation of the date
- [ ] CHK037 The card is **absent entirely** for a signed-in non-member — not present-and-empty (FR-002)
- [ ] CHK038 Entries are **read-only**: no button, menu, or action affordance anywhere in the card (FR-026)
- [ ] CHK039 Relative timestamps use the shared `injectRelativeTime()` helper and sit in the same muted `caption` treatment as the dashboard's activity rows
- [ ] CHK040 An entry with no navigation target renders as **plain text**, never as a link that goes nowhere (FR-022)
- [ ] CHK041 A missing player name renders the **translated** stand-in, never an English word inside a German or Spanish page, and never a blank gap (FR-024)

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.

### How this review was performed (2026-08-11)

**Verified by code inspection of the diff** — the 33 items checked above. The new card was built by
mirroring the two components that already sit on this page and on the dashboard, so most of the
design system compliance is inherited rather than re-decided:

- **Tokens** — the component uses only semantic aliases (`p-md`, `mb-sm`, `gap-xs`, `gap-sm`,
  `text-body-lg`, `text-heading`, `text-body-sm`, `text-body`, `text-caption`, `text-subtle`,
  `hover:text-link`). No raw scale steps, no ad-hoc colors, no new values.
  `team-happenings.component.html`
- **Card, shape, elevation** — rendered through the shared `jh-card`, so radius, border, warm
  shadow and hover-lift are the component's, not re-implemented.
- **Heading style** — deliberately follows the *team page's* card convention
  (`text-body-lg font-semibold text-heading`), not the dashboard's uppercase eyebrow, so the new
  card sits consistently beside Roster / Recent events / News.
- **Body text tone** — `text-body` to match the sibling cards on this page, rather than the
  dashboard's lighter `text-muted`. Both are token aliases; this one matches its neighbours.
- **Sentence case, no emoji** — all 21 new catalogue strings across en/de/es.
- **Read-only** — no `button`, `input`, or action affordance in the template; asserted by
  `team-happenings.component.spec.ts` ("is read-only").
- **Empty vs. error** — the empty branch uses `jh-empty-state inline`, matching the sibling cards.
  DESIGN.md's "error vs. empty must look different" holds: nothing here renders an error style.
- **Separate `.ts` / `.html` / `.css`** — constitution VI satisfied.

**NOT verified — requires the running application.** These four are genuinely outstanding and are
the remaining gate-7 work:

- **CHK011** (touch targets ≥ 44px) — the card has no interactive controls other than inline text
  links, which DESIGN.md's 44px rule targets at buttons and inputs; still worth an eyeball.
- **CHK013 / CHK014** (container width, section rhythm) — inherited from the page's existing grid,
  but unconfirmed visually.
- **CHK020 / CHK025 / CHK027** (visible focus, AA contrast, keyboard reachability) — the entry
  links are plain `routerLink` anchors inheriting global focus styles, but contrast and focus were
  not measured.
- **CHK030 / CHK036** — **the two that matter most.** CHK030 is the awards overlap (one award in
  both the dated card and the trophy card) and CHK036 is the 375px German wording. Both are
  judgement calls that can only be made looking at the rendered page.

An attempt to bring the stack up for these was not completed in this session, so they are recorded
as outstanding rather than passed.
