import { ParamMap } from '@angular/router';

/**
 * The two path segments of a shared invite link, `/join/{slug}/{token}` (feature 053).
 *
 * This is the thing that survives the email-verification hop: the register form sends it, the
 * server puts it on the verification link, the verify page reads it back, and the onboarding
 * wizard offers the invitation it names. It is an identity, never a destination — every place
 * that turns it into a path composes that path here, from validated parts, so a tampered value
 * can only ever mean "no invite" and can never send anyone anywhere.
 *
 * One util for four components, for the same reason `safeReturnUrl` is one util: four
 * hand-written regexes for the same two segments would be four places for the shape to drift.
 * The two patterns below are the ones the server applies (`InviteReference.TryParse`).
 */
export interface InviteRef {
  slug: string;
  token: string;
}

/** Team slugs: lowercase alphanumeric segments joined by single hyphens, 3–30 chars (TeamSlugPolicy). */
const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const SLUG_MIN = 3;
const SLUG_MAX = 30;
/** Invite tokens: base64url, 43 chars today; bounds rather than an exact length (research R2). */
const TOKEN = /^[A-Za-z0-9_-]{16,128}$/;

const JOIN_PATH = /^\/join\/([^/?#]+)\/([^/?#]+)(?:\?action=(?:accept|decline))?$/;

/** Both parts well-formed, or null. */
export function inviteRef(slug: string | null | undefined, token: string | null | undefined): InviteRef | null {
  if (!slug || !token) {
    return null;
  }
  const s = slug.trim();
  const t = token.trim();
  if (s.length < SLUG_MIN || s.length > SLUG_MAX || !SLUG.test(s) || !TOKEN.test(t)) {
    return null;
  }
  return { slug: s, token: t };
}

/**
 * The invite a `returnUrl` points at, if it is the invite page — with or without the
 * `?action=` the invite page appends for a signed-out visitor. Anything else is null.
 */
export function inviteFromReturnUrl(url: string | null | undefined): InviteRef | null {
  if (!url) {
    return null;
  }
  const match = JOIN_PATH.exec(url);
  return match ? inviteRef(match[1], match[2]) : null;
}

/** The invite carried on a verification link (`inviteSlug` + `inviteToken` query params). */
export function inviteFromQuery(params: ParamMap): InviteRef | null {
  return inviteRef(params.get('inviteSlug'), params.get('inviteToken'));
}

/** What sign-in should carry: the invite page, with the accept resumed after authentication. */
export function inviteReturnUrl(ref: InviteRef): string {
  return `${invitePagePath(ref)}?action=accept`;
}

/** The invite page itself, with no automatic action. */
export function invitePagePath(ref: InviteRef): string {
  return `/join/${ref.slug}/${ref.token}`;
}
