# UI Review Checklist: Join Requests Reach the People Who Decide Them

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Scope of the UI this feature ships**: the *Needs you* card (a join-request kind, and every kind's
words moved client-side), two new Alerts rows (a request, an answer), the team page's answer notice
and request errors, onboarding's request errors, and two changed confirmation strings.

**How it was checked**: a real-browser walk in German at 375px and 1280px against the rebuilt
stack (`docker compose up -d --build`), one browser context per actor — 42 automated checks, every
screenshot looked at (T049). The walk found two defects, both fixed in `82420d2` and re-walked:
the Alerts supporting line was cut ("Wartet nicht mehr auf eine Antw…"), and Home kept counting a
request the card had just dropped.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** — the new notices are `jh-alert` (`info` / `danger` token triples); the rows reuse the existing `text-heading` / `text-muted` / `border-border-muted` classes.
- [ ] CHK002 **Exactly one coral `brand-primary` CTA per view** — *pre-existing, not changed*: every *Needs you* item carries a primary accept (feature 025's design), and the join-request item follows the card's pattern rather than breaking it. Worth a card-level decision of its own.
- [x] CHK003 Lemon is not used.
- [x] CHK004 Status uses the paired `*-bg` / `*-border` / `*-fg` tokens (via `jh-alert`).
- [x] CHK005 No new colors.

## Typography, numbers & voice

- [x] CHK006 Faces unchanged.
- [x] CHK007 The *Needs you* count stays in the mono face.
- [x] CHK008 Sentence case in all new copy (en/de/es).
- [x] CHK009 Nothing new below `caption`.
- [x] CHK010 "You"/"we" voice; errors open "We couldn't" / "Wir konnten" / "No hemos podido"; no emoji. German uses no `—`; Spanish no dangling raya (`catalog-punctuation` green).

## Layout & spacing

- [ ] CHK011 **Touch target ≥ 44px** — *pre-existing, not changed*: the *Needs you* card and the team page's join queue use `jhButton size="sm"` (`min-h-9`, 36px), and the queue's decline is a text button with `py-1`. The new join-request item uses the card's existing buttons.
- [x] CHK012 Spacing from scale tokens (`gap-sm`, `gap-xs`, `mb-sm`, `px-sm`).
- [x] CHK013 Page containers unchanged.
- [x] CHK014 Section rhythm unchanged.
- [x] CHK030 **Translated text fits** — German at 375px and 1280px: *Needs you* headlines wrap instead of truncating (the card used to `truncate` them), and at phone width the answers move under the text; both new Alerts rows wrap (after the fix, only a news excerpt is still cut to one line, which is a preview); the team page notice, the limit message and onboarding's limit message wrap. *Pre-existing, not this feature*: the team page roster truncates long names beside the admin chip ("Mira Albrecht-Hohen…").
- [x] CHK031 No horizontal scroll at 375px — measured `scrollWidth - clientWidth = 0` on every screen of the walk.
- [x] CHK032 Reflow — the 375px walk exercises the narrowest layout; nothing new has a fixed width.

## Shape & elevation

- [x] CHK015 Rounded throughout (`jh-alert` `rounded-md`, items `rounded-md`, cards `lg`).
- [x] CHK016 No new shadows.
- [x] CHK017 Cards are `jh-card`; nothing re-assembled from utilities.
- [x] CHK018 No new floating elements.

## Motion & states

- [x] CHK019 No new transitions.
- [x] CHK020 Focus visible — buttons via `jhButton`; the *Needs you* headline link now carries the same `focus-visible:ring-focus` as the Alerts row (it fell back to the browser's outline before).
- [x] CHK021 Button hover/press via `jhButton`.
- [x] CHK022 No infinite animation.
- [x] CHK033 No new motion.

## Iconography

- [x] CHK023 `jh-icon` Lucide glyphs only: `user-plus` (a request, invite scheme), `users` (an answer, info scheme).
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Contrast — only existing tokens, all measured by `contrast.spec.ts`.
- [x] CHK026 Never color alone — every notice and error is text.
- [x] CHK027 Keyboard reachable; `jh-alert` carries `role="alert"`, so a notice that appears after a press is announced.
- [x] CHK034 No new images.
- [x] CHK035 Order — at phone width the text precedes the answers in DOM and on screen; at `sm` and up they sit side by side in the same order.
- [x] CHK036 No new headings.

## Browser surfaces

- [x] CHK038 Nothing new that the browser draws.

## Empty, loading & error states

- [x] CHK028 *Needs you* still hides when empty; a notice explaining a request that left stays on Home even when the card goes with its last item.
- [x] CHK029 Every new failure has a styled, translated state (`jh-alert`), branched on the status — never the server's English `detail`.
- [x] CHK037 No skeletons or spinners added.

## Feature-specific UI

- [x] CHK039 *Needs you* holds one item of every kind in German without an English word (walk screenshot `03-home-needs-you-every-kind`, 375 and 1280).
- [x] CHK040 The admin's alert names the player from their current profile, and a banned or erased player as "Ein ehemaliger Spieler" (`02-admin-alerts-waiting`, `11-admin-alert-banned-player`).
- [x] CHK041 The player's answer rows: accepted opens the team, declined opens the team browser; no admin named (`04-…`, `06-…`).
- [x] CHK042 A stale press explains itself — on Home above the card, on the team page at the queue's place (`07-…`, `08-…`).
- [x] CHK043 The limit message on the team page and in onboarding (`09-…`, `10-…`).

## Notes

- Two unchecked items (CHK002, CHK011) are pre-existing in the *Needs you* card (feature 025) and the team page queue (feature 009); this feature follows them and does not make them worse. Neither is fixed here — restyling the card's actions is a design decision beyond #360.
- DESIGN.md conflicts: none found.
