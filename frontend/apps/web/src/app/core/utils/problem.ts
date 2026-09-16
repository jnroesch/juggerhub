import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a user-safe message from an API error.
 *
 * ONLY an RFC 7807 `problem+json` body is trusted, and that is the whole point of this helper.
 * Our backend answers every failure with ProblemDetails, so a response shaped like anything else
 * did not come from the backend at all: an oversized body, a restarting pod or a wedged upstream is
 * answered by nginx, the ingress controller or Cloudflare with an HTML error page. Angular cannot
 * parse that page as JSON, so it hands the raw document over as `HttpErrorResponse.error` — a plain
 * string. This helper used to return such a string verbatim, which rendered the entire
 * `<html>…502 Bad Gateway…</html>` document as the error message inside a dialog (#293). Those
 * pages are untranslated, unbounded and say nothing a reader can act on, which is exactly what
 * `fallback` is for.
 */
export function problemDetail(error: unknown, fallback = "We couldn't do that just now. Please try again."): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === 'object' && typeof body.detail === 'string' && body.detail.trim()) {
      return body.detail;
    }
  }
  return fallback;
}
