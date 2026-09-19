import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InviteLink } from '../../../../core/models/team.models';
import { translocoTestingModule } from '../../../../../testing/transloco-testing';
import { InviteLinkComponent } from './invite-link.component';

const LINK: InviteLink = {
  url: 'https://juggerhub.test/teams/kiel-krakens/join?token=abc',
  token: 'abc',
  // Far enough out that the expiry phrase is stable whatever day the suite runs.
  expiresDate: new Date(Date.now() + 7 * 86_400_000).toISOString(),
};

/** A host, because the component takes a required input. */
@Component({
  imports: [InviteLinkComponent],
  template: `<jh-invite-link [slug]="slug()" (changed)="changes = changes + 1" />`,
})
class HostComponent {
  readonly slug = signal('kiel-krakens');
  changes = 0;
}

describe('InviteLinkComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let httpMock: HttpTestingController;
  let clipboard: { writeText: jest.Mock };

  beforeEach(() => {
    jest.useFakeTimers(); // the "Copied!" flash clears itself after 1.5s
    clipboard = { writeText: jest.fn().mockResolvedValue(undefined) };
    Object.defineProperty(navigator, 'clipboard', { value: clipboard, configurable: true });

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

  function linkRequest() {
    return httpMock.expectOne('/api/v1/teams/kiel-krakens/invitations/link');
  }

  /** The first read, answered with whatever the team has. */
  function load(link: InviteLink | null): void {
    linkRequest().flush(link);
    fixture.detectChanges();
  }

  it(`shows the team's existing link, ready to copy`, () => {
    load(LINK);

    expect(el('invite-link').textContent).toContain(LINK.url);
    expect(el('create-link')).toBeNull();
  });

  /**
   * Neither control may be offered before the read comes back. Offering *Create an invite link*
   * to a team that already has one is the press a person is most likely to make too early, and
   * it would silently retire the link they were about to copy.
   */
  it('offers nothing at all until the read has answered', () => {
    expect(el('create-link')).toBeNull();
    expect(el('invite-link')).toBeNull();

    load(null);
    expect(el('create-link')).not.toBeNull();
  });

  it('creates a link on request and tells the parent, so a pending list can follow', () => {
    load(null);

    el('create-link').click();
    const request = linkRequest();
    expect(request.request.method).toBe('POST');
    request.flush(LINK);
    fixture.detectChanges();

    expect(el('invite-link').textContent).toContain(LINK.url);
    expect(fixture.componentInstance.changes).toBe(1);
  });

  it('replaces the link with the same request, and shows the new one', () => {
    load(LINK);

    el('rotate-link').click();
    const replacement = { ...LINK, url: LINK.url.replace('abc', 'def'), token: 'def' };
    const request = linkRequest();
    expect(request.request.method).toBe('POST');
    request.flush(replacement);
    fixture.detectChanges();

    expect(el('invite-link').textContent).toContain('def');
    expect(fixture.componentInstance.changes).toBe(1);
  });

  /**
   * Principle VII — a mutation on the browser hop is never retried by itself. A replayed POST
   * would retire the link the first attempt had just handed back, along with anything already
   * shared using it.
   */
  it('does not retry a failed create by itself, and says what happened', () => {
    load(null);

    el('create-link').click();
    linkRequest().flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
    jest.advanceTimersByTime(30_000);
    fixture.detectChanges();

    httpMock.expectNone('/api/v1/teams/kiel-krakens/invitations/link');
    expect(el('invite-link-error')).not.toBeNull();
    // And it can be pressed again, which is the retry.
    expect(el<HTMLButtonElement>('create-link').disabled).toBe(false);
  });

  it('says "copied" only once the clipboard write has actually resolved', async () => {
    load(LINK);

    const copy = el<HTMLButtonElement>('copy-link');
    const before = copy.textContent?.trim();
    copy.click();
    expect(clipboard.writeText).toHaveBeenCalledWith(LINK.url);

    await Promise.resolve();
    fixture.detectChanges();
    expect(copy.textContent?.trim()).not.toBe(before);

    jest.advanceTimersByTime(1500);
    fixture.detectChanges();
    expect(copy.textContent?.trim()).toBe(before);
  });

  /**
   * `navigator.clipboard` is absent in an insecure context — which is exactly where this gets
   * used on a phone on the local network. Claiming "Copied!" there hands somebody a paste of
   * whatever was in the clipboard before.
   */
  it('reports a refused copy instead of claiming it worked', async () => {
    clipboard.writeText.mockRejectedValue(new Error('denied'));
    load(LINK);

    const copy = el<HTMLButtonElement>('copy-link');
    const before = copy.textContent?.trim();
    copy.click();

    await Promise.resolve().then(() => undefined);
    fixture.detectChanges();

    expect(el('invite-link-error')).not.toBeNull();
    expect(copy.textContent?.trim()).toBe(before);
  });

  /** A read that fails must not leave the block empty: creating a link is still a real offer. */
  it('falls back to the create control when the read fails', () => {
    linkRequest().error(new ProgressEvent('network error'));
    fixture.detectChanges();

    expect(el('create-link')).not.toBeNull();
  });

  it('re-reads when it is pointed at another team', () => {
    load(LINK);

    fixture.componentInstance.slug.set('hamburg-hammers');
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/teams/hamburg-hammers/invitations/link').flush(null);
    fixture.detectChanges();
    expect(el('create-link')).not.toBeNull();
  });
});
