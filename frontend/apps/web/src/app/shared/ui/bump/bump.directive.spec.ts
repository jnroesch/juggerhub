import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BumpDirective } from './bump.directive';

@Component({
  imports: [BumpDirective],
  template: `
    <span [jhBump]="count()">
      @if (count() > 0) {
        <span data-bump>{{ count() }}</span>
      }
    </span>
  `,
})
class HostComponent {
  readonly count = signal(0);
}

describe('BumpDirective', () => {
  async function render(initial: number) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.count.set(initial);
    await fixture.whenStable();
    const badge = () => (fixture.nativeElement as HTMLElement).querySelector('[data-bump]');
    const set = async (value: number) => {
      fixture.componentInstance.count.set(value);
      await fixture.whenStable();
    };
    return { badge, set };
  }

  it('does not bump for the count the page loads with', async () => {
    const { badge } = await render(3);
    expect(badge()?.classList).not.toContain('jh-bump');
  });

  it('bumps when the count rises, including the 0 → 1 that creates the badge', async () => {
    const { badge, set } = await render(0);
    await set(1);
    expect(badge()?.classList).toContain('jh-bump');
  });

  it('does not bump when the count falls', async () => {
    const { badge, set } = await render(4);
    await set(2);
    expect(badge()?.classList).not.toContain('jh-bump');
  });
});
