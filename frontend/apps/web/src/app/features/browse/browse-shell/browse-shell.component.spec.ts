import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
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

/**
 * The active tab's dark label (GH #318). `routerLinkActive="text-heading"` over a base
 * `text-muted` never applied — two bare utilities have the same specificity, and the stylesheet's
 * order put `.text-muted` last — so the look is a `[class]` binding with two exclusive branches,
 * read off the directive's own `isActive`. The base list names no colour at all.
 */
describe('BrowseShellComponent — active tab', () => {
  @Component({ template: '<jh-browse-shell title="Browse" />', imports: [BrowseShellComponent] })
  class Host {}

  it('paints only the active tab with the heading colour, and marks it aria-current', async () => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideRouter([{ path: 'browse/:tab', component: Host }])],
    });
    const harness = await RouterTestingHarness.create('/browse/teams');
    // RouterLinkActive settles in a microtask after the navigation.
    await harness.fixture.whenStable();
    harness.detectChanges();

    const tab = (id: string) => harness.routeNativeElement?.querySelector(`[data-testid="${id}"]`) as HTMLElement;

    expect(tab('browse-tab-teams').classList).toContain('text-heading');
    expect(tab('browse-tab-teams').classList).not.toContain('text-muted');
    expect(tab('browse-tab-teams').getAttribute('aria-current')).toBe('page');

    expect(tab('browse-tab-events').classList).toContain('text-muted');
    expect(tab('browse-tab-events').classList).not.toContain('text-heading');
    expect(tab('browse-tab-events').hasAttribute('aria-current')).toBe(false);
  });
});
