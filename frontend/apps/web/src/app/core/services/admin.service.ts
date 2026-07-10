import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AccountStatus,
  AdminOverview,
  AdminTeamDetail,
  AdminTeamListItem,
  AdminUserDetail,
  AdminUserListItem,
} from '../models/admin.models';
import { PagedResult } from '../models/profile.models';

/**
 * The platform admin area API (feature 013): overview, user search, the per-player
 * detail, and the recorded account actions. Every call is server-enforced by the
 * `PlatformAdmin` policy — this service carries no gating of its own. (The cached
 * access probe that gates the nav entry and route guard lives in
 * RecognitionAdminService.checkAccess, shared since feature 012.)
 */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin';

  getOverview(): Observable<AdminOverview> {
    return this.http.get<AdminOverview>(`${this.base}/overview`);
  }

  searchUsers(
    q: string,
    status: AccountStatus | null,
    skip: number,
    take: number,
  ): Observable<PagedResult<AdminUserListItem>> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (q.trim()) {
      params = params.set('q', q.trim());
    }
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PagedResult<AdminUserListItem>>(`${this.base}/users`, { params });
  }

  getUserDetail(handle: string): Observable<AdminUserDetail> {
    return this.http.get<AdminUserDetail>(`${this.base}/users/${encodeURIComponent(handle)}`);
  }

  // --- Teams (feature 014): browse for award assignment -----------------------

  searchTeams(q: string, skip: number, take: number): Observable<PagedResult<AdminTeamListItem>> {
    let params = new HttpParams().set('skip', skip).set('take', take);
    if (q.trim()) {
      params = params.set('q', q.trim());
    }
    return this.http.get<PagedResult<AdminTeamListItem>>(`${this.base}/teams`, { params });
  }

  getTeamDetail(slug: string): Observable<AdminTeamDetail> {
    return this.http.get<AdminTeamDetail>(`${this.base}/teams/${encodeURIComponent(slug)}`);
  }

  suspend(handle: string): Observable<void> {
    return this.action(handle, 'suspend');
  }

  reinstate(handle: string): Observable<void> {
    return this.action(handle, 'reinstate');
  }

  ban(handle: string): Observable<void> {
    return this.action(handle, 'ban');
  }

  unban(handle: string): Observable<void> {
    return this.action(handle, 'unban');
  }

  sendPasswordReset(handle: string): Observable<void> {
    return this.action(handle, 'reset-password');
  }

  private action(handle: string, action: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${encodeURIComponent(handle)}/${action}`, null);
  }
}
