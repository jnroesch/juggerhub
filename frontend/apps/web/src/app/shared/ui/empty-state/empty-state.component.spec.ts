import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { CardComponent } from '../card/card.component';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  imports: [EmptyStateComponent],
  template: `
    <jh-empty-state [heading]="heading()" [inline]="inline()">
      No messages yet.
      <a data-testid="action" href="#">Start one</a>
    </jh-empty-state>
  `,
})
class HostComponent {
  readonly heading = signal<string | null>(null);
  readonly inline = signal(false);
}

describe('EmptyStateComponent (jh-empty-state)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function container(): HTMLElement {
    return fixture.nativeElement.querySelector('jh-empty-state > div') as HTMLElement;
  }

  function card(): CardComponent | null {
    return fixture.debugElement.query(By.directive(CardComponent))?.componentInstance ?? null;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  /*
   * The surface is the shared card primitive rather than a second hand-rolled copy of it
   * (GH #302) — but an empty state sets its own generous vertical room, which is why it
   * takes the card `flush` instead of the 24px body padding.
   */
  it('draws the default variant on the shared card, flush, with muted centered text', () => {
    expect(card()?.padding()).toBe('flush');
    expect(container().classList).toContain('py-2xl');
    expect(container().classList).toContain('text-center');
    expect(container().querySelector('.text-muted')).not.toBeNull();
  });

  it('projects the message and an optional next-step action', () => {
    expect(container().textContent).toContain('No messages yet.');
    expect(container().querySelector('[data-testid="action"]')).not.toBeNull();
  });

  it('renders a heading when provided', () => {
    fixture.componentInstance.heading.set('Nothing here');
    fixture.detectChanges();
    expect(container().querySelector('h2')?.textContent).toBe('Nothing here');
  });

  it('drops the card chrome in the inline variant', () => {
    fixture.componentInstance.inline.set(true);
    fixture.detectChanges();
    expect(card()).toBeNull();
    expect(container().classList).toContain('text-center');
  });

  /* The body is one `<ng-template>` behind both variants: projection must survive a switch. */
  it('keeps the projected content when the variant changes', () => {
    fixture.componentInstance.inline.set(true);
    fixture.detectChanges();
    expect(container().textContent).toContain('No messages yet.');
    expect(container().querySelector('[data-testid="action"]')).not.toBeNull();
  });
});
