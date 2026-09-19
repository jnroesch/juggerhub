import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { PushDeviceService } from './push-device.service';

/**
 * The six states the settings page has to tell apart (feature 055, FR-002), and the rule that
 * permission is never requested without a press (FR-004).
 */
/** Lets every pending promise settle before the next assertion. */
const flushMicrotasks = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

describe('PushDeviceService', () => {
  let service: PushDeviceService;
  let http: HttpTestingController;

  const originalNotification = (globalThis as Record<string, unknown>)['Notification'];
  const originalMatchMedia = window.matchMedia;

  /** Replaces the browser bits the service feature-detects. */
  function stubBrowser(options: {
    push?: boolean;
    permission?: NotificationPermission;
    requestPermission?: jest.Mock;
    subscription?: unknown;
    subscribe?: jest.Mock;
    userAgent?: string;
    standalone?: boolean;
  }): { subscribe: jest.Mock; requestPermission: jest.Mock } {
    const subscribe = options.subscribe ?? jest.fn();
    const requestPermission = options.requestPermission ?? jest.fn().mockResolvedValue('granted');

    if (options.push === false) {
      delete (window as unknown as Record<string, unknown>)['PushManager'];
      delete (globalThis as Record<string, unknown>)['Notification'];
    } else {
      (window as unknown as Record<string, unknown>)['PushManager'] = function PushManager() {
        /* presence is all the service checks */
      };
      (globalThis as Record<string, unknown>)['Notification'] = {
        permission: options.permission ?? 'default',
        requestPermission,
      };
    }

    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        ready: Promise.resolve({
          pushManager: {
            getSubscription: jest.fn().mockResolvedValue(options.subscription ?? null),
            subscribe,
          },
        }),
      },
    });

    Object.defineProperty(navigator, 'userAgent', {
      configurable: true,
      value: options.userAgent ?? 'Mozilla/5.0 (Linux; Android 14) Chrome/120',
    });

    window.matchMedia = jest.fn().mockReturnValue({ matches: options.standalone ?? false });

    return { subscribe, requestPermission };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PushDeviceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    (globalThis as Record<string, unknown>)['Notification'] = originalNotification;
    window.matchMedia = originalMatchMedia;
  });

  it('reports unsupported where there is no Push API', async () => {
    stubBrowser({ push: false });

    await service.refresh();

    expect(service.state()).toBe('unsupported');
  });

  it('reports needs-install on an iPhone that is not running standalone', async () => {
    // On iOS the Push API is absent in a browser tab and present in the installed app, which is
    // why this is detected by display mode rather than by sniffing a version.
    stubBrowser({
      push: false,
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Safari',
      standalone: false,
    });

    await service.refresh();

    expect(service.state()).toBe('needs-install');
  });

  it('reports blocked when permission was denied, and never asks again', async () => {
    const { requestPermission } = stubBrowser({ permission: 'denied' });

    await service.refresh();

    expect(service.state()).toBe('blocked');
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it('reports on when this browser already has a subscription', async () => {
    stubBrowser({ permission: 'granted', subscription: { endpoint: 'https://push.example/1' } });

    await service.refresh();

    expect(service.state()).toBe('on');
  });

  it('reports off without asking for permission', async () => {
    const { requestPermission } = stubBrowser({ permission: 'default' });

    await service.refresh();

    expect(service.state()).toBe('off');
    // FR-004: permission is requested only from a member's press, never on load.
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it('subscribes and registers the device when enabled', async () => {
    const subscribe = jest.fn().mockResolvedValue({
      endpoint: 'https://push.example/abc',
      toJSON: () => ({ keys: { p256dh: 'device-key', auth: 'device-auth' } }),
      unsubscribe: jest.fn(),
    });
    stubBrowser({ permission: 'default', subscribe });

    const enabling = service.enable();

    // enable() awaits the permission prompt before it fetches anything, so the queued microtasks
    // have to run before the first request exists.
    await flushMicrotasks();
    http.expectOne('/api/v1/push/public-key').flush({ publicKey: 'BEJgGLEQANpFt4O8' });

    await flushMicrotasks();
    const post = http.expectOne('/api/v1/push/subscriptions');
    expect(post.request.method).toBe('POST');
    expect(post.request.body.endpoint).toBe('https://push.example/abc');
    expect(post.request.body.p256dh).toBe('device-key');
    post.flush(null);

    await expect(enabling).resolves.toBe(true);
    expect(service.state()).toBe('on');
    expect(subscribe).toHaveBeenCalledWith(
      expect.objectContaining({ userVisibleOnly: true }),
    );
  });

  it('goes to blocked when the member denies the prompt', async () => {
    stubBrowser({
      permission: 'default',
      requestPermission: jest.fn().mockResolvedValue('denied'),
    });

    await expect(service.enable()).resolves.toBe(false);

    expect(service.state()).toBe('blocked');
  });
});
