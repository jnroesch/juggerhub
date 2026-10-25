import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { BottomNavComponent } from './bottom-nav.component';
import { MembershipService } from '../../core/services/membership.service';
import { NotificationService } from '../../core/services/notification.service';
import { ChatService } from '../../core/services/chat.service';
import { translocoTestingModule } from '../../../testing/transloco-testing';

/**
 * How the five tab labels behave below `narrow` (240px — see `tailwind.config.js`), GH #473.
 *
 * Five labels do not fit the 187px a 375px phone lays out at 200% browser zoom: German needs
 * 211px of them and Spanish 226px, so the bar pushed every route sideways. The owner's answer
 * (2026-10-25) is that the current destination keeps its words and the other four are read out
 * but not drawn.
 *
 * The widths themselves are measured in `web-e2e/src/responsive.spec.ts`, which is the only
 * place that can — a real engine laying out real text. What this spec pins is the half a unit
 * test can see and the half most likely to be undone by accident: **which** tab keeps its label,
 * and that the other four are withdrawn with `sr-only` rather than `hidden`. The label *is* each
 * link's accessible name — there is no `aria-label` on them — so a well-meant swap to
 * `narrow:hidden` would leave a screen reader five anonymous icons, and nothing on screen would
 * look any different.
 */
describe('BottomNavComponent — the current tab keeps its label at narrow widths', () => {
  const teams = signal<{ slug: string }[]>([]);

  /**
   * The component reads the URL once at construction — `toSignal(…, { initialValue:
   * this.router.url })` — so the stub has to be in place *before* `createComponent`, not after.
   */
  function create(url: string): ComponentFixture<BottomNavComponent> {
    Object.defineProperty(TestBed.inject(Router), 'url', { get: () => url, configurable: true });
    const fixture = TestBed.createComponent(BottomNavComponent);
    fixture.detectChanges();
    return fixture;
  }

  /** Every tab, as `{ id, label text, classes on the label span }`. */
  function tabs(fixture: ComponentFixture<BottomNavComponent>) {
    const links: HTMLElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a[data-testid^="tab-"]'),
    );
    return links.map((link) => {
      // The label is the last span in the link; the chat/alerts badges are nested deeper.
      const spans: HTMLElement[] = Array.from(link.querySelectorAll('span'));
      const label = spans[spans.length - 1];
      return {
        id: link.getAttribute('data-testid'),
        text: label.textContent?.trim(),
        classes: label.getAttribute('class') ?? '',
      };
    });
  }

  beforeEach(() => {
    teams.set([]);

    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideRouter([]),
        {
          provide: MembershipService,
          useValue: { teams, myTeamTarget: signal('/my-team') },
        },
        { provide: NotificationService, useValue: { unreadCount: signal(0) } },
        { provide: ChatService, useValue: { unreadCount: signal(0) } },
      ],
    });
  });

  it('renders all five destinations', () => {
    const rendered = tabs(create('/'));

    expect(rendered.map((t) => t.id)).toEqual([
      'tab-home',
      'tab-browse',
      'tab-my-team',
      'tab-chat',
      'tab-alerts',
    ]);
  });

  it('leaves the current tab’s label alone and withdraws the other four', () => {
    const rendered = tabs(create('/alerts'));

    const current = rendered.find((t) => t.id === 'tab-alerts')!;
    expect(current.classes).not.toContain('sr-only');

    for (const other of rendered.filter((t) => t.id !== 'tab-alerts')) {
      expect(other.classes).toContain('narrow:sr-only');
    }
  });

  it('follows the route, so a different destination keeps its label', () => {
    const rendered = tabs(create('/browse/teams'));

    expect(rendered.find((t) => t.id === 'tab-browse')!.classes).not.toContain('sr-only');
    expect(rendered.find((t) => t.id === 'tab-home')!.classes).toContain('narrow:sr-only');
  });

  /*
   * `isActiveDestination` lights nothing on a route that is none of the five — `/account`, an
   * event, someone else's team (GH #279) — so there is no current label to keep and the bar is
   * icon-only there. That is the rule read literally, recorded so it is not mistaken for a bug.
   */
  it('withdraws every label where no destination is current', () => {
    const rendered = tabs(create('/account'));

    for (const tab of rendered) {
      expect(tab.classes).toContain('narrow:sr-only');
    }
  });

  /*
   * The load-bearing one. `sr-only` keeps the text in the accessibility tree; `hidden` would not,
   * and these links have no other accessible name.
   */
  it('withdraws labels with sr-only, never hidden — the label is the link’s accessible name', () => {
    const rendered = tabs(create('/'));

    for (const tab of rendered) {
      expect(tab.text).toBeTruthy();
      expect(tab.classes).not.toContain('hidden');
    }
  });
});
