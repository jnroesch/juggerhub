# UI Review Checklist: The Team Page Leads Into the Team Chat

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-28
**Feature**: [spec.md](../spec.md)

**Scope of the diff**: `features/teams/team-detail/team-detail.component.html`. The header's action
block is wrapped in `@if (!isMember())`, the `team-tools` card gains **Team chat** (every member) and
**Contact admins** (plain members) plus one inline error line, and there is one page-level
`jh-alert`. It adds 4 copy keys × 3 catalogues and no new component, style or token.

**Evidence**: a browser walk against the rebuilt compose stack (Playwright, `locale: de-DE`, one
context per actor) at **375px** and **1280px**, 25/25 scripted checks passing, and screenshots
01–15 read one by one. Walk scripts deleted afterwards.

## Color & tokens

- [x] CHK001 Semantic aliases only: `jhButton variant="secondary"`, `text-danger-fg`, `jh-alert tone="info"`. No raw scale step added.
- [x] CHK002 One coral CTA per view. Member view: *News posten* (admins) or none (plain members); the card's buttons are all `secondary`. Non-member view: *Beitritt anfragen*, unchanged (screenshots 01, 03, 05, 09, 10).
- [x] CHK003 No lemon used.
- [x] CHK004 The card's failure line uses `text-danger-fg`, the same idiom as `news-post.component.html:17,85` and `training-session.component.html:52`. The not-a-member note uses `jh-alert tone="info"`, like 058's `join-notice`.
- [x] CHK005 No new colour.

## Typography, numbers & voice

- [x] CHK006 Inherited. No font set in the diff.
- [x] CHK007 No numbers added.
- [x] CHK008 Sentence case: *Team chat*, *Opening…*. German *Team-Chat*, *Wird geöffnet…*. Spanish *Chat del equipo*, *Abriendo…*.
- [x] CHK009 `text-body-sm` (14px) for the error line; buttons are `size="sm"` like the card's existing links.
- [x] CHK010 *"We couldn't open the team chat just now."* / *"You're no longer on this team."*: plain "we"/"you", no shouting, no emoji.

## Layout & spacing

- [x] CHK011 Card buttons are the same `jhButton size="sm"` as the card's existing *Invites*/*Manage* links, whose 44px sizing the directive owns. They stack full-width in the card (screenshots 02, 05).
- [x] CHK012 `gap-xs`, `mt-xs`, `mt-sm` tokens only.
- [x] CHK013 Page column unchanged.
- [x] CHK014 Unchanged.
- [x] CHK030 **German at 375px**: *Team-Chat*, *Admins kontaktieren*, *Einladungen*, *Verwalten* in full; the walk asserted `scrollWidth <= clientWidth` per button (0 clipped). At desktop the rail card holds all labels in full (03, 09). The non-member header at 375px is unchanged (10).
- [x] CHK031 `document.scrollWidth > innerWidth` false at 375px for admin and member.
- [x] CHK032 Full-width stacked buttons with no fixed widths; they wrap and grow with text size like the card's existing links. No new fixed-size container.

## Shape & elevation

- [x] CHK015 Buttons use the directive's `md` radius; the card is the existing `jh-card`.
- [x] CHK016 No shadow added.
- [x] CHK017 Existing `jh-card`, unchanged.
- [x] CHK018 Nothing new floats.

## Motion & states

- [x] CHK019 No transition added; the directive's own apply.
- [x] CHK020 Focus ring from `jhButton` (native `<button>`s).
- [x] CHK021 Directive-owned hover/press.
- [x] CHK022 No animation.
- [x] CHK033 No motion added.

## Iconography

- [x] CHK023 No icon added.
- [x] CHK024 No emoji.

## Accessibility

- [x] CHK025 Text uses existing token pairs: secondary button label on `surface-card`, and `danger-fg` on the white card as in the other inline errors.
- [x] CHK026 Busy is conveyed by the label change (*Wird geöffnet…*) and `disabled`, not by colour alone (screenshot 06). Failure is a sentence.
- [x] CHK027 Native `<button type="button">` elements, keyboard-reachable. The failure line has `role="alert"`; the notice is a `jh-alert`.
- [x] CHK034 No `<img>` added.
- [x] CHK035 DOM order = visual order. In the card: Team chat → Contact admins → Invites → Manage, then the error line. Removing the header block for members removes controls; it does not reorder any.
- [x] CHK036 No heading added (the card keeps its `h2`).

## Browser surfaces

- [x] CHK038 No new scroll region, input or prose link.

## Empty, loading & error states

- [x] CHK028 No empty state involved.
- [x] CHK029 Busy (06), failure (15) and not-a-member (13) are all styled to the system.
- [x] CHK037 No skeleton or spinner. The busy state is the button's own label.

## Feature-specific UI

- [x] CHK039 **Members have no header actions**: the scripted walk read `[data-testid="team-detail"] header` as `[]` for an admin and a plain member at 375px. This also removes the empty full-width row an admin's header used to get. Non-members see `["contact-admins","request-to-join"]`; a pending requester sees `["contact-admins","cancel-request"]` (12).
- [x] CHK040 **The card is the member's one place for actions** (owner decision): admin → *Team-Chat · Einladungen · Verwalten*; plain member → *Team-Chat · Admins kontaktieren · Verwalten* (02, 05).
- [x] CHK041 **The not-a-member notice survives the reload that removes the card**: it renders above the non-member view (13).
- [x] CHK042 **Team chat opens the TEAM conversation, never the Admins thread**: the landed conversation is tagged *Team*, with J's *Admins* thread listed separately in the inbox (04).

## Notes

- **Accepted consequence (owner decision), not a finding**: at 375px the member card stacks below
  the main column, after roster, events, results, badges, news and *Was passiert gerade* (01, 05).
- **Seen, pre-existing, out of scope**: the *Trainings* card's *Trainings ansehen* link is
  content-width while the Team-Tools card's actions are full-width (01, 05). It predates this
  feature and was not changed.
- No conflict with DESIGN.md.
