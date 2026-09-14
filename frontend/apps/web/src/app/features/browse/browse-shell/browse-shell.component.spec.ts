import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { BrowseShellComponent } from './browse-shell.component';

/**
 * The search box against the page's applied search (GH #279): a search restored from the URL, or
 * emptied by "Clear all", must show in the box — without the page's late, trimmed copy ever
 * overwriting what is being typed.
 */
describe('BrowseShellComponent — search box', () => {
  let fixture: ComponentFixture<BrowseShellComponent>;
  let emitted: string[];

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.configureTestingModule({ imports: [translocoTestingModule()], providers: [provideRouter([])] });
    fixture = TestBed.createComponent(BrowseShellComponent);
    fixture.componentRef.setInput('title', 'Trainings');
    emitted = [];
    fixture.componentInstance.query.subscribe((q) => emitted.push(q));
    fixture.detectChanges();
  });

  afterEach(() => jest.useRealTimers());

  const box = () => fixture.nativeElement.querySelector('[data-testid="browse-search"]') as HTMLInputElement;

  function type(value: string): void {
    box().value = value;
    box().dispatchEvent(new Event('input'));
    jest.advanceTimersByTime(250);
  }

  /** What the page does with an emitted search: store it and hand it back as `searchValue`. */
  function pageApplies(value: string): void {
    fixture.componentRef.setInput('searchValue', value);
    fixture.detectChanges();
  }

  it('shows a search restored from the URL', () => {
    pageApplies('open mat');
    expect(box().value).toBe('open mat');
  });

  it('does not overwrite the box with its own echo', () => {
    type('köln ');
    expect(emitted).toEqual(['köln']);
    pageApplies('köln');
    // The trailing space the player is mid-way through typing survives.
    expect(box().value).toBe('köln ');
  });

  it('empties the box on "Clear all", and reports the same search typed again afterwards', () => {
    type('köln');
    pageApplies('köln');

    pageApplies(''); // "Clear all"
    expect(box().value).toBe('');

    type('köln');
    expect(emitted).toEqual(['köln', 'köln']);
  });
});
