import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { EventNews } from '../../../../core/models/event.models';
import { EventService } from '../../../../core/services/event.service';
import { NewsPostEditing } from '../../../../shared/news-post/news-post-editing';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../../testing/transloco-testing';
import { EventNewsFeedComponent } from './news-feed.component';

function post(id: string, body: string, editedDate: string | null = null): EventNews {
  return { id, authorDisplayName: 'Anna', body, createdDate: '2026-09-27T19:00:00Z', editedDate };
}

/** The event page as far as its news goes: it owns the list and provides the editing state. */
@Component({
  imports: [EventNewsFeedComponent],
  providers: [NewsPostEditing],
  template: `<jh-event-news-feed [(news)]="news" eventId="e1" [canManage]="canManage()" />`,
})
class PageComponent {
  readonly news = signal<EventNews[]>([post('p1', 'Check-in opens at 08:00.'), post('p2', 'Bring water.', '2026-09-28T08:00:00Z')]);
  readonly canManage = signal(true);
}

/**
 * Feature 059 — event admins edit and delete event news in place. The shared controls have their own
 * spec; these pin what the event page wires to them: who sees them, the marker, and that the
 * page's own list follows every change.
 */
describe('EventNewsFeedComponent', () => {
  let fixture: ComponentFixture<PageComponent>;
  let service: { editNews: jest.Mock; deleteNews: jest.Mock };

  beforeEach(() => {
    service = { editNews: jest.fn(), deleteNews: jest.fn() };
    TestBed.configureTestingModule({
      imports: [PageComponent, translocoTestingModule()],
      providers: [...translocoLocaleTestingProviders(), { provide: EventService, useValue: service }],
    });
    fixture = TestBed.createComponent(PageComponent);
    fixture.detectChanges();
  });

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

  const metas = () =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="news-meta"]')).map((p) => (p as HTMLElement).textContent ?? '');

  it('offers the controls to the event admins only', () => {
    expect(fixture.nativeElement.querySelectorAll('[data-news-menu-trigger]').length).toBe(2);

    fixture.componentInstance.canManage.set(false);
    fixture.detectChanges();

    expect(el('[data-news-menu-trigger]')).toBeNull();
  });

  it('marks an edited post, and only that one', () => {
    const [first, second] = metas();
    expect(first).not.toContain('edited');
    expect(second).toContain('· edited');
  });

  it('saves through the event and puts the answer into the page list', () => {
    service.editNews.mockReturnValue(of(post('p1', 'Check-in opens at 09:00.', '2026-09-28T09:00:00Z')));
    click('[data-news-menu-trigger="p1"]');
    click('[data-testid="news-edit"]');
    const input = el<HTMLTextAreaElement>('[data-testid="news-edit-input"]');
    if (!input) {
      throw new Error('The editor did not open');
    }
    expect(input.getAttribute('maxlength')).toBe('2000');
    expect(el('[data-testid="news-editor"]')?.textContent).toContain("Saving won't notify anyone.");
    input.value = 'Check-in opens at 09:00.';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    click('[data-testid="news-edit-save"]');

    expect(service.editNews).toHaveBeenCalledWith('e1', 'p1', 'Check-in opens at 09:00.');
    expect(fixture.componentInstance.news()[0].body).toBe('Check-in opens at 09:00.');
    expect(metas()[0]).toContain('· edited');
  });

  it('deletes through the event, takes the post off the page list and focuses the heading', () => {
    service.deleteNews.mockReturnValue(of(undefined));
    click('[data-news-menu-trigger="p1"]');
    click('[data-testid="news-delete"]');
    expect(el('[data-testid="news-delete-confirm"]')?.textContent).toContain("It disappears from the event page and from everyone's Home.");

    click('[data-testid="news-delete-submit"]');

    expect(service.deleteNews).toHaveBeenCalledWith('e1', 'p1');
    expect(fixture.componentInstance.news().map((n) => n.id)).toEqual(['p2']);
    expect(el('[data-testid="news-notice"]')).toBeNull();
    expect(document.activeElement).toBe(el('#event-news-heading'));
  });

  it('says so when the post was already gone', () => {
    service.deleteNews.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    click('[data-news-menu-trigger="p2"]');
    click('[data-testid="news-delete"]');

    click('[data-testid="news-delete-submit"]');

    expect(fixture.componentInstance.news().map((n) => n.id)).toEqual(['p1']);
    expect(el('[data-testid="news-notice"]')?.textContent?.trim()).toBe('This post no longer exists.');
  });
});
