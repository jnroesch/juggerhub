import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { NotificationSettingsComponent } from './notification-settings.component';
import { PushDeviceService } from '../../../core/services/push-device.service';
import { NotificationPreferenceMatrix } from '../../../core/models/notification-preferences.models';

/**
 * The preference matrix gained a third channel (feature 055). What matters here is that the Push
 * toggle exists per category, that pressing it saves against the Push channel, and that the
 * always-on group did not quietly gain one.
 */
describe('NotificationSettingsComponent', () => {
  let fixture: ComponentFixture<NotificationSettingsComponent>;
  let http: HttpTestingController;

  const matrix: NotificationPreferenceMatrix = {
    categories: [
      {
        category: 'TeamNews',
        label: 'Team news',
        description: 'News posted to your teams',
        channels: { inApp: true, email: true, push: true },
        availableChannels: ['InApp', 'Email', 'Push'],
      },
      {
        // Feature 056. Note the channel VALUES are all true: the server leaves an unavailable
        // cell at its default on purpose, so a component that branched on them instead of on
        // availableChannels would render three working toggles here and this fixture would catch
        // it.
        category: 'Chat',
        label: 'Chat messages',
        description: 'New messages in your conversations.',
        channels: { inApp: true, email: true, push: true },
        availableChannels: ['Push'],
      },
    ],
    alwaysOn: [{ label: 'Security & sign-in', description: 'Verification and password' }],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationSettingsComponent, translocoTestingModule()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // The device section talks to the browser, which jsdom has none of. Its own spec covers it.
        { provide: PushDeviceService, useValue: { state: () => 'unsupported', refresh: async () => undefined } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationSettingsComponent);
    http = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    http.expectOne('/api/v1/notification-preferences').flush(matrix);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('renders a toggle for each of the three channels', () => {
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="toggle-TeamNews-inApp"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="toggle-TeamNews-email"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="toggle-TeamNews-push"]')).not.toBeNull();
  });

  it('saves against the Push channel when the push toggle is pressed', () => {
    const el = fixture.nativeElement as HTMLElement;
    const toggle = el.querySelector<HTMLButtonElement>('[data-testid="toggle-TeamNews-push"]');

    toggle?.click();

    const put = http.expectOne('/api/v1/notification-preferences/TeamNews/Push');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ enabled: false });
    put.flush(null);
  });

  it('gives the always-on group no push toggle', () => {
    const el = fixture.nativeElement as HTMLElement;

    // Three switches for the fully-deliverable category, one for chat's push cell, and none for
    // the always-on group — which is a chip, not a control.
    expect(el.querySelectorAll('[role="switch"]').length).toBe(4);
  });

  describe('a category that is not deliverable on every channel (feature 056)', () => {
    it('renders its unavailable cells as markers rather than toggles', () => {
      const el = fixture.nativeElement as HTMLElement;

      expect(el.querySelector('[data-testid="toggle-Chat-push"]')).not.toBeNull();
      expect(el.querySelector('[data-testid="toggle-Chat-inApp"]')).toBeNull();
      expect(el.querySelector('[data-testid="toggle-Chat-email"]')).toBeNull();

      expect(el.querySelector('[data-testid="unavailable-Chat-inApp"]')).not.toBeNull();
      expect(el.querySelector('[data-testid="unavailable-Chat-email"]')).not.toBeNull();
    });

    it('does not let an unavailable cell be pressed or focused', () => {
      const el = fixture.nativeElement as HTMLElement;
      const marker = el.querySelector('[data-testid="unavailable-Chat-inApp"]')!;

      // A greyed-out switch would say "you could turn this on", which is untrue; and a screen
      // reader must not announce a toggle that cannot be toggled.
      expect(marker.getAttribute('role')).toBeNull();
      expect(marker.tagName).not.toBe('BUTTON');
      expect(marker.hasAttribute('tabindex')).toBe(false);
    });

    it('explains itself to a screen reader rather than reading as nothing', () => {
      const el = fixture.nativeElement as HTMLElement;
      const marker = el.querySelector('[data-testid="unavailable-Chat-inApp"]')!;

      // A lone em dash is announced as nothing at all, so the dash is aria-hidden and the reason
      // is in sr-only text beside it.
      expect(marker.querySelector('[aria-hidden="true"]')?.textContent).toContain('—');
      expect(marker.querySelector('.sr-only')?.textContent?.trim()).not.toBe('');
    });

    it('still saves the channel it does have', () => {
      const el = fixture.nativeElement as HTMLElement;
      el.querySelector<HTMLButtonElement>('[data-testid="toggle-Chat-push"]')?.click();

      const put = http.expectOne('/api/v1/notification-preferences/Chat/Push');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ enabled: false });
      put.flush(null);
    });
  });
});
