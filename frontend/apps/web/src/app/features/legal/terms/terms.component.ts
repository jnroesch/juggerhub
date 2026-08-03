import { Component } from '@angular/core';
import { LegalPageComponent, type LegalSiblingLink } from '../legal-page.component';

/**
 * Reading order for the Terms of Use (feature 041). Declared here rather than inferred from JSON
 * key order, so the document's structure is explicit and a reordered catalog cannot quietly
 * reshuffle a text people are bound by — the same rule `PRIVACY_SECTIONS` follows.
 *
 * The order is an argument, read top to bottom: what this is and who runs it → the account you
 * hold → **how to behave**, the section everyone is actually pointed at → what you keep when you
 * post something → what we may do when a rule is broken → how it ends → what we don't promise →
 * how it changes and which law applies.
 *
 * `behaviour` sits third on purpose. It is the substance of the agreement for almost every
 * reader, and burying the rules under liability boilerplate would be a way of not really
 * publishing them.
 */
export const TERMS_SECTIONS = [
  'whatThisIs',
  'yourAccount',
  'behaviour',
  'yourContent',
  'whatWeMayDo',
  'endingIt',
  'noGuarantees',
  'changesAndLaw',
] as const;

/**
 * The Terms of Use at `/terms` — public, unguarded, and outside the app shell.
 *
 * Unguarded is not an oversight. This document has to be readable *before* someone agrees to it,
 * and the reader who most needs it is the one deciding whether to register at all. See
 * specs/041-community-guidelines-terms/contracts/routes.md (RC-1).
 */
@Component({
  selector: 'jh-terms',
  imports: [LegalPageComponent],
  templateUrl: './terms.component.html',
})
export class TermsComponent {
  protected readonly sections = TERMS_SECTIONS;

  /** The other two legal documents (036 FR-016, extended to three documents by 041). */
  protected readonly siblings: readonly LegalSiblingLink[] = [
    { link: '/privacy', labelKey: 'toPrivacyLong' },
    { link: '/imprint', labelKey: 'toImprintLong' },
  ];
}
