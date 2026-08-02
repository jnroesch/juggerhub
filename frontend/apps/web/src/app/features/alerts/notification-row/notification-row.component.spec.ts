import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificationRowComponent } from './notification-row.component';
import { AppNotification } from '../../../core/models/notification.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/**
 * The row must render every notification type it can receive (feature 039).
 *
 * The icon degrades safely — the template has a `@default` arm — but `title` and `supporting` do
 * not: an unhandled type falls through to the generic fallback title and an *empty* supporting
 * line, which reads as a broken row rather than as a missing icon. So the assertions here are
 * about the text and the link, not the glyph.
 */
describe('NotificationRowComponent — EventCancelled', () => {
  let fixture: ComponentFixture<NotificationRowComponent>;

  const cancellation: AppNotification = {
    id: '0198c4f2-0000-7000-8000-000000000001',
    type: 'EventCancelled',
    createdDate: new Date().toISOString(),
    isRead: false,
    actorDisplayName: 'Mira Kessler',
    resolved: false,
    payload: {
      eventId: '0198c4f2-0000-7000-8000-0000000000aa',
      eventName: 'Hamburg Autumn Open',
    },
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationRowComponent, translocoTestingModule()],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationRowComponent);
    fixture.componentRef.setInput('notification', cancellation);
    fixture.detectChanges();
  });

  it('renders a title naming the cancelled event', () => {
    const title = fixture.nativeElement.textContent as string;
    expect(title).toContain('Hamburg Autumn Open');
  });

  it('renders a non-empty supporting line', () => {
    const paragraphs = fixture.nativeElement.querySelectorAll('p');
    const supporting = paragraphs[1]?.textContent?.trim() ?? '';
    expect(supporting.length).toBeGreaterThan(0);
    // The generic fallback would leave this empty — that is the regression being guarded.
    expect(supporting).not.toBe('');
  });

  it('links to the event page, which stays viewable after cancellation', () => {
    const anchor = fixture.nativeElement.querySelector('a[href]') as HTMLAnchorElement | null;
    expect(anchor?.getAttribute('href')).toBe('/events/0198c4f2-0000-7000-8000-0000000000aa');
  });

  it('offers no inline actions', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="notif-accept"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="notif-decline"]')).toBeNull();
  });
});
