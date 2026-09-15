import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { AdminTeamPickerComponent } from './team-picker.component';

const team = { slug: 'eclipse', name: 'Eclipse', location: 'Basel', type: 'CityTeam', memberCount: 12, awardCount: 0 };

describe('AdminTeamPickerComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function mount(): ComponentFixture<AdminTeamPickerComponent> {
    const fixture = TestBed.createComponent(AdminTeamPickerComponent);
    fixture.componentRef.setInput('subjectLabel', 'Ecplise');
    fixture.componentRef.setInput('initialQuery', 'Ecplise');
    fixture.detectChanges();
    return fixture;
  }

  it('searches for the given name and emits the team the admin picks — never one on its own', () => {
    const f = mount();
    const picked = jest.fn();
    f.componentInstance.picked.subscribe(picked);

    const search = http.expectOne((r) => r.url === '/api/v1/admin/teams');
    expect(search.request.params.get('q')).toBe('Ecplise');
    search.flush({ items: [team], totalCount: 1, skip: 0, take: 10 });
    f.detectChanges();
    expect(picked).not.toHaveBeenCalled();

    (f.nativeElement.querySelector('[data-testid="pick-team-eclipse"]') as HTMLButtonElement).click();
    expect(picked).toHaveBeenCalledWith({ slug: 'eclipse', name: 'Eclipse' });
  });

  it('closes on Escape', () => {
    const f = mount();
    http.expectOne((r) => r.url === '/api/v1/admin/teams').flush({ items: [], totalCount: 0, skip: 0, take: 10 });
    const closed = jest.fn();
    f.componentInstance.closed.subscribe(closed);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(closed).toHaveBeenCalled();
  });
});
