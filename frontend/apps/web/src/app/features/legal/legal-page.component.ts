import { Component, DestroyRef, OnInit, computed, inject, input, ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoLocaleService } from '@jsverse/transloco-locale';
import { AlertComponent, ButtonDirective, LoadingComponent, PageContainerComponent } from '../../shared/ui';
import { LanguageSwitcherComponent } from '../settings/language/language-switcher.component';
import { LegalContentService, type LegalSection } from './legal-content.service';

/** Which of the two documents this page renders. */
export type LegalDocumentKey = 'privacy' | 'imprint';

/** The German text is the binding one; every other language is an informational translation. */
const AUTHORITATIVE_LANG = 'de';

/**
 * The shared long-form document shell for the privacy policy and the imprint (feature 036).
 *
 * Implements the DESIGN.md "Long-form content" treatment: a `container-sm` measure (~70–75
 * characters), an unbroken `h1` → `h2` → `h3` hierarchy so a screen reader can traverse the
 * document section by section, a caption-step meta line, and no card, shadow or accent field —
 * text on the page background, nothing competing with the words.
 *
 * Rendered OUTSIDE the app shell on purpose (contracts/routes.md §1): the shell's anonymous bar
 * pushes sign-in and register, which is the wrong framing for a reader who has not decided to
 * register — and often the reason they are on this page at all.
 *
 * No `[innerHTML]` anywhere. Paragraphs are array entries in the catalog, never strings carrying
 * markup, so there is no sink to sanitise (constitution I).
 */
@Component({
  selector: 'jh-legal-page',
  imports: [
    RouterLink,
    TranslocoDirective,
    PageContainerComponent,
    LoadingComponent,
    AlertComponent,
    ButtonDirective,
    LanguageSwitcherComponent,
  ],
  providers: [LegalContentService],
  templateUrl: './legal-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './legal-page.component.css',
})
export class LegalPageComponent implements OnInit {
  /** Which document to render. */
  readonly doc = input.required<LegalDocumentKey>();
  /** Section keys in reading order. Explicit, so rendering never depends on JSON key order. */
  readonly sectionOrder = input.required<readonly string[]>();
  /** Long documents get an anchored table of contents; the imprint does not need one. */
  readonly showToc = input(false);
  /** Route of the sibling document, so the two always link to each other (FR-016). */
  readonly siblingLink = input.required<string>();
  /** Catalog key for the sibling's label. */
  readonly siblingLabelKey = input.required<string>();

  private readonly service = inject(LegalContentService);
  private readonly locale = inject(TranslocoLocaleService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly failed = this.service.failed;
  protected readonly content = this.service.content;

  protected readonly document = computed(() => this.content()?.[this.doc()] ?? null);
  protected readonly meta = computed(() => this.content()?.meta ?? null);
  protected readonly crossLink = computed(() => this.content()?.crossLink ?? null);

  /**
   * Only shown on the non-authoritative translations (FR-019). A reader of the German text is
   * reading the binding version and does not need telling that the German version governs.
   */
  protected readonly showAuthoritativeNotice = computed(() => this.service.lang() !== AUTHORITATIVE_LANG);

  /**
   * "Last updated <date>", with the date formatted for the active locale (031 FR-009) — a
   * German reader sees 31. Juli 2026, not 2026-07-31. Built here rather than with a pipe so the
   * label and its date stay one translated sentence.
   */
  protected readonly lastUpdatedLine = computed(() => {
    const meta = this.meta();
    if (!meta) return '';

    const formatted = this.locale.localizeDate(meta.lastUpdated, this.service.lang(), { dateStyle: 'long' });
    return meta.lastUpdatedLabel.replace('{{date}}', formatted);
  });

  /** Section keys paired with their content, in the declared reading order. */
  protected readonly sections = computed<{ key: string; section: LegalSection }[]>(() => {
    const doc = this.document();
    if (!doc) return [];

    return this.sectionOrder()
      .filter((key) => doc.sections[key])
      .map((key) => ({ key, section: doc.sections[key] }));
  });

  ngOnInit(): void {
    this.service.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  /** Stable anchor id so an external deep link to a section keeps working (PC-4). */
  protected anchor(key: string): string {
    return `${this.doc()}-${key}`;
  }

  protected retry(): void {
    this.service.retry();
  }
}
