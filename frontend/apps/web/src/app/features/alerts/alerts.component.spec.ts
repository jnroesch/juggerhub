import { HttpErrorResponse } from '@angular/common/http';
import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { AppNotification } from '../../core/models/notification.models';
import { MembershipService } from '../../core/services/membership.service';
import { NotificationService } from '../../core/services/notification.service';
import { TeamService } from '../../core/services/team.service';
import { AlertsComponent } from './alerts.component';

const invite: AppNotification = {
  id: 'n-invite',
  type: 'TeamInvite',
  createdDate: new Date().toISOString(),
  isRead: false,
  actorDisplayName: 'Mara',
  resolved: false,
  payload: { invitationId: 'i1', token: 'tok-1', teamSlug: 'rheinfeuer', teamName: 'Rheinfeuer', inviterName: 'Mara' },
};

/**
 * Feature 064 — accepting a team invite inline can now meet our own limit on joining by invitation.
 * A 429 is not a dead invitation: the row must stay actionable and the page must say why nothing
 * happened. Any other failure still reconciles the row as handled, as before.
 */
describe('AlertsComponent — accepting an invite (feature 064)', () => {
  let fixture: ComponentFixture<AlertsComponent>;
  let notifications: { markInviteResolved: jest.Mock };
  let teams: { acceptInvite: jest.Mock };

  beforeEach(() => {
    const items = signal<AppNotification[]>([invite]);
    notifications = { markInviteResolved: jest.fn() };
    teams = { acceptInvite: jest.fn() };
    TestBed.configureTestingModule({
      imports: [AlertsComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        {
          provide: NotificationService,
          useValue: {
            items,
            hasMore: computed(() => false),
            unreadCount: signal(1),
            loadFirstPage: jest.fn().mockReturnValue(of({ items: [invite], totalCount: 1, skip: 0, take: 20 })),
            markRead: jest.fn().mockReturnValue(of(undefined)),
            ...notifications,
          },
        },
        { provide: TeamService, useValue: teams },
        { provide: MembershipService, useValue: { load: jest.fn() } },
      ],
    });
    fixture = TestBed.createComponent(AlertsComponent);
    fixture.detectChanges();
  });

  const accept = () => {
    (fixture.nativeElement.querySelector('[data-testid="notif-accept"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  };
  const notice = () => (fixture.nativeElement.querySelector('[data-testid="invite-notice"]') as HTMLElement | null)?.textContent?.trim();

  it('keeps the invite actionable and says to try later when the limit is reached', () => {
    teams.acceptInvite.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429 })));

    accept();

    expect(teams.acceptInvite).toHaveBeenCalledTimes(1);
    expect(notifications.markInviteResolved).not.toHaveBeenCalled();
    expect(notice()).toBe("You've joined a lot of teams in a short time. Try again in a while.");
    expect(fixture.nativeElement.querySelector('[data-testid="notif-accept"]')).not.toBeNull();
  });

  it('still reconciles a dead invitation as handled, with no notice', () => {
    teams.acceptInvite.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 })));

    accept();

    expect(notifications.markInviteResolved).toHaveBeenCalledWith('n-invite');
    expect(notice()).toBeUndefined();
  });
});
