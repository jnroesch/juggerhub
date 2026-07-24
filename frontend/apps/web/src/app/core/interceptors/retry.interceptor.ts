import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { throwError, timeout, timer } from 'rxjs';
import { retry } from 'rxjs/operators';

/** Time limit for a SINGLE attempt. Bounds the attempt, not the whole retry sequence. */
export const REQUEST_TIMEOUT_MS = 15_000;

/** Retries after the first try, so at most three requests leave the browser. */
export const MAX_RETRIES = 2;

/** First backoff step; doubles per attempt and carries jitter. */
export const BASE_BACKOFF_MS = 300;

/**
 * Statuses worth asking again about. Everything absent from this list is a decision the server
 * already made, and asking twice only repeats it.
 *
 * `0` is a network-level failure (offline, DNS, connection reset, CORS) — Angular reports these
 * with status 0 rather than a real code.
 *
 * **429 is deliberately absent.** It is our own fail-closed chat rate limiter, and retrying it
 * defeats the limit it exists to enforce. Note this is the opposite of the *outbound* rule on the
 * backend, where a 429 means a provider is throttling us and backing off IS correct — same status
 * code, opposite right answer (specs/028-network-resilience/research.md §4).
 */
const RETRYABLE_STATUSES = new Set([0, 408, 502, 503, 504]);

/**
 * Methods safe to repeat. A read can be asked again free of consequence; a write cannot, because a
 * request that timed out may already have been applied and the browser cannot tell the difference.
 */
const SAFE_METHODS = new Set(['GET', 'HEAD']);

/**
 * Endpoints whose failures the caller handles deliberately — the auth flows. Mirrors the
 * `SKIP_REFRESH` list in {@link authInterceptor}: those requests keep their current
 * single-attempt behaviour so sign-in stays fast and predictable.
 */
const SKIP_RETRY = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
  '/auth/verify-email',
  '/auth/resend-verification',
  '/auth/me',
];

/**
 * Gives every request a bounded time limit and quietly retries transient failures on safe reads
 * (feature 028; constitution Principle VII).
 *
 * **Registration order matters and is part of the contract.** This interceptor must be registered
 * AFTER {@link authInterceptor} in `app.config.ts`, which makes it the *inner* of the two. That
 * ordering is what keeps the session-refresh behaviour intact:
 *
 * - a **401** is not in {@link RETRYABLE_STATUSES}, so it passes straight through this layer and is
 *   handled exactly once by the outer auth interceptor — refresh once, retry once;
 * - a **transient fault** is absorbed here and never reaches the auth interceptor at all.
 *
 * Reversing the order would place retry *outside* the refresh, letting a single expired session
 * drive several refresh cycles. That is a defect, not a preference.
 *
 * Failures that survive every attempt propagate unchanged, so the existing per-screen
 * error-and-retry treatments keep working untouched.
 */
export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  const retryable =
    SAFE_METHODS.has(req.method.toUpperCase()) && !SKIP_RETRY.some((path) => req.url.includes(path));

  // The timeout sits INSIDE the retry, so it bounds each attempt rather than the whole sequence,
  // and a timed-out attempt becomes a retryable failure rather than a final one.
  const attempt$ = next(req).pipe(timeout({ each: REQUEST_TIMEOUT_MS }));

  if (!retryable) {
    // Still time-limited: FR-001 applies to every request, including the ones never retried.
    return attempt$;
  }

  return attempt$.pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error: unknown, retryCount: number) => {
        if (!isTransient(error)) {
          return throwError(() => error);
        }

        // Exponential growth with full jitter. The randomness matters more than the growth: it
        // stops every client that failed at the same instant from retrying in the same instant.
        const ceiling = BASE_BACKOFF_MS * 2 ** (retryCount - 1);
        return timer(Math.random() * ceiling);
      },
    }),
  );
};

/**
 * True only for failures where asking again could plausibly get a different answer: a per-attempt
 * timeout, or one of the transient transport/server statuses.
 */
function isTransient(error: unknown): boolean {
  if (error instanceof HttpErrorResponse) {
    return RETRYABLE_STATUSES.has(error.status);
  }
  // rxjs `timeout` rejects with a TimeoutError, which is not an HttpErrorResponse.
  return (error as { name?: string } | null)?.name === 'TimeoutError';
}
