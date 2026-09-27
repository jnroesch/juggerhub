import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RiseDirective, RiseScope } from './rise.directive';

@Component({
  imports: [RiseDirective],
  providers: [RiseScope],
  template: `
    <ul>
      @for (item of items(); track item) {
        <li [jhRise]="$index">{{ item }}</li>
      }
    </ul>
  `,
})
class PageComponent {
  readonly items = signal<readonly string[]>([]);
}

@Component({
  imports: [RiseDirective],
  template: `<p [jhRise]="0">no scope</p>`,
})
class UnscopedComponent {}

describe('RiseDirective', () => {
  const rows = (host: HTMLElement) => Array.from(host.querySelectorAll('li'));

  it('raises the first batch a page renders, staggered by position', async () => {
    const fixture = TestBed.createComponent(PageComponent);
    await fixture.whenStable();

    // The list arrives after the page, as it does from the server.
    fixture.componentInstance.items.set(['a', 'b', 'c']);
    await fixture.whenStable();

    const li = rows(fixture.nativeElement);
    expect(li.map((el) => el.classList.contains('jh-rise'))).toEqual([true, true, true]);
    expect(li.map((el) => el.style.getPropertyValue('--jh-stagger'))).toEqual(['0', '1', '2']);
  });

  it('leaves everything rendered after that batch still — a reload, a filter, the next page', async () => {
    const fixture = TestBed.createComponent(PageComponent);
    fixture.componentInstance.items.set(['a']);
    await fixture.whenStable();

    fixture.componentInstance.items.set(['a', 'b']);
    await fixture.whenStable();

    const [first, second] = rows(fixture.nativeElement);
    expect(first.classList).toContain('jh-rise');
    expect(second.classList).not.toContain('jh-rise');
  });

  it('never rises without a scope above it', async () => {
    const fixture = TestBed.createComponent(UnscopedComponent);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('p')?.classList).not.toContain('jh-rise');
  });
});
