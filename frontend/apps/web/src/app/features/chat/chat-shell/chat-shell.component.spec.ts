import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { ChatShellComponent } from './chat-shell.component';
import { AuthService } from '../../../core/services/auth.service';

/**
 * The chat shell (feature 019, US8). What matters here is that the two layouts are **layout only**:
 * the rail and the mobile inbox are the same component, so live delivery, typing and receipts cannot
 * diverge between them (SC-009). The breakpoint itself is a CSS concern (`lg:` utilities) and is
 * verified in a real browser rather than asserted here.
 */
describe('ChatShellComponent', () => {
  let fixture: ComponentFixture<ChatShellComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'chat', component: ChatShellComponent }]),
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    await router.navigate(['/chat']);

    fixture = TestBed.createComponent(ChatShellComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    // The inbox mounts inside the shell and seeds itself; let those settle.
    httpMock.match(() => true).forEach((r) => r.flush({ items: [], totalCount: 0, skip: 0, take: 20 }));
    httpMock.verify({ ignoreCancelled: true });
  });

  it('renders the rail, which is the same inbox the mobile layout shows', () => {
    const rail = fixture.nativeElement.querySelector('[data-testid="chat-rail"]');
    expect(rail).toBeTruthy();
    // One inbox instance backs both shapes — that is what stops the two from diverging.
    expect(rail.querySelector('[data-testid="chat-inbox"]')).toBeTruthy();
  });

  it('shows the desktop empty pane when no conversation is open', () => {
    const shell = fixture.nativeElement.querySelector('[data-testid="chat-shell"]');
    expect(shell.textContent).toContain('Pick a conversation');
  });

  it('keeps the rail mounted at /chat', () => {
    // The rail is hidden on mobile by a CSS class, never unmounted — so switching viewport cannot
    // drop the inbox's live subscriptions.
    expect(fixture.nativeElement.querySelector('[data-testid="chat-rail"]')).toBeTruthy();
  });
});
