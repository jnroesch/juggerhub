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

    // Only the one togglable category has switches; the always-on group is a chip, not a control.
    expect(el.querySelectorAll('[role="switch"]').length).toBe(3);
  });
});
