import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';
import {
  ActivityItem,
  HandleAvailability,
  OwnerProfile,
  PagedResult,
  PublicProfile,
  UpdateProfileRequest,
} from '../models/profile.models';
import { LocationSelection } from '../models/city.models';
import { AuthService } from './auth.service';

/**
 * Profile API client. Owner calls carry the session cookie (via the auth
 * interceptor). Public calls hit anonymous endpoints. Avatar bytes are never
 * modeled here — the browser loads them straight from the avatar URL.
 */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = '/api/v1/profiles';
  private readonly authBase = '/api/v1/auth';
  /** Bumped on every successful own-avatar upload; see `ownAvatarUrl`. */
  private readonly ownAvatarRevision = signal(0);

  // --- Owner ---------------------------------------------------------------

  getMine(): Observable<OwnerProfile> {
    return this.http.get<OwnerProfile>(`${this.base}/me`);
  }

  private mine$?: Observable<OwnerProfile>;

  /**
   * Cached owner profile for viewer-context reads (feature 021): handle for
   * self-detection + administered teams. Shared across profile views so opening
   * several profiles doesn't refetch.
   *
   * Dropped by every owner mutation below, so a change the viewer just made is visible on the next
   * screen without a reload. It used to live for the whole session: setting a home city and
   * navigating to Browse still read the cached city-less profile, so the proximity Sort option
   * stayed hidden until F5 (the option only exists once `location` is set).
   */
  getMineCached(): Observable<OwnerProfile> {
    this.mine$ ??= this.getMine().pipe(shareReplay(1));
    return this.mine$;
  }

  /**
   * Drop the cached owner profile; the next `getMineCached` refetches.
   *
   * Every mutation in this service calls it. It is public because the cached payload also carries
   * `teams`, which changes through `TeamService` (join/leave/role) — a caller that moves the
   * owner's membership should invalidate here too.
   */
  invalidateMine(): void {
    this.mine$ = undefined;
  }

  updateMine(request: UpdateProfileRequest): Observable<OwnerProfile> {
    return this.http
      .put<OwnerProfile>(`${this.base}/me`, request)
      .pipe(tap(() => this.invalidateMine()));
  }

  /**
   * Set (or clear) ONLY the home city (feature 030), without touching the rest of the profile.
   * Onboarding uses this to persist the city the moment it's picked so the team step can order by
   * proximity (FR-013). 204 on success; 422 unresolvable city; 503 geocoder unavailable.
   */
  setHomeCity(selection: LocationSelection): Observable<void> {
    return this.http
      .put<void>(`${this.base}/me/home-city`, selection)
      .pipe(tap(() => this.invalidateMine()));
  }

  uploadAvatar(file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<void>(`${this.base}/me/avatar`, form).pipe(
      tap(() => {
        this.invalidateMine();
        this.ownAvatarRevision.update((v) => v + 1);
        this.auth.markAvatarUploaded();
      }),
    );
  }

  /**
   * Mark first-login onboarding complete (feature 004). Idempotent, owner-only.
   * Called on any terminal exit of the flow — finishing or dismissing.
   */
  completeOnboarding(): Observable<void> {
    return this.http.post<void>(`${this.base}/me/onboarding/complete`, {});
  }

  // --- Public --------------------------------------------------------------

  getPublic(handle: string): Observable<PublicProfile> {
    return this.http.get<PublicProfile>(`${this.base}/${encodeURIComponent(handle)}`);
  }

  getActivity(handle: string, skip = 0, take = 20): Observable<PagedResult<ActivityItem>> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.http.get<PagedResult<ActivityItem>>(
      `${this.base}/${encodeURIComponent(handle)}/activity`,
      { params },
    );
  }

  /** Canonical URL the browser uses to render an avatar (adds a cache-buster hook if needed). */
  avatarUrl(handle: string): string {
    return `${this.base}/${encodeURIComponent(handle)}/avatar`;
  }

  /**
   * The signed-in player's own avatar URL, cache-busted by this session's upload count (GH #283).
   * The URL alone never changes on upload, so an `<img>` already showing it keeps the old picture
   * from the browser's image cache; the revision makes every surface showing the player's own
   * avatar (top nav, owner profile) re-fetch together. Reads a signal, so a `computed` calling it
   * recomputes after an upload.
   */
  ownAvatarUrl(handle: string): string {
    return `${this.avatarUrl(handle)}?v=${this.ownAvatarRevision()}`;
  }

  // --- Handle (registration UX aid) ---------------------------------------

  checkHandle(handle: string): Observable<HandleAvailability> {
    const params = new HttpParams().set('handle', handle);
    return this.http.get<HandleAvailability>(`${this.authBase}/handle-available`, { params });
  }
}
