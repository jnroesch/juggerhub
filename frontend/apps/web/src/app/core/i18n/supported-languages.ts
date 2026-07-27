/**
 * The single source of truth for which languages JuggerHub ships (feature 031, FR-017/FR-018).
 * Adding a language is a one-line change here plus its catalogs / `.resx` / email folder on the
 * backend — no architecture change (SC-008). English is the source and universal fallback.
 *
 * Kept in parity with the backend supported-culture list (RequestLocalization + the language
 * `PUT` validator).
 */
export const SUPPORTED_LANGUAGES = ['en', 'de', 'es'] as const;

export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

/** The default and fallback language for the whole app (FR-018). */
export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

/** Human-readable endonym for each language — shown in the switcher in its own language (FR-014). */
export const LANGUAGE_ENDONYMS: Record<SupportedLanguage, string> = {
  en: 'English',
  de: 'Deutsch',
  es: 'Español',
};

/** transloco-locale mapping: which locale drives date/number formatting for each language (FR-009). */
export const LANG_TO_LOCALE: Record<SupportedLanguage, string> = {
  en: 'en-US',
  de: 'de-DE',
  es: 'es-ES',
};

export function isSupportedLanguage(value: string | null | undefined): value is SupportedLanguage {
  return !!value && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/**
 * Resolve the effective language from the fixed precedence (FR-007): explicit account preference,
 * then locally stored choice, then browser language, then English. Each candidate is base-matched
 * (FR-003) and unsupported values are skipped, so the result is always a supported language.
 */
export function resolveLanguage(
  accountPreference: string | null | undefined,
  storedChoice: string | null | undefined,
  browserLanguage: string | null | undefined,
): SupportedLanguage {
  return (
    matchSupportedLanguage(accountPreference) ??
    matchSupportedLanguage(storedChoice) ??
    matchSupportedLanguage(browserLanguage) ??
    DEFAULT_LANGUAGE
  );
}

/**
 * Collapse a BCP-47 tag to a supported base language, or null when unsupported (FR-003).
 * e.g. "de-AT" -> "de", "es-MX" -> "es", "fr" -> null.
 */
export function matchSupportedLanguage(tag: string | null | undefined): SupportedLanguage | null {
  if (!tag) return null;
  const base = tag.toLowerCase().split('-')[0];
  return isSupportedLanguage(base) ? base : null;
}
