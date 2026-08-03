# UI Review Checklist: Terms of Use with Community Rules

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-08-03
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and **before**
verification. Check each item against the diff, recording `file:line` for anything that fails.
DESIGN.md is the source of truth: if a check ever conflicts with it, DESIGN.md wins and the
conflict is reported rather than silently resolved.

**Scope of this feature's UI**: two surfaces only — the `/terms` document page (which inherits
the shared `LegalPageComponent`, so most of the standing set is satisfied by reuse rather than by
new code) and the acceptance control on `/register`. The `jh-legal-links` cluster gains a third
anchor.

**Verification legend**: `[x]` verified · `[~]` verified by automated test or code reading, not in a
browser · `[ ]` **not verified** (needs a browser pass). Nothing is ticked that was not actually
checked.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`) — new markup uses `text-body-sm`, `text-danger-fg`, `border-border-strong`, `text-link`, `text-subtle` only
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view** — `/register` still has one submit button; `/terms` has no CTA at all
- [x] CHK003 Lemon `brand-highlight` is used only for small pops — not used by this feature
- [x] CHK004 Status uses the paired `*-bg` / `*-border` / `*-fg` tokens — both new alerts use `jh-alert tone="danger"`; the blocked-submit hint uses `text-danger-fg`
- [x] CHK005 No new colors introduced ad hoc — this feature added none

## Typography, numbers & voice

- [x] CHK006 Headings/hero use **Hubot Sans**; body and UI text use **Mona Sans** — inherited unchanged from `LegalPageComponent`
- [x] CHK007 Scores, stats, times, counts in the **mono** face — none in this feature; the version string is prose, not a statistic
- [~] CHK008 **Sentence case everywhere** — all new headings and labels are sentence case (read across the three catalogues)
- [x] CHK009 Nothing meaningful below 12px; body is 16px — document body is `body-md`, meta line is `caption`
- [~] CHK010 Copy addresses the reader as **"you"** / **"we"**; no emoji — German uses "du" consistently with the existing legal text; no emoji in any of the three catalogues

## Layout & spacing

- [~] CHK011 Interactive controls have a **touch target ≥ 44px** — the 16px checkbox sits inside a full-width `<label>`, so the hit area is the label row; identical to the existing `sign-in-remember` control. **Worth a physical check on mobile.**
- [x] CHK012 Spacing composes from the 4px scale tokens — new markup uses `gap-sm`, `mt-xs`, `mt-0.5` only
- [x] CHK013 *(superseded by CHK030 for `/terms` — see Feature-specific)*
- [x] CHK014 *(superseded by CHK031 for `/terms` — see Feature-specific)*

## Shape & elevation

- [x] CHK015 **No sharp corners** — checkbox `rounded`, terms link `rounded-sm`, alerts inherit `jh-alert`
- [x] CHK016 Shadows are the warm-tinted tokens — this feature adds no shadow
- [x] CHK017 Cards are `surface-card` + 1px border + `sm` shadow — `/register` keeps its existing `jh-card`; `/terms` deliberately has none (CHK036)
- [x] CHK018 Larger shadows reserved for floating elements — none added

## Motion & states

- [x] CHK019 Transitions use the token durations/easings — the terms link uses `transition-colors duration-fast`
- [~] CHK020 Focus is always visible — the checkbox and the in-label link are native focusable elements inheriting the global focus ring; **not confirmed visually**
- [x] CHK021 Button hover/press behaviour — submit button unchanged
- [x] CHK022 No infinite decorative animation loops — none added

## Iconography

- [x] CHK023 Icons are **Lucide line icons** only — this feature adds no icon
- [x] CHK024 No emoji used as UI icons — verified across all six catalogue files

## Accessibility

- [ ] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** — uses existing token pairs only, but **not measured**
- [x] CHK026 Status **never conveyed by color alone** — the blocked-submit state is stated in words (`register-terms-required`), not just a disabled button
- [~] CHK027 Interactive elements keyboard-reachable with labels/roles — checkbox and link are separately tabbable by construction; the error hint carries `role="alert"` and is wired via `aria-describedby`. **Tab order not confirmed in a browser.**

## Empty, loading & error states

- [x] CHK028 Empty states — none introduced by this feature
- [x] CHK029 Loading and error states exist and are styled — `/terms` inherits the loading spinner and the error+retry block; `/register` gains a styled `jh-alert` for a failed catalogue load

## Feature-specific UI

### The `/terms` document page — DESIGN.md "Long-form content"

- [x] CHK030 Content column caps at **`container-sm` (640px)** — `<jh-page-container width="sm">`, inherited unchanged. Overrides CHK013 per DESIGN.md
- [x] CHK031 Paragraph rhythm uses `sm` between paragraphs and `2xl` between sections, **not `section-gap`** — `mt-sm` on `<p>`, `mt-2xl` on `<section>`, inherited unchanged. Overrides CHK014
- [x] CHK032 Heading hierarchy `h1` → `h2` with **no skipped levels** — asserted by `legal-page.component.spec.ts` ("has an unbroken heading hierarchy" for terms)
- [x] CHK033 Meta line sits under the `h1` at `caption`/`text-subtle`, carrying **both** version and last-updated — asserted by two tests (`legal-version` present on terms, absent on privacy)
- [x] CHK034 **In-prose links are underlined** — `underline underline-offset-2` on the terms cross-links, the `jh-legal-links` anchors (asserted in its spec), and the acceptance-label link
- [ ] CHK035 Lists in the rules section use `disc`/`decimal` — **NOT MET, see Findings below.** The rules render as consecutive paragraphs because `LegalPageComponent` has no list primitive
- [x] CHK036 **Restraint**: no card, no shadow, no gradient strip, no accent field — inherited; `/terms` adds none
- [x] CHK037 Renders **outside the app shell** with the slim brand + language-switcher header — route declared outside `ShellComponent`, template inherited
- [x] CHK038 Table of contents uses `routerLink` + `fragment`, never a bare `href="#id"` — inherited; the anchor-target test covers all eight terms sections
- [x] CHK039 Authoritative-language notice on `en`/`es`, **not** on `de` — covered by the existing `showAuthoritativeNotice` tests
- [x] CHK040 Failed catalogue load shows styled error + retry, never a blank document — covered by the existing failure-handling tests

### The acceptance control on `/register`

- [x] CHK041 Checkbox **unticked on first render** — asserted on the rendered input *and* the form model (`renders the acceptance checkbox unticked`). Never set programmatically anywhere in the component
- [x] CHK042 Label + checkbox are a single `<label>` so the text is part of the hit area — follows the existing `sign-in-remember` pattern
- [~] CHK043 The terms link is **underlined** and separately keyboard-reachable — it is a real `<a routerLink>` inside the label, so tabbable by construction; **tab order not confirmed in a browser**
- [x] CHK044 Label composed from translated segments around a real `routerLink` — **no `[innerHTML]`**; the catalogue guard also rejects any value containing `<`
- [x] CHK045 Blocked-submit reason stated in plain language — `register-terms-required`, asserted by test
- [x] CHK046 The `409` case renders a specific, actionable message — asserted by test (contains "updated" and "reload"), not the generic error string
- [x] CHK047 Catalogue-load failure state is styled and explains the block — `register-terms-unavailable` via `jh-alert`, asserted by test
- [~] CHK048 Sentence case; warm "you" voice, not contract-speak — read across all three catalogues

### `jh-legal-links`

- [x] CHK049 Third anchor matches the existing two in styling — identical class list; the container already carries `flex-wrap gap-x-md gap-y-xs`. **Narrow-width wrap not confirmed visually**
- [x] CHK050 Verified on `/register` — the `inline` variant test asserts all three links render

## Findings

### F1 — CHK035: the rules render as paragraphs, not lists

DESIGN.md's Long-form content section says *"Lists carry the load in legal text; they are content,
not decoration."* The ten conduct rules in `behaviour` are consecutive `<p>` elements instead of a
`<ul>`.

**Cause**: `LegalPageComponent` renders `body: string[]` as paragraphs and has no list primitive.
Adding one means a catalogue schema change (tagging entries as list items) across all three
documents and all three languages — a change to feature 036's shared component with no
requirement in this spec asking for it.

**Assessment**: a real deviation from DESIGN.md, deliberately not resolved here rather than
silently ignored, per the checklist's instruction to report conflicts. The rules still read as a
scannable block because each is one short sentence. Worth a follow-up that fixes it for all three
documents at once.

### F2 — Browser-level checks not run

CHK025 (measured contrast), and the visual halves of CHK011, CHK020, CHK027, CHK043 and CHK049
were **not** verified — no browser session was run. Everything marked `[~]` was confirmed by
reading the code or by an automated test, which does not substitute for looking at it. These are
the items to cover in a manual pass, together with quickstart scenarios 8–9.

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- **Known intentional overrides**: CHK030 overrides CHK013, and CHK031 overrides CHK014, for the
  `/terms` page. Both are DESIGN.md's own Long-form content rules taking precedence over the
  ordinary-page rules — recorded here rather than resolved silently, per the template's
  instruction. The same overrides already apply to `/privacy` and `/imprint`.
- If any other check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here.
