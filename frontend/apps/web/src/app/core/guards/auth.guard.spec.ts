import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, isObservable, of } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { authGuard } from './auth.guard';

/**
 * authGuard carries the attempted URL to sign-in as returnUrl so a shared team/event link opened
 * while signed out resumes after login. UX only — the API still enforces 401 server-side.
 */
describe('authGuard', () => {
  function runGuard(user: unknown, url: string) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { ensureSession: () => of(user) } }],
    });
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
    return { result, router: TestBed.inject(Router) };
  }

  async function resolve(result: boolean | UrlTree | Observable<boolean | UrlTree>) {
    return isObservable(result) ? firstValueFrom(result) : result;
  }

  it('allows an authenticated user through', async () => {
    const { result } = runGuard({ id: 'u1' }, '/t/berlin-jugger');
    expect(await resolve(result)).toBe(true);
  });

  it('redirects a signed-out user to sign-in carrying the attempted URL as returnUrl', async () => {
    const { result, router } = runGuard(null, '/t/berlin-jugger');
    const tree = await resolve(result);

    expect(tree).toBeInstanceOf(UrlTree);
    const serialized = router.serializeUrl(tree as UrlTree);
    expect(serialized).toContain('/sign-in');
    expect((tree as UrlTree).queryParams['returnUrl']).toBe('/t/berlin-jugger');
  });

  it('preserves query params in the returnUrl', async () => {
    const { result } = runGuard(null, '/events/123?tab=news');
    const tree = await resolve(result);
    expect((tree as UrlTree).queryParams['returnUrl']).toBe('/events/123?tab=news');
  });
});
