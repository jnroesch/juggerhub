/**
 * Guards `returnUrl` values against open redirects: only internal, single-slash
 * paths are honoured. Anything absolute (`https://…`), protocol-relative (`//evil`),
 * or absent collapses to `null` so callers fall back to a safe default.
 *
 * Shared by the auth flow (sign-in → onboarding → register) so a returnUrl carried
 * across those hops — e.g. an invite opened while signed out — survives intact
 * without each component re-deriving the safety check.
 */
export function safeReturnUrl(url: string | null | undefined): string | null {
  return url && url.startsWith('/') && !url.startsWith('//') ? url : null;
}
