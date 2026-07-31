# UI Review Checklist: Privacy Policy & Imprint (036)

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../DESIGN.md) before a feature is considered done.
**Created**: 2026-07-31
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
- [x] CHK002 *(n/a — no CTA on either page)* **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary`
- [x] CHK003 Lemon `brand-highlight` is used only for small pops ("New" badges, streaks, dots) — never large fields
- [x] CHK004 Status (success/danger/warning/info) uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [x] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first

## Typography, numbers & voice

- [x] CHK006 Headings/hero use the **Hubot Sans** display face; body and UI text use **Mona Sans**
- [x] CHK007 *(n/a — no scores or stats; the last-updated date is prose, not a stat)* Scores, stats, times, and counts are set in the **mono** face (tabular)
- [x] CHK008 **Sentence case everywhere** (headings, buttons, labels, nav); UPPERCASE only as a styled eyebrow
- [x] CHK009 Nothing meaningful drops below 12px (`caption`); body is 16px (`body-md`)
- [x] CHK010 Copy addresses the reader as **"you"** / the community as **"we"**; CTAs invite, never shout; no emoji in product UI

## Layout & spacing

- [x] CHK011 Interactive controls (buttons, inputs) have a **touch target ≥ 44px**
- [x] CHK012 Spacing composes from the 4px scale tokens (`space-1`…`space-13`) — no arbitrary pixel values
- [x] CHK013 *(superseded for document pages by DESIGN.md → Long-form content: `container-sm`, 640px, for measure)* Content sits in a centered column capped at `container-lg` (1100px); layout is mobile-first and reflows down
- [x] CHK014 *(corrected — see notes)* Section rhythm uses `section-gap` (`clamp(48px, 8vw, 112px)`)

## Shape & elevation

- [x] CHK015 **No sharp corners** — radius matches element type (controls `sm`, buttons/inputs `md`, cards `lg`, media `xl`, chips/avatars `pill`)
- [x] CHK016 Shadows are the warm-tinted `xs`…`xl` tokens (`rgba(64,46,24,…)`) — never pure black, never harsh
- [x] CHK017 *(n/a — document pages deliberately carry no card)* Cards are a white `surface-card` with a 1px muted border and soft `sm` shadow; they **lift 3px + deepen shadow on hover**
- [x] CHK018 Larger shadows are reserved for elements that genuinely float above the page

## Motion & states

- [x] CHK019 Transitions use the `fast`/`base`/`slow` durations (120/200/320ms) and token easings (`ease-out` entrances, `ease-bounce` for toggles)
- [x] CHK020 Focus is always visible: 2px coral border + coral `focus-ring`
- [x] CHK021 Buttons darken a brand step + gain a colored glow on hover, and nudge down 1px / scale 0.99 on press
- [x] CHK022 No infinite decorative animation loops in content

## Iconography

- [x] CHK023 *(n/a — no icons on either page)* Icons are **Lucide line icons** only (no filled/duotone), 16–22px, colored via `currentColor` or a token
- [x] CHK024 No emoji used as UI icons

## Accessibility

- [x] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [x] CHK028 *(n/a — a legal document has no empty state; it either loads or errors)* Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [x] CHK030 The content column caps at **`container-sm` (640px)** via `jh-page-container width="sm"`, not `container-md`. Measure is the point of the treatment
- [x] CHK031 **Links inside prose are underlined**, unlike navigation links elsewhere. Intentional (DESIGN.md → Long-form content) — flag only if *missing*
- [x] CHK032 Heading levels **never skip** (`h1` → `h2` → `h3`). This is the screen-reader navigation, not decoration
- [x] CHK033 No card, shadow, gradient strip, or accent field on the document pages — text on the page background
- [x] CHK034 The meta line (last-updated date, authoritative-language notice) sits at the `caption` step in `text-subtle` — present, not competing
- [x] CHK035 The app footer is **not occluded** by the fixed mobile bottom bar at 375px and below
- [x] CHK036 No horizontal scroll at **320px** on either document page
- [x] CHK037 The DESIGN.md "Long-form content" section introduces **no new token** — no new colour, container width, or type step

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.
### Review outcome (2026-07-31)

**One real finding, fixed.** CHK014 caught a genuine inconsistency rather than a checkbox: the
Long-form content section as first drafted specified `section-gap` (`clamp(48px, 8vw, 112px)`)
between document sections. That token is page-level rhythm between major page regions; applied
between the sixteen sections of a privacy policy it turns a readable document into a scroll
marathon. **DESIGN.md was corrected**, not the page — the conflict was in the newly written
guidance, and the guidance is what future document pages will follow. The implementation now uses
`sm` between paragraphs and `2xl` between sections, and DESIGN.md says so.

CHK013 is superseded for these two pages: the generic rule caps content at `container-lg` (1100px),
while the new Long-form content section caps *documents* at `container-sm` (640px) for measure.
That is the point of the treatment, and DESIGN.md now states both rules and which applies where.

Six items are genuinely not applicable (CHK002, CHK007, CHK017, CHK023, CHK028, and the primary-
button half of CHK021): a document page has no CTA, no stats, no card, no icons, and no empty
state. Marked `[x]` with the reason inline rather than left unchecked, so a later reader can tell
"does not apply" from "nobody looked".

- **CHK002 does not apply and CHK025's known conflict must not be re-litigated here.** The open app-wide issue — DESIGN.md demands ≥4.5:1 while specifying white-on-`coral-4` primary buttons at 3.14:1 — is the owner's brand decision, tracked separately. This feature ships **no primary button**, so it neither triggers nor resolves it. Body text on the page background is well clear of 4.5:1.
