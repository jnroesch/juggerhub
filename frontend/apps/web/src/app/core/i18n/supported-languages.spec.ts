import {
  DEFAULT_LANGUAGE,
  isSupportedLanguage,
  matchSupportedLanguage,
  resolveLanguage,
} from './supported-languages';

describe('supported-languages', () => {
  describe('matchSupportedLanguage (FR-003 base matching)', () => {
    it('collapses regional variants to the supported base language', () => {
      expect(matchSupportedLanguage('de-AT')).toBe('de');
      expect(matchSupportedLanguage('es-MX')).toBe('es');
      expect(matchSupportedLanguage('en-GB')).toBe('en');
    });

    it('is case-insensitive', () => {
      expect(matchSupportedLanguage('DE')).toBe('de');
    });

    it('returns null for unsupported or empty tags', () => {
      expect(matchSupportedLanguage('fr')).toBeNull();
      expect(matchSupportedLanguage('')).toBeNull();
      expect(matchSupportedLanguage(null)).toBeNull();
      expect(matchSupportedLanguage(undefined)).toBeNull();
    });
  });

  describe('isSupportedLanguage', () => {
    it('matches only the allowlist', () => {
      expect(isSupportedLanguage('de')).toBe(true);
      expect(isSupportedLanguage('fr')).toBe(false);
      expect(isSupportedLanguage(null)).toBe(false);
    });
  });

  describe('resolveLanguage (FR-007 precedence)', () => {
    it('prefers the account preference over everything', () => {
      expect(resolveLanguage('es', 'de', 'en-US')).toBe('es');
    });

    it('falls to the local choice when no account preference', () => {
      expect(resolveLanguage(null, 'de', 'en-US')).toBe('de');
    });

    it('falls to the browser language when no account or local choice', () => {
      expect(resolveLanguage(null, null, 'de-AT')).toBe('de');
    });

    it('falls back to English when nothing is supported', () => {
      expect(resolveLanguage('fr', 'it', 'ja')).toBe(DEFAULT_LANGUAGE);
      expect(resolveLanguage(null, null, null)).toBe('en');
    });

    it('skips an unsupported account preference and uses the next candidate', () => {
      expect(resolveLanguage('fr', 'es', 'en')).toBe('es');
    });
  });
});
