import { Component } from '@angular/core';
import { LegalPageComponent } from '../legal-page.component';

/**
 * Reading order for the privacy policy (feature 036). Declared here rather than inferred from
 * JSON key order, so the document's structure is explicit and a reordered catalog cannot quietly
 * reshuffle a legal text.
 *
 * The order tells a story: who we are → what we hold, roughly in the order you encounter it →
 * how we measure → what we rely on → who else sees it → how long → what you can do about it.
 */
export const PRIVACY_SECTIONS = [
  'controller',
  'account',
  'email',
  'profile',
  'location',
  'chat',
  'participation',
  'eventContacts',
  'media',
  'language',
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
}
