import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadingComponent } from './loading.component';

describe('LoadingComponent (jh-loading)', () => {
  let fixture: ComponentFixture<LoadingComponent>;

  function line(): HTMLElement {
    return fixture.nativeElement.querySelector('p') as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [LoadingComponent] });
    fixture = TestBed.createComponent(LoadingComponent);
  });

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

  it('centers when align is center', () => {
    fixture.componentRef.setInput('align', 'center');
    fixture.detectChanges();
    expect(line().classList).toContain('text-center');
  });
});
