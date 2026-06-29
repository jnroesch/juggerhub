import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Health read model returned by GET /api/v1/health. */
export interface Health {
  status: 'healthy' | 'degraded' | 'unhealthy';
  database: 'reachable' | 'unreachable';
  version: string;
  timestamp: string;
}

/**
 * Calls the backend health endpoint. The URL is relative and same-origin (served
 * through the nginx /api proxy), so no environment-specific base URL is needed.
 */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  getHealth(): Observable<Health> {
    return this.http.get<Health>('/api/v1/health');
  }
}
