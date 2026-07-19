import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/profile.models';
import {
  AgendaSession,
  AttendanceEntry,
  CreateTrainingRequest,
  CreatedTraining,
  EditSeriesRequest,
  EditSessionRequest,
  SeriesEditResult,
  TrainingRsvp,
  TrainingSeriesSummary,
  TrainingSessionDetail,
  TrainingSessionRow,
  TrainingVisibility,
} from '../models/trainings.models';

/**
 * Trainings API client (feature 018). Team-scoped list/create/public + session-scoped read/RSVP and
 * admin management, plus the cross-team dashboard agenda. Server-side authorization is the real
 * boundary — this is UX only. Every list is paginated.
 */
@Injectable({ providedIn: 'root' })
export class TrainingsService {
  private readonly http = inject(HttpClient);
  private readonly teams = '/api/v1/teams';
  private readonly base = '/api/v1/trainings';

  private page(skip?: number, take?: number): HttpParams {
    let params = new HttpParams();
    if (skip != null) params = params.set('skip', skip);
    if (take != null) params = params.set('take', take);
    return params;
  }

  // --- Team-scoped ----------------------------------------------------------

  listSessions(slug: string, window: 'upcoming' | 'all' = 'upcoming', skip?: number, take?: number): Observable<PagedResult<TrainingSessionRow>> {
    const params = this.page(skip, take).set('window', window);
    return this.http.get<PagedResult<TrainingSessionRow>>(`${this.teams}/${encodeURIComponent(slug)}/trainings/sessions`, { params });
  }

  listSeries(slug: string, skip?: number, take?: number): Observable<PagedResult<TrainingSeriesSummary>> {
    return this.http.get<PagedResult<TrainingSeriesSummary>>(`${this.teams}/${encodeURIComponent(slug)}/trainings/series`, { params: this.page(skip, take) });
  }

  listPublic(slug: string, skip?: number, take?: number): Observable<PagedResult<TrainingSessionRow>> {
    return this.http.get<PagedResult<TrainingSessionRow>>(`${this.teams}/${encodeURIComponent(slug)}/trainings/public`, { params: this.page(skip, take) });
  }

  create(slug: string, request: CreateTrainingRequest): Observable<CreatedTraining> {
    return this.http.post<CreatedTraining>(`${this.teams}/${encodeURIComponent(slug)}/trainings`, request);
  }

  // --- Session-scoped -------------------------------------------------------

  getSession(sessionId: string): Observable<TrainingSessionDetail> {
    return this.http.get<TrainingSessionDetail>(`${this.base}/sessions/${encodeURIComponent(sessionId)}`);
  }

  respond(sessionId: string, answer: TrainingRsvp): Observable<TrainingSessionRow> {
    return this.http.put<TrainingSessionRow>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/response`, { answer });
  }

  editSession(sessionId: string, request: EditSessionRequest): Observable<TrainingSessionDetail> {
    return this.http.patch<TrainingSessionDetail>(`${this.base}/sessions/${encodeURIComponent(sessionId)}`, request);
  }

  skip(sessionId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/skip`, {});
  }

  cancel(sessionId: string): Observable<TrainingSessionRow> {
    return this.http.post<TrainingSessionRow>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/cancel`, {});
  }

  setSessionVisibility(sessionId: string, visibility: TrainingVisibility): Observable<void> {
    return this.http.put<void>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/visibility`, { visibility });
  }

  getAttendance(sessionId: string, group?: TrainingRsvp, skip?: number, take?: number): Observable<PagedResult<AttendanceEntry>> {
    let params = this.page(skip, take);
    if (group) params = params.set('group', group);
    return this.http.get<PagedResult<AttendanceEntry>>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/attendance`, { params });
  }

  removeGuest(sessionId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/sessions/${encodeURIComponent(sessionId)}/guests/${encodeURIComponent(userId)}`);
  }

  // --- Series-scoped --------------------------------------------------------

  editSeries(trainingId: string, request: EditSeriesRequest): Observable<SeriesEditResult> {
    return this.http.patch<SeriesEditResult>(`${this.base}/${encodeURIComponent(trainingId)}`, request);
  }

  setSeriesVisibility(trainingId: string, visibility: TrainingVisibility): Observable<void> {
    return this.http.put<void>(`${this.base}/${encodeURIComponent(trainingId)}/visibility`, { visibility });
  }

  // --- Dashboard ------------------------------------------------------------

  myAgenda(skip?: number, take?: number): Observable<PagedResult<AgendaSession>> {
    return this.http.get<PagedResult<AgendaSession>>(`/api/v1/me/trainings`, { params: this.page(skip, take) });
  }
}
