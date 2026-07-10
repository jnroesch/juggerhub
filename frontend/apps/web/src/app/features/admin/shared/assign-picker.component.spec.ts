import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Signal, WritableSignal } from '@angular/core';
import { AssignPickerComponent } from './assign-picker.component';
import { RecognitionDefinition } from '../../../core/models/recognition.models';

function def(over: Partial<RecognitionDefinition>): RecognitionDefinition {
  return {
    id: crypto.randomUUID(),
    name: 'Type',
    description: 'A type.',
    appliesToPlayers: true,
    appliesToTeams: true,
    isRetired: false,
    hasIcon: false,
    grantedCount: 0,
    createdAt: '2026-03-04T00:00:00Z',
    ...over,
  };
}

interface PickerApi {
  pickerItems: Signal<RecognitionDefinition[]>;
  selectedDefId: WritableSignal<string | null>;
  select(id: string): void;
  grant(): void;
}

describe('AssignPickerComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function create(
    subjectType: 'player' | 'team',
    subjectRef: string,
    badges: RecognitionDefinition[],
  ): ComponentFixture<AssignPickerComponent> {
    const fixture = TestBed.createComponent(AssignPickerComponent);
    fixture.componentRef.setInput('subjectType', subjectType);
    fixture.componentRef.setInput('subjectRef', subjectRef);
    fixture.componentRef.setInput('subjectLabel', 'Subject');
    fixture.detectChanges(); // constructor loads catalogues
    httpMock.expectOne((r) => r.url === '/api/v1/admin/badges').flush({ items: badges, totalCount: badges.length, skip: 0, take: 100 });
    httpMock.expectOne((r) => r.url === '/api/v1/admin/achievements').flush({ items: [], totalCount: 0, skip: 0, take: 100 });
    fixture.detectChanges();
    return fixture;
  }

  const api = (f: ComponentFixture<AssignPickerComponent>) => f.componentInstance as unknown as PickerApi;

  it('grants a badge to a TEAM by teamSlug and emits granted', () => {
    const badge = def({ appliesToTeams: true });
    const fixture = create('team', 'berlin-bison', [badge]);
    let granted = false;
    fixture.componentInstance.granted.subscribe(() => (granted = true));

    const a = api(fixture);
    a.select(badge.id);
    a.grant();

    const req = httpMock.expectOne(`/api/v1/admin/badges/${badge.id}/awards`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ teamSlug: 'berlin-bison', note: null });
    req.flush({});
    expect(granted).toBe(true);
  });

  it('grants a badge to a PLAYER by playerHandle', () => {
    const badge = def({ appliesToPlayers: true });
    const fixture = create('player', 'ada', [badge]);
    const a = api(fixture);
    a.select(badge.id);
    a.grant();

    const req = httpMock.expectOne(`/api/v1/admin/badges/${badge.id}/awards`);
    expect(req.request.body).toEqual({ playerHandle: 'ada', note: null });
    req.flush({});
  });

  it('offers only types that apply to the subject kind', () => {
    const teamOnly = def({ name: 'Verified club', appliesToPlayers: false, appliesToTeams: true });
    const playerOnly = def({ name: 'Fair play', appliesToPlayers: true, appliesToTeams: false });
    const fixture = create('team', 'berlin-bison', [teamOnly, playerOnly]);
    expect(api(fixture).pickerItems().map((d) => d.name)).toEqual(['Verified club']);
  });
});
