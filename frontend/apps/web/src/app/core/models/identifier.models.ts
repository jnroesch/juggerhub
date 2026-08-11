/**
 * Shared contract for the two user-chosen identifiers in the product: the profile `@handle`
 * and the team slug (mirror of backend `HandleRejection` / `SlugRejection`).
 *
 * The availability endpoints return a CODE, never a sentence — the server's own prose is
 * English-only, and these refusals are read by someone mid-signup who is least able to work
 * around a language they don't speak. The sentence is chosen here, from the catalogues.
 */
export type IdentifierRejection =
  | 'Empty'
  | 'TooShort'
  | 'TooLong'
  | 'InvalidFormat'
  | 'Reserved'
  | 'Taken';

/**
 * Bounds for both identifiers, mirroring `ProfileOptions.Handle*` / `TeamOptions.Slug*`.
 * They are the parameters of the `tooShort` / `tooLong` sentences, so the message and the
 * validator that produces it cannot drift apart.
 */
export const IDENTIFIER_MIN_LENGTH = 3;
export const IDENTIFIER_MAX_LENGTH = 30;
