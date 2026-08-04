import { inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import { relativeTime } from '../utils/format';

/**
 * A `relativeTime` bound to the app's active language (feature 031).
 *
 * `relativeTime` is a pure function and takes a locale it cannot obtain itself; this is the one
 * place that supplies it. The returned closure reads a signal, so calling it inside a `computed()`
 * or a template binding — which is every call site — also re-renders the string when the viewer
 * switches language, matching how the surrounding copy behaves.
 *
 * Must be called in an injection context (field initializer / constructor).
 *
 *   private readonly rel = injectRelativeTime();
 *   protected readonly time = computed(() => this.rel(this.item().createdDate));
 */
export function injectRelativeTime(): (iso: string) => string {
  const transloco = inject(TranslocoService);
  const lang = toSignal(transloco.langChanges$, { initialValue: transloco.getActiveLang() });
  return (iso: string) => relativeTime(iso, lang());
}
