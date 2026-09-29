import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateTeamPoll, PagedResult, TeamPoll, TeamPollState, UpdateTeamPoll } from '../models/poll.models';

/**
 * Team polls API client (feature 062). Stateless: the card keeps its own lists. Every rule —
 * who may ask, answer, change or delete, and what an anonymous poll reveals — is the server's;
 * nothing here is a boundary. No call is retried automatically: the retry interceptor only
 * retries GET/HEAD, and a member retries a change by pressing again.
 */
@Injectable({ providedIn: 'root' })
export class PollService {
  private readonly http = inject(HttpClient);

  list(slug: string, state: TeamPollState, skip = 0, take = 10): Observable<PagedResult<TeamPoll>> {
    return this.http.get<PagedResult<TeamPoll>>(this.base(slug), {
      params: new HttpParams().set('state', state).set('skip', skip).set('take', take),
    });
  }

  create(slug: string, body: CreateTeamPoll): Observable<TeamPoll> {
    return this.http.post<TeamPoll>(this.base(slug), body);
  }

  update(slug: string, pollId: string, body: UpdateTeamPoll): Observable<TeamPoll> {
    return this.http.put<TeamPoll>(this.poll(slug, pollId), body);
  }

  answer(slug: string, pollId: string, optionIds: string[]): Observable<TeamPoll> {
    return this.http.put<TeamPoll>(`${this.poll(slug, pollId)}/answer`, { optionIds });
  }

  withdraw(slug: string, pollId: string): Observable<TeamPoll> {
    return this.http.delete<TeamPoll>(`${this.poll(slug, pollId)}/answer`);
  }

  close(slug: string, pollId: string): Observable<TeamPoll> {
    return this.http.post<TeamPoll>(`${this.poll(slug, pollId)}/close`, null);
  }

  remove(slug: string, pollId: string): Observable<void> {
    return this.http.delete<void>(this.poll(slug, pollId));
  }

  private base(slug: string): string {
    return `/api/v1/teams/${encodeURIComponent(slug)}/polls`;
  }

  private poll(slug: string, pollId: string): string {
    return `${this.base(slug)}/${encodeURIComponent(pollId)}`;
  }
}
