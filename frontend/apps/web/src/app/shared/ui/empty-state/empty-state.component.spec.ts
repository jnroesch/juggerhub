import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
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

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('uses the bordered card container by default with muted, centered text', () => {
    expect(container().classList).toContain('border-border-muted');
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
    expect(container().classList).not.toContain('border-border-muted');
    expect(container().classList).toContain('text-center');
  });
});
