import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Home, HomeNews, PagedResult, UpNextItem } from '../models/home.models';

/**
 * Home dashboard API client (feature 008). Reads only — RSVP goes through EventService
 * (the existing sign-up endpoints). Server-side authorization is the real boundary.
 */
@Injectable({ providedIn: 'root' })
export class HomeService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/home';

  /** The composite dashboard for the signed-in player (first-paint path). */
  getHome(): Observable<Home> {
    return this.http.get<Home>(this.base);
  }

  /** The player's full upcoming-events list ("see all"), paginated. */
  getUpNext(skip = 0, take = 20): Observable<PagedResult<UpNextItem>> {
    return this.http.get<PagedResult<UpNextItem>>(`${this.base}/up-next`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  /** The player's aggregated news feed ("see all"), paginated. */
  getNews(skip = 0, take = 20): Observable<PagedResult<HomeNews>> {
    return this.http.get<PagedResult<HomeNews>>(`${this.base}/news`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }
}
