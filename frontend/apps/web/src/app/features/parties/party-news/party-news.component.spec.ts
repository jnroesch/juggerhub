import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Party, PartyNews } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { PartyNewsComponent } from './party-news.component';

function post(id: string, body: string, editedDate: string | null = null): PartyNews {
  return { id, authorDisplayName: 'Anna', authorRole: 'Admin', body, createdDate: '2026-09-27T19:00:00Z', editedDate };
}

const page = <T>(items: T[]) => ({ items, totalCount: items.length, skip: 0, take: 20 });

/**
 * Feature 059 — party admins edit and delete party news in place on the party's news page. The shared
 * controls have their own spec; these pin what this page wires to them, and that a failed post is
 * reported in the viewer's language rather than with the server's English `detail` (GH #179).
 */
describe('PartyNewsComponent', () => {
  let fixture: ComponentFixture<PartyNewsComponent>;
  let service: Record<string, jest.Mock>;

  function render(myRole: Party['myRole'], posts: PartyNews[]): void {
    service = {
      getParty: jest.fn().mockReturnValue(of({ id: 'party-1', myRole } as Partial<Party>)),
      listNews: jest.fn().mockReturnValue(of(page(posts))),
      postNews: jest.fn(),
      editNews: jest.fn(),
      deleteNews: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [PartyNewsComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: PartyService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'party-1' }) } } },
      ],
    });
    fixture = TestBed.createComponent(PartyNewsComponent);
    fixture.detectChanges();
  }

  function el<T extends HTMLElement = HTMLElement>(selector: string): T | null {
    return fixture.nativeElement.querySelector(selector);
  }

  function click(selector: string): void {
    const target = el(selector);
    if (!target) {
      throw new Error(`Nothing matches ${selector}`);
    }
    target.click();
    fixture.detectChanges();
  }

  const bodies = () =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="news-body"]')).map((p) => (p as HTMLElement).textContent?.trim());

  it('offers the controls to a party admin and to nobody else', () => {
    render('Member', [post('p1', 'Meet 07:00 at the Aral.')]);
    expect(el('[data-news-menu-trigger]')).toBeNull();

    TestBed.resetTestingModule();
    render('Admin', [post('p1', 'Meet 07:00 at the Aral.'), post('p2', 'Bring water.')]);
    expect(fixture.nativeElement.querySelectorAll('[data-news-menu-trigger]').length).toBe(2);
  });

  it('marks an edited post, and only that one', () => {
    render('Member', [post('p1', 'Meet 07:00.'), post('p2', 'Bring water.', '2026-09-28T08:00:00Z')]);

    const metas = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="news-meta"]')).map(
      (p) => (p as HTMLElement).textContent ?? '',
    );
    expect(metas[0]).not.toContain('edited');
    expect(metas[1]).toContain('· edited');
  });

  it('saves through the party and shows the answer in place', () => {
    render('Admin', [post('p1', 'Meet 07:00 at the Aral on the A7.')]);
    service['editNews'].mockReturnValue(of(post('p1', 'Meet 07:00 at the Aral on the A1.', '2026-09-28T09:00:00Z')));
    click('[data-news-menu-trigger="p1"]');
    click('[data-testid="news-edit"]');
    expect(el('[data-testid="news-editor"]')?.textContent).toContain("Saving won't notify the crew again.");
    const input = el<HTMLTextAreaElement>('[data-testid="news-edit-input"]');
    if (!input) {
      throw new Error('The editor did not open');
    }
    input.value = 'Meet 07:00 at the Aral on the A1.';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    click('[data-testid="news-edit-save"]');

    expect(service['editNews']).toHaveBeenCalledWith('party-1', 'p1', 'Meet 07:00 at the Aral on the A1.');
    expect(bodies()).toEqual(['Meet 07:00 at the Aral on the A1.']);
  });

  it('deletes through the party, says what goes with it, and lands focus on the heading', () => {
    render('Admin', [post('p1', 'Wrong party.'), post('p2', 'Bring water.')]);
    service['deleteNews'].mockReturnValue(of(undefined));
    click('[data-news-menu-trigger="p1"]');
    click('[data-testid="news-delete"]');
    const dialog = el('[data-testid="news-delete-confirm"]')?.textContent ?? '';
    expect(dialog).toContain('together with its alerts');
    expect(dialog).toContain('Anyone who got it by email keeps their copy.');

    click('[data-testid="news-delete-submit"]');

    expect(service['deleteNews']).toHaveBeenCalledWith('party-1', 'p1');
    expect(bodies()).toEqual(['Bring water.']);
    expect(document.activeElement).toBe(el('#party-news-heading'));
  });

  it('says so when a post was already gone', () => {
    render('Admin', [post('p1', 'Gone meanwhile.'), post('p2', 'Stays.')]);
    service['deleteNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    click('[data-news-menu-trigger="p1"]');
    click('[data-testid="news-delete"]');

    click('[data-testid="news-delete-submit"]');

    expect(bodies()).toEqual(['Stays.']);
    expect(el('[data-testid="news-notice"]')?.textContent?.trim()).toBe('This post no longer exists.');
  });

  it('reports a failed post in its own words, never the server\'s', () => {
    render('Admin', []);
    service['postNews'].mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { detail: 'Write an update of up to 1000 characters.' } })),
    );
    // Drive the draft signal directly, as chat-compose's spec does: the template-driven ngModel's
    // DOM 'input' path is unreliable in the zoneless test environment, and the value accessor is not
    // what is under test here.
    (fixture.componentInstance as unknown as { body: { set: (v: string) => void } }).body.set('Something.');
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(service['postNews']).toHaveBeenCalledWith('party-1', { body: 'Something.' });
    expect(fixture.nativeElement.textContent).toContain("We couldn't post that.");
    expect(fixture.nativeElement.textContent).not.toContain('Write an update of up to 1000 characters.');
  });
});
