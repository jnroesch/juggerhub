# UI Review Checklist: Self-Service Account Deletion

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before this feature is considered done.
**Created**: 2026-08-01
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and **before** verification. Check each item against the diff, recording `file:line` for anything that fails. [DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever conflicts with it, DESIGN.md wins and the conflict is reported rather than silently resolved.

**Reviewed 2026-08-01** against `features/account/delete-account.component.{ts,html,css}` and the `/account` page.

**Basis of this review, stated plainly.** The feature-specific set (CHK030–036) and the token/colour set (CHK001–005) were checked line by line against the component. The standing DESIGN.md set (CHK006–029) was reviewed as a whole against a component that is deliberately made of existing primitives — `jhButton`, `jh-alert`, the `danger-*` tokens, the 4px space scale — and introduces no new visual vocabulary. Several of those items **do not apply** and are marked complete on that basis rather than by inspection:

- **CHK007** (mono face for numbers) — the panel shows no numbers.
- **CHK017 / CHK018** (card lift, floating shadows) — the panel is an inline bordered region, not a card, and does not float.
- **CHK019 / CHK021** (transitions, button press states) — inherited from `jhButton`; nothing bespoke was animated.
- **CHK023 / CHK024** (icons) — no icons were added.
- **CHK028** (empty states) — this surface has no empty state.

**Not verified in a browser.** This review is a code review. The remaining visual confirmation — real focus rings, real touch targets at mobile width, the panel at 320px — belongs with the e2e/manual pass (T058, T061) and is not claimed here.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps (`sand-4`, `coral-5`)
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view**; supporting actions use sage `brand-secondary` — the danger zone adds no coral (see CHK031)
- [x] CHK003 Lemon `brand-highlight` is used only for small pops ("New" badges, streaks, dots) — never large fields — not used here
- [x] CHK004 Status (success/danger/warning/info) uses the paired `*-bg` / `*-border` / `*-fg` tokens, not ad-hoc colors
- [x] CHK005 No new colors introduced ad hoc — any new value was added to DESIGN.md tokens first — none added

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

- [x] CHK025 Body text meets **WCAG AA contrast (≥ 4.5:1)** against its surface
- [x] CHK026 Status is **never conveyed by color alone** — paired with text or an icon
- [x] CHK027 Interactive elements are keyboard-reachable with a visible focus state and appropriate labels/roles

## Empty, loading & error states

- [x] CHK028 Empty states offer a warm, low-pressure next step (e.g. "Be the first to…")
- [x] CHK029 Loading and error states exist and are styled to the system (not raw/unstyled)

## Feature-specific UI

- [x] CHK030 The danger zone uses the paired `danger-fg` / `danger-bg` / `danger-border` tokens, never ad-hoc red
- [x] CHK031 The destructive action is **not** the view's one coral `brand-primary` CTA (CHK002) — erasure is a danger action, not the page's primary invitation. Uses `jhButton variant="danger"` to open and a `bg-danger-fg` confirm; no coral anywhere in the section
- [x] CHK032 Blocking-condition refusals render via `jh-alert tone="danger"` (which carries `role="alert"`), listing **every** blocker at once per FR-011 — `@for` over the full array, never truncated
- [x] CHK033 The disclosure states plainly that messages and posts **remain** (FR-025) and that self-typed identifying text survives (FR-027) — in the warm "you" voice, not legalese. `retainedIntro` leads with *"this is the part people don't expect"*
- [x] CHK034 The confirmation word is localised per T064's vocabulary and the input's expected value matches what the dialog asks for in that language — `confirmationWord` drives both the label and the client-side match; the server accepts the whole en/de/es set
- [x] CHK035 Cancelling changes nothing and clears the typed password (FR-042 / spec US2 scenario 4). **Note**: built as an inline expanding panel, not a modal — matching the existing `team-settings` danger zone — so focus-trapping does not apply. Keyboard reachable throughout
- [x] CHK036 The "A former player" placeholder renders without layout breakage where an avatar is absent (US3 / T049) — placeholder flows through the same name field as any other member, and no avatar-specific layout depends on the profile existing

## T064 — Erasure-vs-ban vocabulary (FR-030)

Audited the existing `en`/`de`/`es` catalogues before writing any new text. **The collision FR-030 guards against does not exist**, so account erasure can use each language's natural delete verb.

| Concept | en | de | es |
|---|---|---|---|
| Ban (013) | **Ban** account | Konto **verbannen** | **Vetar** cuenta |
| Suspend (013) | Suspend account | Konto **sperren** | **Suspender** cuenta |
| Delete content | Delete team / message | Team / Nachricht **löschen** | **Eliminar** equipo / mensaje |
| **Erase account (037)** | **Delete** account | Konto **löschen** | **Eliminar** cuenta |

**Why this is safe.** Ban carries a distinct verb in all three languages — `ban` / `verbannen` / `vetar` — and never `delete` / `löschen` / `eliminar`. German was the predicted risk because *löschen* reads naturally for both meanings, but 013 chose *verbannen* for ban and *sperren* for suspend, so all three account outcomes already have separate verbs. Erasure takes the fourth.

**Confirmation words** (FR-004, CHK034) — the literal a member types:

| en | de | es |
|---|---|---|
| `DELETE` | `LÖSCHEN` | `ELIMINAR` |

The server accepts the set for all supported languages rather than one hardcoded English value, so a German member types a German word (see [contracts/account-deletion.md](../contracts/account-deletion.md)).

**One thing to preserve.** The German ban description already tells admins that a ban *"blockiert ihre E-Mail für eine erneute Registrierung"*. That is the FR-032 asymmetry stated in the product today, and the erasure copy must not contradict it: a ban blocks the address, an erasure releases it.

## T003 — DESIGN.md destructive-pattern findings

Read before implementation, per constitution Gate 7. **No conflict found; no new style needed.**

DESIGN.md already provides everything this feature's UI requires:

| Need | DESIGN.md provides | Where |
|---|---|---|
| Destructive colour | `danger-fg` (`red-6`), `danger-bg` (`red-0`), `danger-border` (`red-1`) — a full paired status set | tokens, ~L108 |
| Page/form-level error surface | `jh-alert` with `tone="danger"`, which **carries `role="alert"`** | states section, ~L381 |
| Never colour alone | Status is explicitly required to pair colour with text or icon | states section |
| Error voice | "a short, human sentence plus a way out"; never a status code or internal detail | states section |

Two DESIGN.md rules bear directly on this feature and are recorded as CHK031 and CHK033:

1. **One coral CTA per view.** The account page may already have a primary action. The delete control is a *danger* action and must not compete for the coral slot — this is why CHK031 exists rather than being left to judgement.
2. **"Never surface a status code, stack trace, or internal detail."** This coincides exactly with constitution Principle I and with FR-042. The 500 path must render the system's error voice, not the server's message.

**Known standing conflict (not introduced here).** DESIGN.md demands ≥ 4.5:1 body contrast (CHK025) while specifying primary buttons as white-on-`coral-4` (3.14:1). This is an open app-wide issue predating this feature. This feature does **not** resolve it and must not silently work around it — the danger action uses `danger-*` tokens, which are unaffected.

## Notes

- Check items off as verified: `[x]`. Record `file:line` inline for any failure.
- Conventions reminder ([constitution](../../../.specify/memory/constitution.md) VI): keep `.html` / `.css` / `.ts` separate per component.
- If a check conflicts with DESIGN.md, DESIGN.md wins — note the conflict here rather than resolving it silently.

