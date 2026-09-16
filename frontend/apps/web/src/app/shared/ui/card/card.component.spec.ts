import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CardComponent, type CardPadding } from './card.component';

@Component({
  imports: [CardComponent],
  template: `
    <jh-card
      [padding]="padding()"
      [accent]="accent()"
      [interactive]="interactive()"
      [overflowVisible]="overflowVisible()"
    >
      <p data-testid="content">Body</p>
    </jh-card>

    <section jhCard data-testid="as-section">
      <p data-testid="section-content">Section body</p>
    </section>

    <ul jhCard padding="flush" data-testid="as-list">
      <li data-testid="row">Row</li>
    </ul>

    <li jhCard [style.--jh-card-border]="emphasised() ? 'var(--border-strong)' : null" data-testid="as-row">
      Row content
    </li>
  `,
})
class HostComponent {
  readonly padding = signal<CardPadding>('default');
  readonly accent = signal(false);
  readonly interactive = signal(false);
  readonly overflowVisible = signal(false);
  readonly emphasised = signal(false);
}

describe('CardComponent (jh-card)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function card(): HTMLElement {
    return fixture.nativeElement.querySelector('jh-card') as HTMLElement;
  }

  function query(testid: string): HTMLElement {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`) as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  /*
   * `block` is a class rather than a `:host { display: block }` so a caller's `flex` / `grid`
   * still wins — Angular injects component styles after the global sheet, so the CSS form
   * would beat the utility on a specificity tie. See the host block in card.component.ts.
   */
  it('takes its display from a utility class, so a caller can override it', () => {
    expect(card().classList).toContain('block');
  });

  it('projects its content', () => {
    expect(card().querySelector('[data-testid="content"]')?.textContent).toBe('Body');
  });

  it('omits the accent strip by default', () => {
    expect(card().querySelector('[aria-hidden="true"]')).toBeNull();
  });

  it('renders the accent strip when accent is set', () => {
    fixture.componentInstance.accent.set(true);
    fixture.detectChanges();
    expect(card().querySelector('[aria-hidden="true"]')).not.toBeNull();
  });

  it('adds the interactive class only when interactive is set', () => {
    expect(card().classList).not.toContain('jh-card--interactive');
    fixture.componentInstance.interactive.set(true);
    fixture.detectChanges();
    expect(card().classList).toContain('jh-card--interactive');
  });

  it('adds the overflow-visible class only when overflowVisible is set', () => {
    expect(card().classList).not.toContain('jh-card--overflow-visible');
    fixture.componentInstance.overflowVisible.set(true);
    fixture.detectChanges();
    expect(card().classList).toContain('jh-card--overflow-visible');
  });

  describe('padding', () => {
    /*
     * DESIGN.md's `card.padding` is `spacing.6` = 24px = `p-xl`. Before #302 the primitive
     * left it to the caller and three values shipped, 16px on 32 of the 40 call sites. A
     * caller that says nothing must now get 24, or the drift simply starts again.
     */
    it('defaults to the DESIGN.md 24px body padding', () => {
      expect(card().classList).toContain('p-xl');
      expect(card().classList).not.toContain('p-md');
    });

    it('drops to 16px when dense', () => {
      fixture.componentInstance.padding.set('dense');
      fixture.detectChanges();
      expect(card().classList).toContain('p-md');
      expect(card().classList).not.toContain('p-xl');
    });

    it('carries no padding utility at all when flush', () => {
      fixture.componentInstance.padding.set('flush');
      fixture.detectChanges();
      expect(card().classList).not.toContain('p-xl');
      expect(card().classList).not.toContain('p-md');
    });
  });

  describe('as an attribute', () => {
    /*
     * The attribute form is what lets a card stay a <section>, an <li>, an <a> or a <ul>.
     * Without it the 59 hand-rolled surfaces #302 found could only have been adopted by
     * throwing their element away.
     */
    it('styles a host element of any kind and projects into it', () => {
      const section = query('as-section');

      expect(section.tagName).toBe('SECTION');
      expect(section.classList).toContain('p-xl');
      expect(section.querySelector('[data-testid="section-content"]')).not.toBeNull();
    });

    /*
     * The border is the one property a caller may reassign, and only through this custom
     * property: a `border-*` utility on the host would lose to the component's own rule.
     */
    it('takes a caller-set border colour through --jh-card-border', () => {
      const row = query('as-row');

      expect(row.style.getPropertyValue('--jh-card-border')).toBe('');
      fixture.componentInstance.emphasised.set(true);
      fixture.detectChanges();
      expect(row.style.getPropertyValue('--jh-card-border')).toBe('var(--border-strong)');
    });

    it('keeps a flush list a real <ul> with its <li> children', () => {
      const list = query('as-list');

      expect(list.tagName).toBe('UL');
      expect(list.classList).not.toContain('p-xl');
      expect(query('row').parentElement).toBe(list);
    });
  });
});
