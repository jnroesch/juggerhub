# UI Review Checklist: Umami Session Recording

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-08-01
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** the change is built
and **before** verification. [DESIGN.md](../../../DESIGN.md) is the source of truth: if a
check conflicts with it, DESIGN.md wins and the conflict is reported rather than silently
resolved.

**Scope for this feature is deliberately narrow.** Recording ships **no new component, no
new route, and no in-app notice** — FR-020 made the policy page the only disclosure. The
entire UI surface is **new prose inside the existing `/privacy` page**, which already has
its layout and its DESIGN.md "Long-form content" treatment from feature 036.

Most of the standing compliance set is therefore not applicable: no new colors, shadows,
motion, icons, cards, empty states, or interactive controls are introduced. Marking those
`N/A` is the honest result, not a skipped review. The items that **do** apply are the ones
036 established for legal prose — and they are the ones most likely to be got wrong when
adding a section to a document written by someone else.

---

## Applies — long-form legal prose

- [ ] CHK001 New prose sits in the existing `container-sm` (640px) measure — the section is added *inside* the established layout, not alongside it
- [ ] CHK002 Section rhythm matches the surrounding policy sections (same heading level, same `section-gap`) — the recording section must not read as bolted on
- [ ] CHK003 **Sentence case** headings, consistent with every other section of the policy
- [ ] CHK004 In-prose links are **underlined** (036's rule for long-form content — unlike nav links)
- [ ] CHK005 Body text is 16px (`body-md`); nothing meaningful drops below 12px
- [ ] CHK006 Body text meets WCAG AA contrast (≥ 4.5:1) against its surface
- [ ] CHK007 Any anchor/deep link into the new section works, matching how 036 wired its existing anchors
- [ ] CHK008 Reads correctly at mobile width, and clears the fixed bottom bar as the rest of the page does

## Applies — voice (DESIGN.md), under legal constraint

- [ ] CHK009 Addresses the reader as **"you"** and the community as **"we"**, matching the rest of the policy
- [ ] CHK010 No emoji, no shouting, no euphemism. "We record what's on your screen" — not "we capture usage signals"
- [ ] CHK011 Warmth does not soften accuracy: the FR-006a disclosure (message content on screen is captured) must survive the voice pass intact. If warm phrasing and accurate phrasing conflict here, **accuracy wins and the conflict is noted below** — this is the one place in the product where DESIGN.md's voice yields

## Applies — the three languages

- [ ] CHK012 German, English, and Spanish all carry the same facts (spec US3 scenario 3)
- [ ] CHK013 German is authoritative; the existing divergence notice still displays correctly with the new section present
- [ ] CHK014 **Key sets are identical across `de`/`en`/`es`** — 036's Jest identical-key-set test still passes with the new keys added. This is the guard against a missing `de` key silently rendering English inside the legally binding German document (`useFallbackTranslation: true` + `fallbackLang: 'en'`)
- [ ] CHK015 `lastUpdated` changed (FR-020 — it is the only signal members get that anything changed)

## Not applicable — no new UI surface

- [ ] CHK016 Colors/tokens, brand CTAs, status colors — **N/A**, no new components
- [ ] CHK017 Shape, elevation, cards, hover lift — **N/A**
- [ ] CHK018 Motion, focus rings, button states — **N/A**, no new interactive controls
- [ ] CHK019 Iconography — **N/A**, no icons added
- [ ] CHK020 Empty/loading/error states — **N/A**, static prose
- [ ] CHK021 Touch targets — **N/A** except for links, covered by CHK004/CHK008

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- The injected recorder `<script>` renders nothing and is not a UI surface. It is verified
  by [quickstart.md](../quickstart.md), not here.
- Files in scope: `frontend/apps/web/public/i18n/legal/{de,en,es}.json` only.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than
  resolving it silently. CHK011 is the known standing exception, and it is a legal
  requirement (FR-016a) rather than a design preference.
