import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language.service';

@Component({
  imports: [RouterOutlet],
  selector: 'jh-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './app.css',
})
export class App {
  // Instantiated at bootstrap so its effect resolves + applies the interface language
  // (browser/local detection now, account preference once the session loads) from the very first
  // render (feature 031).
  private readonly language = inject(LanguageService);
}
