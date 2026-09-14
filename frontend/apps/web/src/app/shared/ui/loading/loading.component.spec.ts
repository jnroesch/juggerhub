import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { LoadingComponent, PATIENT_THRESHOLD_MS } from './loading.component';

const en: Translation = require('../../../../../public/i18n/en.json');
const de: Translation = require('../../../../../public/i18n/de.json');

describe('LoadingComponent (jh-loading)', () => {
  let fixture: ComponentFixture<LoadingComponent>;

  function line(): HTMLElement {
    return fixture.nativeElement.querySelector('p') as HTMLElement;
  }

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [LoadingComponent, translocoTestingModule({ en, de })],
    });
    fixture = TestBed.createComponent(LoadingComponent);
  });

  afterEach(() => jest.useRealTimers());

  it('defaults to a muted "Loading…" line announced to assistive tech', () => {
    fixture.detectChanges();
    expect(line().textContent).toBe('Loading…');
    expect(line().classList).toContain('text-muted');
    expect(line().classList).toContain('text-body-sm');
    expect(line().getAttribute('role')).toBe('status');
  });

  it('accepts a contextual label', () => {
    fixture.componentRef.setInput('label', 'Loading your profile…');
    fixture.detectChanges();
    expect(line().textContent).toBe('Loading your profile…');
  });

  it('translates the default line into the app language (GH #290)', () => {
    // Most call sites render a bare <jh-loading />, so the default is what German readers see.
    TestBed.inject(TranslocoService).setActiveLang('de');
    fixture.detectChanges();
    expect(line().textContent).toBe('Wird geladen…');
  });

  it('centers when align is center', () => {
    fixture.componentRef.setInput('align', 'center');
    fixture.detectChanges();
    expect(line().classList).toContain('text-center');
  });

  describe('patient line (feature 028)', () => {
    it('stays silent for a fast load', () => {
      fixture.detectChanges();
      jest.advanceTimersByTime(PATIENT_THRESHOLD_MS - 1);
      fixture.detectChanges();

      expect(line().textContent).toBe('Loading…');
    });

    it('switches to patient copy once the load runs past the threshold', () => {
      fixture.detectChanges();
      jest.advanceTimersByTime(PATIENT_THRESHOLD_MS + 1);
      fixture.detectChanges();

      expect(line().textContent).toBe('Still loading…');
    });

    it('accepts contextual patient copy', () => {
      fixture.componentRef.setInput('patientLabel', 'Still finding teams…');
      fixture.detectChanges();
      jest.advanceTimersByTime(PATIENT_THRESHOLD_MS + 1);
      fixture.detectChanges();

      expect(line().textContent).toBe('Still finding teams…');
    });

    it('translates the default patient copy, even after a contextual label', () => {
      // A caller may pass only `label`; the patient line must not fall back to English then.
      TestBed.inject(TranslocoService).setActiveLang('de');
      fixture.componentRef.setInput('label', 'Team wird geladen…');
      fixture.detectChanges();
      jest.advanceTimersByTime(PATIENT_THRESHOLD_MS + 1);
      fixture.detectChanges();

      expect(line().textContent).toBe('Wird noch geladen…');
    });

    it('swaps copy in place, keeping the same announced element and styling', () => {
      // No layout shift and no second live region: DESIGN.md requires the reassurance to
      // reuse the line that is already there rather than add a banner or overlay.
      fixture.detectChanges();
      const before = line();
      jest.advanceTimersByTime(PATIENT_THRESHOLD_MS + 1);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelectorAll('p')).toHaveLength(1);
      expect(line()).toBe(before);
      expect(line().getAttribute('role')).toBe('status');
      expect(line().classList).toContain('text-muted');
      expect(line().classList).toContain('text-body-sm');
    });

    it('cancels its timer on destroy so a finished load cannot flip the label', () => {
      fixture.detectChanges();
      fixture.destroy();

      expect(() => jest.advanceTimersByTime(PATIENT_THRESHOLD_MS + 1)).not.toThrow();
    });
  });
});
