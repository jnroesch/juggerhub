import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { LowercaseInputDirective } from './lowercase-input.directive';

@Component({
  imports: [ReactiveFormsModule, LowercaseInputDirective],
  template: `<input jhLowercase [formControl]="handle" data-testid="field" />`,
})
class HostComponent {
  readonly handle = new FormControl('', { nonNullable: true });
}

describe('LowercaseInputDirective (jhLowercase)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let input: HTMLInputElement;

  /** Simulate typing/pasting: set the DOM value, then fire the event both listeners hear. */
  function type(value: string, caret = value.length): void {
    input.value = value;
    input.setSelectionRange(caret, caret);
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('[data-testid="field"]');
  });

  it('folds typed uppercase to lowercase in the DOM and the form control', () => {
    type('Rheinfeuer');

    expect(input.value).toBe('rheinfeuer');
    expect(fixture.componentInstance.handle.value).toBe('rheinfeuer');
  });

  it('folds a pasted mixed-case value', () => {
    type('RHEIN-Feuer-2024');

    expect(fixture.componentInstance.handle.value).toBe('rhein-feuer-2024');
  });

  it('leaves everything except case alone, so the format warning still fires', () => {
    type('Rhein Feuer!');

    expect(fixture.componentInstance.handle.value).toBe('rhein feuer!');
  });

  it('keeps the caret in place when a capital is fixed mid-value', () => {
    // "rheinFeuer" with the cursor just after the capital that was typed at index 5.
    type('rheinFeuer', 6);

    expect(input.value).toBe('rheinfeuer');
    expect(input.selectionStart).toBe(6);
  });

  it('does not touch an already-lowercase value', () => {
    type('rheinfeuer');

    expect(input.value).toBe('rheinfeuer');
    expect(fixture.componentInstance.handle.value).toBe('rheinfeuer');
  });
});
