import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/language.service';
import {
  LANGUAGE_ENDONYMS,
  SUPPORTED_LANGUAGES,
  SupportedLanguage,
  isSupportedLanguage,
} from '../../../core/i18n/supported-languages';

/**
 * Language switcher (feature 031, US2). A compact, accessible native select listing each language
 * in its own name (endonym, FR-014) with the active one selected. Reachable both signed-out (auth
 * screens / public chrome) and signed-in (nav / settings) per clarification Q2. Selecting applies
 * immediately (FR-004) and persists via LanguageService.
 */
@Component({
  selector: 'jh-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  templateUrl: './language-switcher.component.html',
  styleUrl: './language-switcher.component.css',
})
export class LanguageSwitcherComponent {
  private readonly languageService = inject(LanguageService);

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly endonyms = LANGUAGE_ENDONYMS;
  protected readonly current = this.languageService.language;

  protected onChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (isSupportedLanguage(value)) {
      this.languageService.select(value as SupportedLanguage);
    }
  }
}
