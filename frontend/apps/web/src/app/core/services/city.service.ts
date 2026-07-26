import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { CityOption, Country } from '../models/city.models';

/**
 * City type-ahead client (feature 030). Backend-proxied to the self-hosted geocoder; the browser
 * never calls the geocoder directly. This is a GET, so the shared retry interceptor time-limits
 * and safely retries it (constitution VII). A 503 (geocoder unavailable) surfaces to the caller,
 * which shows a retryable transient state.
 */
@Injectable({ providedIn: 'root' })
export class CityService {
  private readonly http = inject(HttpClient);

  /** Search cities by partial name. The backend returns [] for a too-short query (not an error). */
  search(query: string): Observable<CityOption[]> {
    const params = new HttpParams().set('q', query);
    return this.http.get<CityOption[]>('/api/v1/cities/search', { params });
  }

  private countries$?: Observable<Country[]>;

  /**
   * Every country in the reference dataset, for the browse country filter's type-ahead (filtering to
   * an empty country just shows the results' empty state). Slow-changing, so it is fetched once and
   * shared for the session.
   */
  countries(): Observable<Country[]> {
    this.countries$ ??= this.http
      .get<Country[]>('/api/v1/cities/countries')
      .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    return this.countries$;
  }
}
