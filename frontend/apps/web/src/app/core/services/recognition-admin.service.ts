import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import {
  AdminSubjectAwards,
  AdminSubjectType,
  RecognitionDefinition,
  RecognitionKind,
  RecognitionUpsert,
} from '../models/recognition.models';
import { PagedResult } from '../models/profile.models';

export interface GrantBadgeBody {
  playerHandle?: string;
  teamSlug?: string;
  note?: string | null;
}

export interface GrantAchievementBody extends GrantBadgeBody {
  contextYear?: number | null;
  contextLabel?: string | null;
}

/**
 * Platform-admin operations for badges & achievements (feature 012). The server `PlatformAdmin`
 * policy is the real boundary; the `isAdmin` signal only gates UX (nav entry + route guard).
 */
@Injectable({ providedIn: 'root' })
export class RecognitionAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin';

  // undefined = not yet probed, true/false = known.
  private readonly admin = signal<boolean | undefined>(undefined);
  readonly isAdmin = computed(() => this.admin() === true);

  /** Probe the server access endpoint; caches the result. 200 → admin, 401/403 → not. */
  checkAccess(): Observable<boolean> {
    if (this.admin() !== undefined) {
      return of(this.admin() as boolean);
    }
    return this.http.get<{ isAdmin: boolean }>(`${this.base}/access`).pipe(
      map((r) => r.isAdmin === true),
      catchError(() => of(false)),
      tap((ok) => this.admin.set(ok)),
    );
  }

  listBadges(includeRetired = false): Observable<RecognitionDefinition[]> {
    return this.http
      .get<PagedResult<RecognitionDefinition>>(`${this.base}/badges`, { params: { take: 100, includeRetired } })
      .pipe(map((p) => p.items));
  }

  listAchievements(includeRetired = false): Observable<RecognitionDefinition[]> {
    return this.http
      .get<PagedResult<RecognitionDefinition>>(`${this.base}/achievements`, { params: { take: 100, includeRetired } })
      .pipe(map((p) => p.items));
  }

  // --- Catalogue management (feature 014). Kind picks the catalogue/endpoint. -----------
  // The server `PlatformAdmin` policy validates everything; these are UX conveniences.

  /** The active catalogue for a kind, optionally including retired types. */
  listDefinitions(kind: RecognitionKind, includeRetired = false): Observable<RecognitionDefinition[]> {
    return kind === 'badge' ? this.listBadges(includeRetired) : this.listAchievements(includeRetired);
  }

  createDefinition(kind: RecognitionKind, body: RecognitionUpsert): Observable<RecognitionDefinition> {
    return this.http.post<RecognitionDefinition>(`${this.base}/${this.catalogue(kind)}`, body);
  }

  updateDefinition(kind: RecognitionKind, id: string, body: RecognitionUpsert): Observable<RecognitionDefinition> {
    return this.http.put<RecognitionDefinition>(`${this.base}/${this.catalogue(kind)}/${id}`, body);
  }

  retireDefinition(kind: RecognitionKind, id: string): Observable<unknown> {
    return this.http.delete(`${this.base}/${this.catalogue(kind)}/${id}`);
  }

  reinstateDefinition(kind: RecognitionKind, id: string): Observable<unknown> {
    return this.http.post(`${this.base}/${this.catalogue(kind)}/${id}/reinstate`, {});
  }

  /** Upload/replace the icon. The raw file is the request body; the server sniffs its type. */
  setIcon(kind: RecognitionKind, id: string, file: File): Observable<unknown> {
    return this.http.put(`${this.base}/${this.catalogue(kind)}/${id}/icon`, file);
  }

  removeIcon(kind: RecognitionKind, id: string): Observable<unknown> {
    return this.http.delete(`${this.base}/${this.catalogue(kind)}/${id}/icon`);
  }

  private catalogue(kind: RecognitionKind): string {
    return kind === 'badge' ? 'badges' : 'achievements';
  }

  subjectAwards(type: AdminSubjectType, ref: string): Observable<AdminSubjectAwards> {
    const path = type === 'player' ? 'players' : 'teams';
    return this.http.get<AdminSubjectAwards>(`${this.base}/${path}/${encodeURIComponent(ref)}/awards`);
  }

  grantBadge(definitionId: string, body: GrantBadgeBody): Observable<unknown> {
    return this.http.post(`${this.base}/badges/${definitionId}/awards`, body);
  }

  grantAchievement(definitionId: string, body: GrantAchievementBody): Observable<unknown> {
    return this.http.post(`${this.base}/achievements/${definitionId}/awards`, body);
  }

  revokeBadge(awardId: string, reason?: string): Observable<unknown> {
    return this.http.request('delete', `${this.base}/badges/awards/${awardId}`, { body: { reason: reason ?? null } });
  }

  revokeAchievement(awardId: string, reason?: string): Observable<unknown> {
    return this.http.request('delete', `${this.base}/achievements/awards/${awardId}`, { body: { reason: reason ?? null } });
  }
}
