# UI Review Checklist: Network Resilience (028)

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-24
**Feature**: [spec.md](../spec.md)

**Scope note**: this feature's UI surface is deliberately tiny — **one line of copy that swaps in
place** inside the existing `jh-loading` primitive (FR-011), plus the new DESIGN.md section that
governs it (FR-011b). No new component, no new colour, no new layout. Most items below are marked
n/a for that reason, and saying so explicitly is more useful than ticking boxes nothing touched.

Diff under review: `frontend/apps/web/src/app/shared/ui/loading/loading.component.{ts,html}`,
`.../loading.component.spec.ts`, and the new **Loading, error & retry states** section in
`DESIGN.md`.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** — the line keeps `text-muted` / `text-body-sm`; no raw scale step introduced
- [x] CHK002 **Exactly one coral CTA per view** — n/a, no CTA added or changed
- [x] CHK003 Lemon highlight only for small pops — n/a, not used
- [x] CHK004 Status uses paired `*-bg`/`*-border`/`*-fg` tokens — n/a; the patient line is deliberately **not** a status tone. It is reassurance, not a warning, so styling it as `warning` would misrepresent a normal slow load
- [x] CHK005 No new colors introduced ad hoc — none added

## Typography, numbers & voice

- [x] CHK006 Display vs. body faces — unchanged; the line stays body (Mona Sans)
- [x] CHK007 Mono for scores/stats — n/a
- [x] CHK008 **Sentence case** — "Still loading…" is sentence case, matching "Loading…"
- [x] CHK009 Nothing meaningful below 12px — `body-sm` unchanged
- [x] CHK010 Addresses reader as "you", CTAs invite, **no emoji** — copy is warm and plain; DESIGN.md's new section carries worked examples ("We couldn't load that just now — give it another go.")

## Layout & spacing

- [x] CHK011 Touch target ≥ 44px — n/a, non-interactive text
- [x] CHK012 Spacing from the 4px scale — component-owned spacing unchanged
- [x] CHK013 Centered column / mobile-first — unchanged
- [x] CHK014 Section rhythm — unchanged

## Shape & elevation

- [x] CHK015 No sharp corners — n/a, no box added
- [x] CHK016 Warm-tinted shadows — n/a
- [x] CHK017 Card treatment — n/a
- [x] CHK018 Larger shadows reserved for floating elements — n/a, and specifically: **no overlay, banner or toast was added**, per FR-011

## Motion & states

- [x] CHK019 Token durations/easings — n/a; the copy swap is instantaneous by design. A transition would draw the eye to a change meant to be noticed only if you were already waiting
- [x] CHK020 Focus visible — n/a, non-focusable
- [x] CHK021 Button hover/press — n/a
- [x] CHK022 No infinite decorative loops — **satisfied and load-bearing**: the reassurance is a static line, not a spinner. DESIGN.md's loading treatment has never used one

## Iconography

- [x] CHK023 Lucide line icons only — n/a, no icon added
- [x] CHK024 No emoji as UI icons — none

## Accessibility

- [x] CHK025 WCAG AA contrast — `text-muted` on the existing surface, unchanged from the reviewed `jh-loading` treatment
- [x] CHK026 **Never colour alone** — the state change is conveyed entirely by *words*. The element keeps `role="status"`, so the swap is announced to assistive tech rather than only being visible
- [x] CHK027 Keyboard-reachable with labels/roles — `role="status"` preserved; verified by spec `swaps copy in place, keeping the same announced element and styling`

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm next step — unchanged; DESIGN.md now states explicitly that an **error must not be rendered as an empty state**, since doing so quietly lies about what happened
- [x] CHK029 Loading and error states exist and are styled to the system — this is the item the feature exists to strengthen. DESIGN.md previously had **no** loading/error/retry guidance at all; the new section documents the existing `jh-loading` / `jh-alert` / `jh-empty-state` conventions and adds the patient line

## Feature-specific UI

- [x] CHK030 The patient line **swaps in place** — same `<p>`, same classes, same live region; verified by spec asserting exactly one `<p>` and identical element identity before and after
- [x] CHK031 **No layout shift** — copy substitution only; no element added, removed, or resized
- [x] CHK032 **Silent on a fast load** — verified by spec: nothing changes before the threshold, so the common case carries no visual noise (FR-011)
- [x] CHK033 The threshold and copy are **documented in DESIGN.md before implementation**, per FR-011b — the ordering was followed (T016 before T017), not back-filled
- [x] CHK034 All **34** existing `jh-loading` call sites keep working untouched — `patientLabel` is optional with a default; no call site was edited

## Notes

- **Result: PASS.** No DESIGN.md conflict found.
- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): `.html` / `.css` / `.ts`
  remain separate; only `.ts` and `.html` changed.
- **Pre-existing, out of scope**: the app-wide primary-button contrast issue (white on `coral-4`,
  ~3.14:1 vs. DESIGN.md's ≥4.5:1 rule) is untouched by this feature and remains an open brand
  decision. Recorded here so the PASS above is not read as clearing it.
