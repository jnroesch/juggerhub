/**
 * Feature 061 — the site a team link leads to, as shown beside its label (FR-017): a label is
 * free text and can say "Instagram" while pointing anywhere, so the page always shows where the
 * link really goes.
 *
 * `new URL(...).host` is the WHATWG parser's serialisation: an internationalised host comes back
 * in its ASCII (punycode) form, so a lookalike such as a Cyrillic "і" in "іnstagram.com" shows as
 * `xn--…` rather than passing for the real site. A leading `www.` is dropped for readability —
 * that can never make one site look like another. Anything unparseable yields '' (the server only
 * stores valid https addresses, so this is defensive).
 */
export function linkHost(url: string): string {
  try {
    return new URL(url).host.replace(/^www\./, '');
  } catch {
    return '';
  }
}
