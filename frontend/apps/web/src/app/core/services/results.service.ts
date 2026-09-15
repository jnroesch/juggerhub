import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/event.models';
import {
  ImportConnection,
  RankingRow,
  ResultEditor,
  TeamPlacement,
  TournamentMatch,
  TournamentResult,
  TugenyImportPreview,
  TugenyLinked,
} from '../models/results.models';

/**
 * Tournament results API client (feature 050). Reads are open to any signed-in user; writes are
 * for the event's admins. Server-side authorization is the real boundary — this is UX only.
 *
 * Link and import are the only calls that make the server contact Tugeny. They are rate-limited
 * per user by our own limiter, whose 429 is never retried (the retry interceptor already skips it).
 */
@Injectable({ providedIn: 'root' })
export class ResultsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/events';

  private results(eventId: string): string {
    return `${this.base}/${encodeURIComponent(eventId)}/results`;
  }

  // --- Reads -------------------------------------------------------------------------------

  getResults(eventId: string): Observable<TournamentResult> {
    return this.http.get<TournamentResult>(this.results(eventId));
  }

  getMatches(eventId: string, skip = 0, take = 50): Observable<PagedResult<TournamentMatch>> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.http.get<PagedResult<TournamentMatch>>(`${this.results(eventId)}/matches`, { params });
  }

  getTeamPlacements(slug: string, skip = 0, take = 20): Observable<PagedResult<TeamPlacement>> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.http.get<PagedResult<TeamPlacement>>(`/api/v1/teams/${encodeURIComponent(slug)}/placements`, {
      params,
    });
  }

  // --- The results page (event admins) -----------------------------------------------------

  getEditor(eventId: string): Observable<ResultEditor> {
    return this.http.get<ResultEditor>(`${this.results(eventId)}/editor`);
  }

  saveRanking(eventId: string, placements: RankingRow[]): Observable<TournamentResult> {
    return this.http.put<TournamentResult>(`${this.results(eventId)}/ranking`, { placements });
  }

  clearRanking(eventId: string): Observable<void> {
    return this.http.delete<void>(`${this.results(eventId)}/ranking`);
  }

  // --- Tugeny ------------------------------------------------------------------------------

  linkTugeny(eventId: string, address: string): Observable<TugenyLinked> {
    return this.http.put<TugenyLinked>(`${this.results(eventId)}/tugeny-link`, { address });
  }

  unlinkTugeny(eventId: string): Observable<void> {
    return this.http.delete<void>(`${this.results(eventId)}/tugeny-link`);
  }

  previewTugenyImport(eventId: string): Observable<TugenyImportPreview> {
    return this.http.get<TugenyImportPreview>(`${this.results(eventId)}/tugeny-import`);
  }

  commitTugenyImport(eventId: string, connections: ImportConnection[]): Observable<TournamentResult> {
    return this.http.post<TournamentResult>(`${this.results(eventId)}/tugeny-import`, { connections });
  }
}
