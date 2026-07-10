import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Signal, WritableSignal } from '@angular/core';
import { AdminCatalogueComponent } from './admin-catalogue.component';
import { RecognitionDefinition, RecognitionKind } from '../../../core/models/recognition.models';

function def(over: Partial<RecognitionDefinition>): RecognitionDefinition {
  return {
    id: crypto.randomUUID(),
    name: 'Type',
    description: 'A type.',
    appliesToPlayers: true,
    appliesToTeams: false,
    isRetired: false,
    hasIcon: false,
    grantedCount: 0,
    createdAt: '2026-03-04T00:00:00Z',
    ...over,
  };
}

const BADGES: RecognitionDefinition[] = [
  def({ name: 'Fair play', appliesToPlayers: true, grantedCount: 5 }),
  def({ name: 'Verified club', appliesToPlayers: false, appliesToTeams: true, grantedCount: 2 }),
  def({ name: 'Beta tester', isRetired: true, grantedCount: 3 }),
];

/** Protected surface we drive directly (signals are callable + .set). */
interface CatalogueApi {
  kind: WritableSignal<RecognitionKind>;
  filter: WritableSignal<'all' | 'active' | 'retired'>;
  visible: Signal<RecognitionDefinition[]>;
  total: Signal<number>;
  retiredCount: Signal<number>;
  setKind(kind: RecognitionKind): void;
  setFilter(f: 'all' | 'active' | 'retired'): void;
  openCreate(): void;
  openEdit(def: RecognitionDefinition): void;
  formOpen: WritableSignal<boolean>;
  editing: WritableSignal<RecognitionDefinition | null>;
  formKind: WritableSignal<RecognitionKind>;
  formValid: Signal<boolean>;
  formName: WritableSignal<string>;
  formDescription: WritableSignal<string>;
  formPlayers: WritableSignal<boolean>;
  formTeams: WritableSignal<boolean>;
}

describe('AdminCatalogueComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function create(items: RecognitionDefinition[] = BADGES): ComponentFixture<AdminCatalogueComponent> {
    const fixture = TestBed.createComponent(AdminCatalogueComponent);
    fixture.detectChanges(); // constructor → load() badges
    httpMock.expectOne((r) => r.url === '/api/v1/admin/badges').flush({ items, totalCount: items.length, skip: 0, take: 100 });
    fixture.detectChanges();
    return fixture;
  }

  const api = (f: ComponentFixture<AdminCatalogueComponent>) => f.componentInstance as unknown as CatalogueApi;

  it('lists the badge catalogue with total and retired counts', () => {
    const a = api(create());
    expect(a.total()).toBe(3);
    expect(a.retiredCount()).toBe(1);
    expect(a.visible().length).toBe(3);
  });

  it('filters by status', () => {
    const a = api(create());
    a.setFilter('active');
    expect(a.visible().map((d) => d.name)).toEqual(['Fair play', 'Verified club']);
    a.setFilter('retired');
    expect(a.visible().map((d) => d.name)).toEqual(['Beta tester']);
  });

  it('switching the toggle loads the other catalogue', () => {
    const a = api(create());
    a.setKind('achievement');
    const req = httpMock.expectOne((r) => r.url === '/api/v1/admin/achievements');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 100 });
    expect(a.kind()).toBe('achievement');
  });

  it('create opens an empty form that is invalid until name, description and an applies-to are set', () => {
    const a = api(create());
    a.openCreate();
    expect(a.formOpen()).toBe(true);
    expect(a.editing()).toBeNull();
    expect(a.formValid()).toBe(false); // name/description empty
    a.formName.set('New badge');
    a.formDescription.set('Does a thing.');
    a.formPlayers.set(false);
    a.formTeams.set(false);
    expect(a.formValid()).toBe(false); // no applies-to
    a.formPlayers.set(true);
    expect(a.formValid()).toBe(true);
  });

  it('edit pre-fills the form and locks the kind to the current catalogue', () => {
    const a = api(create());
    a.openEdit(BADGES[1]);
    expect(a.editing()?.name).toBe('Verified club');
    expect(a.formName()).toBe('Verified club');
    expect(a.formTeams()).toBe(true);
    expect(a.formKind()).toBe('badge');
    expect(a.formValid()).toBe(true);
  });
});
