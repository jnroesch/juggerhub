import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChipDirective, ChipTone } from './chip.directive';

@Component({
  imports: [ChipDirective],
  template: `
    <span jhChip [tone]="tone()" class="ml-xs" data-testid="label">Beginners welcome</span>
    <button type="button" jhChip [tone]="controlTone()" data-testid="control">Runner</button>
    <a jhChip href="#" data-testid="link">Team</a>
  `,
})
class HostComponent {
  readonly tone = signal<ChipTone>('muted');
  readonly controlTone = signal<ChipTone>('outline');
}

describe('ChipDirective (jhChip)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function chip(testid: string): HTMLElement {
    return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`) as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('gives a label chip real vertical padding — the defect #278 reported', () => {
    const cls = chip('label').classList;
    expect(cls).toContain('py-2xs');
    expect(cls).toContain('px-sm');
    expect(cls).not.toContain('py-0');
    expect(cls).not.toContain('py-0.5');
    expect(cls).not.toContain('py-3xs');
  });

  it('draws every chip as a pill, in one text step per form', () => {
    expect(chip('label').classList).toContain('rounded-pill');
    expect(chip('label').classList).toContain('text-caption');
    expect(chip('control').classList).toContain('rounded-pill');
    expect(chip('control').classList).toContain('text-body-sm');
  });

  it('meets the 44px touch target on a chip that is a control', () => {
    expect(chip('control').classList).toContain('min-h-11');
    expect(chip('link').classList).toContain('min-h-11');
  });

  it('leaves a label chip at label size — it is not a target', () => {
    expect(chip('label').classList).not.toContain('min-h-11');
    expect(chip('label').classList).not.toContain('cursor-pointer');
  });

  it('gives a control a visible focus ring and a hover, like every other control', () => {
    const cls = chip('control').classList;
    expect(cls).toContain('focus-visible:ring-focus');
    expect(cls).toContain('hover:bg-surface-sunken');
    expect(cls).toContain('disabled:pointer-events-none');
  });

  it('carries the whole appearance in the tone, and swaps it cleanly', () => {
    expect(chip('label').classList).toContain('bg-surface-muted');

    fixture.componentInstance.tone.set('success');
    fixture.detectChanges();

    expect(chip('label').classList).toContain('bg-success-bg');
    expect(chip('label').classList).toContain('text-success-fg');
    expect(chip('label').classList).not.toContain('bg-surface-muted');
    expect(chip('label').classList).not.toContain('text-muted');
  });

  it("swaps a control's hover with its tone", () => {
    fixture.componentInstance.controlTone.set('accent');
    fixture.detectChanges();

    const cls = chip('control').classList;
    expect(cls).toContain('bg-surface-accent-soft');
    expect(cls).toContain('hover:bg-coral-1');
    expect(cls).not.toContain('hover:bg-surface-sunken');
  });

  it('preserves the layout classes the call site brought', () => {
    expect(chip('label').classList).toContain('ml-xs');
  });
});
