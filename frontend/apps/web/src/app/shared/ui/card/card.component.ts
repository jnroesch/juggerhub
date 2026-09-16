import { Component, input } from '@angular/core';

/** How much room the card leaves between its border and its content. */
export type CardPadding = 'default' | 'dense' | 'flush';

/**
 * Shared card primitive (feature 024, completed by GH #302). A white `surface-card` panel
 * with a 1px muted border, `lg` radius, a soft `sm` shadow and DESIGN.md's `spacing.6`
 * (24px) body padding — the DESIGN.md card, whole. `accent` adds the signature 4px
 * coral→sage gradient strip along the top; `interactive` adds the 3px hover lift + deeper
 * shadow for cards that are themselves a link or a button. Content is projected, and the
 * surface is styled on the host (see card.component.css), so callers just wrap content.
 *
 * The **attribute selector is the point of the primitive**: `<section jhCard>`,
 * `<li jhCard>`, `<a jhCard>`, `<ul jhCard padding="flush">` all keep the element the
 * markup actually needs. #302 found 59 hand-rolled `rounded-lg border bg-surface-card`
 * surfaces beside 40 `<jh-card>` elements, and the reason they were hand-rolled is
 * legible in them: they are sections, list items and links, and swapping in a `jh-card`
 * *element* would have thrown the semantics away. Reach for the attribute first; the
 * element form is for the cases with no meaning of their own.
 *
 * Padding used to be the caller's, "because cards vary". It did not vary usefully — it
 * drifted: 32 call sites at 16px, 6 at 24px, 1 at 20px, against a design system that says
 * 24px, and #278 reported the result as text crowding the edge of the box. So the
 * primitive owns it now, and the variation that is real is named:
 *
 * - `default` — 24px (`spacing.6`), DESIGN.md's card padding. The one nobody has to remember.
 * - `dense` — 16px, for a compact repeated row where 24px would push the list off the screen.
 * - `flush` — none, for a card whose children carry their own padding: a `divide-y` list,
 *   a table, a header strip, anything full-bleed.
 */
@Component({
  selector: 'jh-card, [jhCard]',
  templateUrl: './card.component.html',
  styleUrl: './card.component.css',
  host: {
    /*
     * `block` is a host *class*, not a `display: block` in card.component.css, and that is
     * load-bearing for the attribute form. Angular injects a component's styles after the
     * global sheet, so `:host { display: block }` and a caller's `.flex` tie on specificity
     * and the component wins — which silently flattens `<a jhCard class="flex …">` into a
     * stack. As a class it sits in Tailwind's own utilities layer, where `block` is emitted
     * before `flex` / `inline-flex` / `grid` / `hidden`, so the caller's display wins the way
     * it does on any other element. (`<jh-card class="flex …">` was already laid out wrong
     * for this reason before #302.)
     */
    class: 'block',
    '[class.p-xl]': "padding() === 'default'",
    '[class.p-md]': "padding() === 'dense'",
    '[class.jh-card--interactive]': 'interactive()',
    '[class.jh-card--overflow-visible]': 'overflowVisible()',
  },
})
export class CardComponent {
  /** Body padding: `default` (24px), `dense` (16px) or `flush` (none). See the class doc. */
  readonly padding = input<CardPadding>('default');
  /**
   * Render the 4px brand-gradient accent strip at the top. DESIGN.md reserves it for the
   * single focal card on a page that holds nothing else — sign-in, register, an invite
   * landing. It is a signature, not decoration: on a grid of cards it is noise.
   */
  readonly accent = input(false, { transform: booleanish });
  /**
   * Add the hover lift + deeper shadow. Only for a card that is *itself* the control —
   * an `<a jhCard>` or `<button jhCard>`. A card that merely contains links must not
   * lift, or the pointer promises a target that isn't there.
   */
  readonly interactive = input(false, { transform: booleanish });
  /**
   * Drop the default `overflow: hidden` clip so a positioned child (an absolute
   * dropdown / popover) can escape the card's rounded box. Use only on cards
   * without the `accent` strip, which relies on the clip for its rounded top.
   */
  readonly overflowVisible = input(false, { transform: booleanish });
}

function booleanish(value: boolean | '' | null | undefined): boolean {
  return value === '' || value === true;
}
