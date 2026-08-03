import { Component } from '@angular/core';
import { LegalPageComponent, type LegalSiblingLink } from '../legal-page.component';

/**
 * Reading order for the privacy policy (feature 036). Declared here rather than inferred from
 * JSON key order, so the document's structure is explicit and a reordered catalog cannot quietly
 * reshuffle a legal text.
 *
 * The sections are deliberately organised by **category of data and purpose**, not by product
 * feature. An earlier draft had one section per feature (profile, chat, location, trainings,
 * marketplace…) and would have needed an edit — to a legally binding document, in three languages
 * — every time a feature shipped. A stale privacy policy is worse than a general one, so the
 * categories are written to absorb new features rather than enumerate the current ones.
 *
 * The order tells a story: who we are → what we hold → why we may → how we measure → why that
 * needs no banner → what's on your device → who else is involved → how long → what you can do.
 */
export const PRIVACY_SECTIONS = [
  'controller',
  'whatWeHold',
  'whyAndOnWhatBasis',
  'analytics',
  'legalBasis',
  'storage',
  'processors',
  'retention',
  'rights',
  'objection',
] as const;

/** The privacy policy at `/privacy` — public, unguarded, and outside the app shell. */
@Component({
  selector: 'jh-privacy',
  imports: [LegalPageComponent],
  templateUrl: './privacy.component.html',
})
export class PrivacyComponent {
  protected readonly sections = PRIVACY_SECTIONS;

  /** The other two legal documents (036 FR-016, extended to three documents by 041). */
  protected readonly siblings: readonly LegalSiblingLink[] = [
    { link: '/terms', labelKey: 'toTermsLong' },
    { link: '/imprint', labelKey: 'toImprintLong' },
  ];
}
