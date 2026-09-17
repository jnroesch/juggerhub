import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InvitableUser, UserRelation } from '../../../../core/models/team.models';
import { translocoTestingModule } from '../../../../../testing/transloco-testing';
import { InviteSearchComponent } from './invite-search.component';

function user(handle: string, relation: UserRelation): InvitableUser {
  return { userId: `u-${handle}`, handle, displayName: handle.toUpperCase(), location: 'Kiel, Germany', relation };
}

/** A host, because the component takes a required input. */
@Component({
  imports: [InviteSearchComponent],
  template: `<jh-invite-search [slug]="slug()" (invited)="invitedCount = invitedCount + 1" />`,
})
class HostComponent {
  readonly slug = signal('kiel-krakens');
  invitedCount = 0;
}

describe('InviteSearchComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    jest.useFakeTimers(); // the search debounces 300ms
    TestBed.configureTestingModule({
      imports: [translocoTestingModule(), HostComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
  });

  function el<T extends HTMLElement>(testId: string): T {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function search(term: string): void {
    const input = el<HTMLInputElement>('user-search');
    input.value = term;
    input.dispatchEvent(new Event('input'));
    jest.advanceTimersByTime(300);
    fixture.detectChanges();
  }

  function searchRequest() {
    return httpMock.expectOne((r) => r.url === '/api/v1/teams/kiel-krakens/invitations/user-search');
  }

  function flushResults(...users: InvitableUser[]): void {
    searchRequest().flush({ items: users, totalCount: users.length });
    fixture.detectChanges();
  }

  it('searches the team it was given, after the debounce', () => {
    const input = el<HTMLInputElement>('user-search');
    input.value = 'ana';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    // Nothing asked yet — the debounce has not elapsed.
    httpMock.expectNone((r) => r.url.includes('user-search'));

    jest.advanceTimersByTime(300);
    const request = searchRequest();
    expect(request.request.params.get('q')).toBe('ana');
    request.flush({ items: [], totalCount: 0 });
    fixture.detectChanges();
  });

  /** The relation is a rule about whether an invite may be offered at all, not a label. */
  it('offers an invite only to someone invitable', () => {
    search('a');
    flushResults(user('ana', 'Invitable'), user('bo', 'Invited'), user('cy', 'Member'));

    expect(el('invite-ana')).not.toBeNull();
    expect(el('invite-bo')).toBeNull();
    expect(el('invite-cy')).toBeNull();
  });

  it('flips a row to invited and tells the parent', () => {
    search('a');
    flushResults(user('ana', 'Invitable'));

    el('invite-ana').click();
    const request = httpMock.expectOne('/api/v1/teams/kiel-krakens/invitations');
    expect(request.request.body).toEqual({ userId: 'u-ana' });
    request.flush({ id: 'i1' });
    fixture.detectChanges();

    expect(el('invite-ana')).toBeNull();
    expect(fixture.componentInstance.invitedCount).toBe(1);
  });

  /** FR-026 — one failure must not disturb the invitations that succeeded. */
  it('reports a failed invitation without touching the other rows', () => {
    search('a');
    flushResults(user('ana', 'Invitable'), user('bo', 'Invitable'));

    el('invite-ana').click();
    httpMock.expectOne('/api/v1/teams/kiel-krakens/invitations').flush({ id: 'i1' });
    fixture.detectChanges();

    el('invite-bo').click();
    httpMock
      .expectOne('/api/v1/teams/kiel-krakens/invitations')
      .flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el('invite-search-error')).not.toBeNull();
    // ana stayed invited, bo is still offered.
    expect(el('invite-ana')).toBeNull();
    expect(el('invite-bo')).not.toBeNull();
  });

  /** FR-025 — a search matching nobody says so rather than rendering an empty area. */
  it('says so when nobody matches', () => {
    expect(el('user-search-empty')).toBeNull(); // nothing searched yet

    search('zzz');
    flushResults();

    expect(el('user-search-empty')).not.toBeNull();
  });

  /**
   * A failed search is not an answer. It must also not tear the subscription down — the defect
   * class the handle check in `TeamCreateComponent` documents, which this code carried before it
   * was extracted.
   */
  it('says a failed search failed, and still searches the next term typed', () => {
    search('ana');
    searchRequest().error(new ProgressEvent('network error'));
    fixture.detectChanges();

    expect(el('user-search-failed')).not.toBeNull();
    expect(el('user-search-empty')).toBeNull();

    search('bo');
    flushResults(user('bo', 'Invitable'));

    expect(el('user-search-failed')).toBeNull();
    expect(el('invite-bo')).not.toBeNull();
  });

  it('clears the results when the box is emptied', () => {
    search('ana');
    flushResults(user('ana', 'Invitable'));
    expect(el('invite-ana')).not.toBeNull();

    search('');

    expect(el('invite-ana')).toBeNull();
    expect(el('user-search-empty')).toBeNull();
  });
});
