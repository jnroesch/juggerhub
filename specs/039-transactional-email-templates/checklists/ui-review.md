# UI Review Checklist: Transactional Email Templates & Notification Preference Gating

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-08-02
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification — not a spec-quality gate like `requirements.md`.
[DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever conflicts with it,
DESIGN.md wins and the conflict is reported rather than silently resolved.

## Scope of UI in this feature

Deliberately narrow. This feature ships **no new visual pattern** — the review below is mostly
confirming an *absence*:

1. **Notification row** — one new type (`EventCancelled`) added to existing `link`/`title`/
   `supporting` computeds. No template or CSS change; the icon uses the existing `@default` arm.
2. **Notification settings** — one new category row, rendered by the existing component from the
   server-supplied list. **Zero frontend template change** — only a type-union member.
3. **Email templates** — 12 new files plus 3 footer edits. Email HTML is governed by
   `base-styles.html`, not by DESIGN.md's Tailwind token layer, so the email-specific checks are
   recorded separately at the end.

## Color & tokens

- [x] CHK001 Components reference semantic aliases — no component styling changed; the row's icon container reuses the existing `bg-surface-secondary-soft text-secondary` default branch
- [x] CHK002 Exactly one coral `brand-primary` CTA per view — unchanged; the cancellation row is link-only and adds no button
- [x] CHK003 Lemon `brand-highlight` used only for small pops — untouched
- [x] CHK004 Status uses paired `*-bg`/`*-border`/`*-fg` tokens — no status styling added; a cancellation is conveyed by copy, not by a colour treatment
- [x] CHK005 No new colors introduced ad hoc — none added

## Typography, numbers & voice

- [x] CHK006 Hubot Sans for display, Mona Sans for body — unchanged; no new type styles
- [x] CHK007 Scores/stats/times in the mono face — n/a, no numerics added
- [x] CHK008 Sentence case everywhere — new i18n strings are sentence case in all three languages ("{{event}} was cancelled", "{{event}} wurde abgesagt", "{{event}} se ha cancelado")
- [x] CHK009 Nothing meaningful below 12px — unchanged
- [x] CHK010 Copy addresses the reader as "you"; CTAs invite, never shout; no emoji — verified across the new i18n keys, the new category copy, and all 12 email bodies

## Layout & spacing

- [x] CHK011 Touch target ≥ 44px — no new interactive control; the new settings row reuses the existing toggle
- [x] CHK012 Spacing composes from the 4px scale — no spacing changed
- [x] CHK013 Centered column capped at `container-lg`, mobile-first — unchanged
- [x] CHK014 Section rhythm uses `section-gap` — unchanged

## Shape & elevation

- [x] CHK015 No sharp corners — unchanged
- [x] CHK016 Warm-tinted shadow tokens — unchanged
- [x] CHK017 Cards are `surface-card` + 1px border + `sm` shadow, lift on hover — the notification row's `jh-card` is untouched
- [x] CHK018 Larger shadows reserved for floating elements — unchanged

## Motion & states

- [x] CHK019 Token durations/easings — no new transition
- [x] CHK020 Focus always visible — the row's existing `focus-visible:ring-focus` anchor styling applies to the new link unchanged
- [x] CHK021 Button hover/press treatment — n/a, no new button
- [x] CHK022 No infinite decorative animation loops — none

## Iconography

- [x] CHK023 Lucide line icons only, 16–22px, `currentColor` — the new type falls to the existing 22px line-icon `@default` arm; no icon added
- [x] CHK024 No emoji as UI icons — none

## Accessibility

- [x] CHK025 Body text meets WCAG AA — unchanged token pairings
- [x] CHK026 Status never conveyed by colour alone — the cancellation is stated in the title and supporting text; no colour-only signal
- [x] CHK027 Keyboard-reachable with visible focus and appropriate labels — the row's `[attr.aria-label]="title()"` now resolves to real copy for this type instead of the generic fallback, which is a small accessibility *improvement*

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm next step — n/a, no new empty state
- [x] CHK029 Loading and error states exist and are styled — n/a, no new async surface

## Feature-specific UI

- [x] CHK030 The `EventCancelled` row renders a real title **and** a non-empty supporting line — guarded by `notification-row.component.spec.ts`. An unhandled type would fall through to `alerts.row.fallbackTitle` plus an empty line; the icon degrades safely but the text does not, which is why this is checked explicitly
- [x] CHK031 The new "Events" settings row renders from the server-supplied list with no hardcoded client copy — only `NotificationCategoryId` changed
- [x] CHK032 The settings matrix renders in all three languages — covered by the `Preference_matrix_renders_in_every_supported_language` theory. The category-copy lookup was additionally hardened from a bare indexer to a per-category English fallback, so a future missing translation degrades instead of throwing
- [x] CHK033 Email templates use only classes already defined in `base-styles.html` (`.content`, `.eyebrow`, `h1`, `.button`, `.alt-link`) — **no new CSS rule added** (FR-004), verified across all 12 files
- [x] CHK034 The party-news excerpt card is copied verbatim from `team-news.html`, so the two news emails read as one system
- [x] CHK035 Footer legal links reuse the existing `.footer-links a` styling in all three languages — no new footer CSS

## Notes

**No DESIGN.md conflicts found.** This feature was deliberately scoped to add no visual pattern,
and the review confirms that: every checked item is either unchanged or reuses an existing
component, token, or class.

**Email HTML is outside DESIGN.md's token layer.** DESIGN.md governs the Tailwind/SPA surface;
transactional email is governed by the constitution's *Transactional Email* section (base
templates with inline CSS) and by `base-styles.html`. The email-specific checks are recorded as
CHK033–CHK035 rather than forced into the Tailwind-token items, which do not apply to email
clients. This is a scope observation, not a conflict.

**One accessibility improvement worth noting**: the row's `aria-label` binds to `title()`. Before
this change an `EventCancelled` row would have announced the generic fallback label; it now
announces the event name.
