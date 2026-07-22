import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NeedsYouItem, NeedsYouKind } from '../../../core/models/home.models';
import { NeedsYouCardComponent } from './needs-you-card.component';

function item(kind: NeedsYouKind, partial: Partial<NeedsYouItem> = {}): NeedsYouItem {
  return {
    kind,
    id: 'x1',
    title: 'Something',
    context: 'context',
    linkTarget: null,
    occurredAt: '2026-07-20T10:00:00Z',
    ...partial,
  };
}

describe('NeedsYouCardComponent', () => {
  let httpMock: HttpTestingController;

  function mount(items: NeedsYouItem[]): { fixture: ComponentFixture<NeedsYouCardComponent>; resolved: string[] } {
    const fixture = TestBed.createComponent(NeedsYouCardComponent);
    const resolved: string[] = [];
    fixture.componentRef.setInput('items', items);
    fixture.componentInstance.resolved.subscribe((id) => resolved.push(id));
    fixture.detectChanges();
    return { fixture, resolved };
  }

  const root = (f: ComponentFixture<NeedsYouCardComponent>) =>
    f.nativeElement.querySelector('[data-testid="needs-you"]') as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('renders nothing when there are no items', () => {
    const { fixture } = mount([]);
    expect(root(fixture)).toBeNull();
  });

  it('renders the block with a count when there are items', () => {
    const { fixture } = mount([item('TeamInvite'), item('MarketApplication', { id: 'x2' })]);
    expect(root(fixture)).toBeTruthy();
  });

  it('accepting a team invite POSTs to the token accept endpoint and emits resolved', () => {
    const { fixture, resolved } = mount([item('TeamInvite', { id: 'tok-abc' })]);
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    const req = httpMock.expectOne('/api/v1/invitations/tok-abc/accept');
    expect(req.request.method).toBe('POST');
    req.flush({});
    fixture.detectChanges();
    expect(resolved).toEqual(['tok-abc']);
  });

  it('"I\'m in" on a party request POSTs to the party join endpoint', () => {
    const { fixture } = mount([item('PartyRequest', { id: 'party-1' })]);
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    const req = httpMock.expectOne('/api/v1/parties/party-1/join');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('a market application is shown as pending with no action buttons', () => {
    const { fixture } = mount([item('MarketApplication', { id: 'app-1' })]);
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(root(fixture)!.textContent).toContain('pending');
  });
});
