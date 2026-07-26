# UI Review Checklist: Structured Locations & "Near You"

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-07-25
**Feature**: [spec.md](../spec.md)

**Scope reviewed**: the shared `jh-city-picker` (onboarding, profile edit, team-create, event-create), the "City, Country" location display across profile/team/event/browse cards, and the browse country filter + onboarding near-you list.

**How to use**: implementation-quality gate, run after UI is built. DESIGN.md is the source of truth; conflicts are reported, not silently resolved.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** — the picker was rebuilt on `border-border-strong` / `bg-surface-card` / `text-body` / `text-subtle` / `focus-ring` tokens (it previously used ad-hoc hex fallbacks — fixed in this pass).
- [x] CHK002 **Exactly one coral CTA per view** — onboarding keeps its single coral "Continue"; the picker and browse filters are inputs, not CTAs.
- [x] CHK003 Lemon `brand-highlight` not misused (not used by this feature).
- [x] CHK004 Status uses paired tokens — the geocoder-unavailable message uses `text-danger-fg`.
- [x] CHK005 No ad-hoc colors — the picker's hardcoded hex fallbacks were removed; all values come from tokens.

## Typography, numbers & voice

- [x] CHK006 Display/body faces inherited from the app type scale (`text-body-md`, `text-body-sm`).
- [x] CHK007 Team player counts render in the mono face (existing browse row); the picker has no numeric data.
- [x] CHK008 Sentence case throughout ("Pick your home city", "No matching city — try a different spelling").
- [x] CHK009 Nothing below 12px — smallest text is `text-body-sm`.
- [x] CHK010 Voice: "…it powers teams and events near **you**"; no emoji.

## Layout & spacing

- [x] CHK011 Touch targets ≥ 44px — picker input `min-h-11`, options `min-h-11`, chip clear button bumped to `h-11 w-11` (was 32px — fixed in this pass).
- [x] CHK012 Spacing composes from scale tokens (`px-md`, `py-sm`, `gap-xs`) — no arbitrary px.
- [x] CHK013 Onboarding sits in the centered `max-w-sm` column; picker is fluid within its container.
- [ ] CHK014 `section-gap` rhythm — n/a (feature adds controls inside existing screens, no new page sections).

## Shape & elevation

- [x] CHK015 Radius by element type — input `rounded-md`, results `rounded-md`, options `rounded-sm`, selected chip `rounded-pill`.
- [x] CHK016 Results dropdown uses the warm `shadow-sm` token.
- [x] CHK017 Location display rides existing cards (unchanged card treatment).
- [x] CHK018 No oversized shadows introduced.

## Motion & states

- [x] CHK019 Transitions use `duration-fast` token easings on the input + hover states.
- [x] CHK020 Focus visible — input `focus:border-brand focus:ring-2 focus:ring-focus`; options + clear button `focus-visible:ring-2 focus-visible:ring-focus`.
- [x] CHK021 Buttons/options darken on hover (`hover:bg-surface-sunken`); primary CTAs unchanged.
- [x] CHK022 No infinite decorative animation.

## Iconography

- [x] CHK023 Icons via the shared `jh-icon` set (`check`, `x`), sized 16px, colored via token/`currentColor`.
- [x] CHK024 No emoji as icons.

## Accessibility

- [x] CHK025 Body/label text uses AA-contrast tokens against `surface-card`.
- [x] CHK026 The geocoder-unavailable state is conveyed as **text** (`role="status"`), never color alone.
- [x] CHK027 Keyboard-reachable: input has `aria-label` + `role="combobox"`, options `role="option"`, chip clear has `aria-label`, all with visible focus.

## Empty, loading & error states

- [x] CHK028 Empty state offers a next step: "No matching city — try a different spelling."
- [x] CHK029 Loading ("Searching…") and error (retryable "City search isn't available right now") states are styled to the system.

## Feature-specific UI

- [x] CHK030 City picker: debounced type-ahead, disambiguated `"City, Region, Country"` option labels (FR-003), a confirmed-selection pill chip with a clear control, and a graceful 503 transient state (FR-019).
- [x] CHK031 Location display shows `"City, Country"` everywhere a location appears (FR-010).
- [x] CHK032 Browse country filter + onboarding near-you list reuse the existing browse row/label styling.

## Notes

- Two findings were **found and fixed** during this review (rather than deferred): the picker was rebuilt from ad-hoc CSS/hex fallbacks onto DESIGN.md tokens (CHK001/005/015/016/020), and the chip clear button was enlarged to a 44px touch target (CHK011).
- Conventions ([constitution](../../../.specify/memory/constitution.md) VI): `jh-city-picker` keeps `.html` / `.css` / `.ts` separate; component-local CSS is now empty (all styling via tokens in the template).
- No DESIGN.md conflicts found.
