import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CardComponent } from './card.component';

@Component({
  imports: [CardComponent],
  template: `
    <jh-card [accent]="accent()" [interactive]="interactive()">
      <p data-testid="content">Body</p>
    </jh-card>
  `,
})
class HostComponent {
  readonly accent = signal(false);
  readonly interactive = signal(false);
}

describe('CardComponent (jh-card)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function card(): HTMLElement {
    return fixture.nativeElement.querySelector('jh-card') as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
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
});
