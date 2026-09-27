import { TranslocoService } from '@jsverse/transloco';
import { LANG_TO_LOCALE, isSupportedLanguage } from './supported-languages';

/**
 * Plurals live in the catalogue, chosen by the language's own CLDR rules (GH #338).
 *
 * A message whose wording depends on a number is an object keyed by CLDR plural category instead
 * of a string:
 *
 *   "members": { "one": "member", "other": "members" }
 *   "unreadMessages": { "one": "{{count}} unread message", "other": "{{count}} unread messages" }
 *
 * and a call site asks for the category the count falls into, never for a form by name:
 *
 *   {{ 'myTeam.members' | pluralKey: n | transloco }}
 *   {{ 'nav.unreadMessages' | pluralKey: n | transloco: { count: n } }}
 *   translatePlural(this.t, 'teams.invitations.expiresInDays', days)
 *
 * The category comes from `Intl.PluralRules`, i.e. the browser's CLDR data, so each catalogue
 * carries exactly the forms its language has — `one`/`other` for en/de/es, `one`/`few`/`many`/`other`
 * for Polish, all six for Arabic — and adding a language is catalogue work, not a template change.
 * `catalog-plurals.spec.ts` checks every catalogue against those rules.
 *
 * The number is only ever the category's input. Deciding a form in code — `n === 1 ? 'a' : 'b'` —
 * is the English two-form rule compiled into a template, where no translator can see it; that is
 * what this replaced, in thirteen places.
 *
 * Why not ICU MessageFormat (`@jsverse/transloco-messageformat`): its transpiler runs Transloco's
 * `{{ }}` interpolation FIRST and compiles the result, so a player whose display name contains `{`
 * would make every "{{name}} is typing…" throw for everyone who sees it; it compiles with
 * `new Function`, which a future Content-Security-Policy without `unsafe-eval` would forbid; and
 * it caches one compiled function per interpolated string. `Intl.PluralRules` costs nothing to ship.
 */

/** The six CLDR plural categories. Inside a catalogue these names are reserved for plural forms. */
export const PLURAL_CATEGORIES: readonly Intl.LDMLPluralRule[] = ['zero', 'one', 'two', 'few', 'many', 'other'];

const rulesByLang = new Map<string, Intl.PluralRules>();

/** The CLDR category `count` falls into in `lang` — e.g. `one` for 1 in English, `few` for 3 in Polish. */
export function pluralCategory(lang: string, count: number): Intl.LDMLPluralRule {
  let rules = rulesByLang.get(lang);
  if (!rules) {
    rules = new Intl.PluralRules(isSupportedLanguage(lang) ? LANG_TO_LOCALE[lang] : lang);
    rulesByLang.set(lang, rules);
  }
  return rules.select(count);
}

/**
 * The catalogue key of the form `count` needs in the active language: `key.one`, `key.few`, …
 *
 * Falls back to `key.other` when the catalogue has no form for that category. That happens by
 * design for categories no realistic count reaches — Spanish selects `many` for a round million —
 * and transiently while the active catalogue is still loading; the Transloco pipe re-renders once
 * it arrives, and this is evaluated again. `other` is the one form every plural message must have.
 */
export function resolvePluralKey(transloco: TranslocoService, key: string, count: number): string {
  const lang = transloco.getActiveLang();
  const form = `${key}.${pluralCategory(lang, count)}`;
  return transloco.getTranslation(lang)[form] !== undefined ? form : `${key}.other`;
}

/** `translate` for a plural message: picks the form and passes `count` for `{{count}}`. */
export function translatePlural(
  transloco: TranslocoService,
  key: string,
  count: number,
  params: Record<string, unknown> = {},
): string {
  return transloco.translate(resolvePluralKey(transloco, key, count), { ...params, count });
}

/**
 * True for a catalogue node that is a plural message: an object whose keys are all CLDR categories.
 * Used by the catalogue guards; an object mixing categories with other keys is ordinary nesting.
 */
export function isPluralNode(node: unknown): node is Partial<Record<Intl.LDMLPluralRule, string>> {
  if (node === null || typeof node !== 'object' || Array.isArray(node)) {
    return false;
  }
  const keys = Object.keys(node);
  return keys.length > 0 && keys.every((k) => (PLURAL_CATEGORIES as readonly string[]).includes(k));
}
