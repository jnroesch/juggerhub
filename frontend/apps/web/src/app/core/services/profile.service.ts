import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ActivityItem,
  HandleAvailability,
  OwnerProfile,
  PagedResult,
  PublicProfile,
  UpdateProfileRequest,
} from '../models/profile.models';

/**
 * Profile API client. Owner calls carry the session cookie (via the auth
 * interceptor). Public calls hit anonymous endpoints. Avatar bytes are never
 * modeled here — the browser loads them straight from the avatar URL.
 */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/profiles';
  private readonly authBase = '/api/v1/auth';

  // --- Owner ---------------------------------------------------------------

  getMine(): Observable<OwnerProfile> {
    return this.http.get<OwnerProfile>(`${this.base}/me`);
  }

  updateMine(request: UpdateProfileRequest): Observable<OwnerProfile> {
    return this.http.put<OwnerProfile>(`${this.base}/me`, request);
  }

  uploadAvatar(file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<void>(`${this.base}/me/avatar`, form);
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

  // --- Handle (registration UX aid) ---------------------------------------

  checkHandle(handle: string): Observable<HandleAvailability> {
    const params = new HttpParams().set('handle', handle);
    return this.http.get<HandleAvailability>(`${this.authBase}/handle-available`, { params });
  }
}
