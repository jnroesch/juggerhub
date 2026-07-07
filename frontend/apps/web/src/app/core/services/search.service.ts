import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EventBrowseParams,
  EventCard,
  PagedResult,
  PlayerBrowseParams,
  PlayerCard,
  TeamBrowseParams,
  TeamCard,
} from '../models/search.models';

/**
 * Browse/search API client (feature 007). All three endpoints are anonymous and do the
 * filtering/sorting/paging server-side — this client only shapes query params. Server-side
 * authorization + the player opt-in gate are the real boundary; this is UX only.
 */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpClient);

  browseTeams(params: TeamBrowseParams): Observable<PagedResult<TeamCard>> {
    return this.http.get<PagedResult<TeamCard>>('/api/v1/teams', { params: toTeamParams(params) });
  }

  browseEvents(params: EventBrowseParams): Observable<PagedResult<EventCard>> {
    return this.http.get<PagedResult<EventCard>>('/api/v1/events', { params: toEventParams(params) });
  }

  browsePlayers(params: PlayerBrowseParams): Observable<PagedResult<PlayerCard>> {
    return this.http.get<PagedResult<PlayerCard>>('/api/v1/profiles', { params: toPlayerParams(params) });
  }
}

/** Append a value only when it is meaningfully present (skips undefined/null/empty strings). */
function put(params: HttpParams, key: string, value: string | number | boolean | null | undefined): HttpParams {
  if (value === undefined || value === null) {
    return params;
  }
  if (typeof value === 'string' && value.trim().length === 0) {
    return params;
  }
  return params.set(key, String(value));
}

function toTeamParams(p: TeamBrowseParams): HttpParams {
  let params = new HttpParams();
  params = put(params, 'q', p.q);
  params = put(params, 'activeOnly', p.activeOnly);
  params = put(params, 'beginnersWelcome', p.beginnersWelcome);
  params = put(params, 'city', p.city);
  params = put(params, 'sort', p.sort);
  params = put(params, 'skip', p.skip);
  params = put(params, 'take', p.take);
  return params;
}

function toEventParams(p: EventBrowseParams): HttpParams {
  let params = new HttpParams();
  params = put(params, 'q', p.q);
  params = put(params, 'hidePast', p.hidePast);
  params = put(params, 'from', p.from);
  params = put(params, 'to', p.to);
  params = put(params, 'type', p.type);
  params = put(params, 'city', p.city);
  params = put(params, 'sort', p.sort);
  params = put(params, 'skip', p.skip);
  params = put(params, 'take', p.take);
  return params;
}

function toPlayerParams(p: PlayerBrowseParams): HttpParams {
  let params = new HttpParams();
  params = put(params, 'q', p.q);
  for (const position of p.positions ?? []) {
    params = params.append('positions', position);
  }
  params = put(params, 'city', p.city);
  params = put(params, 'sort', p.sort);
  params = put(params, 'skip', p.skip);
  params = put(params, 'take', p.take);
  return params;
}
