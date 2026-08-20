/**
 * Showcase gallery contracts (feature 046 / #99) — mirror of the backend's
 * `ShowcaseImageDto`. One shape for both surfaces: a player's gallery and a team's are the
 * same thing with different owners.
 *
 * There is deliberately no image URL here. The server never discloses where a picture is
 * stored, so the client composes the address it fetches from the owner it already knows
 * plus the image id — exactly as it composes an avatar URL from a handle.
 */
export interface ShowcaseImage {
  id: string;
  /** Owner-supplied caption, or null. Untrusted text: bind it, never render it as HTML. */
  caption: string | null;
  /** Dense 0-based position. The listing already arrives in this order. */
  position: number;
}

/** Which gallery a component is showing — selects the API paths and the image addresses. */
export type ShowcaseOwner =
  | { kind: 'profile'; handle: string }
  | { kind: 'team'; slug: string };

/** Why an upload was refused, as far as the client needs to distinguish it. */
export type ShowcaseUploadFailure =
  | 'full'
  | 'type'
  | 'size'
  | 'unreadable'
  | 'unavailable'
  | 'unknown';
