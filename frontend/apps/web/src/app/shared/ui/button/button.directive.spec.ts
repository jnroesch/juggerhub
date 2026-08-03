import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ButtonDirective, ButtonSize, ButtonVariant } from './button.directive';

@Component({
  imports: [ButtonDirective],
  template: `
    <button jhButton [variant]="variant()" [size]="size()" [full]="full()" class="mt-lg">Go</button>
    <a jhButton variant="secondary" href="#">Link</a>
  `,
})
class HostComponent {
  readonly variant = signal<ButtonVariant>('primary');
  readonly size = signal<ButtonSize>('md');
  readonly full = signal(false);
}

describe('ButtonDirective (jhButton)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function btn(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  }
  function link(): HTMLAnchorElement {
    return fixture.nativeElement.querySelector('a') as HTMLAnchorElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('applies the 44px touch target on the default md size', () => {
    expect(btn().classList).toContain('min-h-11');
  });

  it('primary uses the coral brand background, on-accent text, hover glow, and press nudge', () => {
    const cls = btn().classList;
    expect(cls).toContain('bg-brand');
    expect(cls).toContain('text-on-accent');
    expect(cls).toContain('hover:shadow-coral');
    expect(cls).toContain('active:translate-y-px');
    expect(cls).not.toContain('text-white');
  });

  it('always exposes a visible focus ring', () => {
    expect(btn().classList).toContain('focus-visible:ring-focus');
  });

  it('preserves author layout classes on the host', () => {
    expect(btn().classList).toContain('mt-lg');
  });

  it('switches variant classes without leaking the previous variant', () => {
    fixture.componentInstance.variant.set('danger');
    fixture.detectChanges();
    const cls = btn().classList;
    expect(cls).toContain('text-danger-fg');
    expect(cls).not.toContain('bg-brand');
  });

  it('sm size drops below the md target for dense/inline use', () => {
    fixture.componentInstance.size.set('sm');
    fixture.detectChanges();
    expect(btn().classList).toContain('min-h-9');
    expect(btn().classList).not.toContain('min-h-11');
  });

  it('full stretches to container width', () => {
    fixture.componentInstance.full.set(true);
    fixture.detectChanges();
    expect(btn().classList).toContain('w-full');
  });

  it('works on a native anchor with a static variant', () => {
    expect(link().classList).toContain('border-border-strong');
  });
});
