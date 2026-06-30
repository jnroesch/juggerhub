import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a user-safe message from an API error. The backend only ever returns
 * generic ProblemDetails (`detail`) — never internals — so this is safe to show.
 */
export function problemDetail(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === 'object' && typeof body.detail === 'string' && body.detail) {
      return body.detail;
    }
    if (typeof body === 'string' && body) {
      return body;
    }
  }
  return fallback;
}
