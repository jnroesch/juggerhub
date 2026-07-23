import { Route } from '@angular/router';
import { appRoutes } from './app.routes';
import { authGuard } from './core/guards/auth.guard';

/**
 * Feature 026 (US1) — teams, events, and browse are authenticated-only. These assertions pin the
 * client-side guard config (UX layer). The security boundary itself is the server (proven by the
 * backend integration tests); this just stops the routes from silently losing their guard.
 */
describe('app.routes — authenticated-only access (feature 026)', () => {
  const shellChildren: Route[] = appRoutes.find((r) => r.path === '' && r.children)?.children ?? [];

  const find = (path: string): Route | undefined => {
    const inShell = shellChildren.find((r) => r.path === path);
    return inShell ?? appRoutes.find((r) => r.path === path);
  };

  const guarded = (path: string) =>
    (find(path)?.canActivate ?? []).includes(authGuard as unknown as never);

  it.each(['t/:slug', 'events/:id', 'browse/teams', 'browse/events', 'browse/players'])(
    'guards "%s" with authGuard',
    (path) => {
      expect(find(path)).toBeDefined();
      expect(guarded(path)).toBe(true);
    },
  );

  it('serves the single /u/:handle profile in-shell and ungated (signed-out visitors can view public profiles)', () => {
    // A shell child (in-shell → keeps nav), with NO authGuard: the read-only view redirects
    // signed-out visitors to sign-in only when the profile is private/unknown.
    expect(find('u/:handle')).toBeDefined();
    expect(guarded('u/:handle')).toBe(false);
    // The standalone /p/:handle and /profile routes were folded into this single URL.
    expect(appRoutes.find((r) => r.path === 'p/:handle')).toBeUndefined();
    expect(find('profile')).toBeUndefined();
  });
});
