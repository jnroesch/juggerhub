import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertComponent, AlertTone } from './alert.component';

@Component({
  imports: [AlertComponent],
  template: `<jh-alert [tone]="tone()" class="mt-md">Something went wrong.</jh-alert>`,
})
class HostComponent {
  readonly tone = signal<AlertTone>('danger');
}

describe('AlertComponent (jh-alert)', () => {
  let fixture: ComponentFixture<HostComponent>;

  function alert(): HTMLElement {
    return fixture.nativeElement.querySelector('jh-alert') as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('is announced to assistive tech and boxed', () => {
    expect(alert().getAttribute('role')).toBe('alert');
    expect(alert().classList).toContain('rounded-md');
    expect(alert().classList).toContain('border');
  });

  it('defaults to the single danger red (danger-fg)', () => {
    expect(alert().classList).toContain('text-danger-fg');
    expect(alert().classList).toContain('bg-danger-bg');
    expect(alert().classList).not.toContain('text-danger'); // legacy red-5 retired
  });

  it('preserves caller layout classes', () => {
    expect(alert().classList).toContain('mt-md');
  });

  it('switches token triples by tone without leaking the previous tone', () => {
    fixture.componentInstance.tone.set('success');
    fixture.detectChanges();
    expect(alert().classList).toContain('text-success-fg');
    expect(alert().classList).not.toContain('text-danger-fg');
  });
});
