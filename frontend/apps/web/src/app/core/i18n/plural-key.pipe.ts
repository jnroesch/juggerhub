import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { resolvePluralKey } from './plural';

/**
 * Turns a plural message's key into the key of the form a count needs (GH #338):
 *
 *   {{ 'myTeam.members' | pluralKey: team.memberCount | transloco }}
 *
 * It only chooses the key; the `transloco` pipe after it still does the loading, the fallback and
 * the re-render on a language switch. See `plural.ts` for the catalogue convention.
 *
 * Impure because the answer depends on the active language as well as the arguments: when the
 * viewer switches language the `transloco` pipe marks the view for check, this runs again under
 * the new language's rules, and the new key reaches the `transloco` pipe in the same pass. The
 * work per run is a map lookup and one `Intl.PluralRules.select`.
 */
@Pipe({ name: 'pluralKey', pure: false })
export class PluralKeyPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);

  transform(key: string, count: number): string {
    return resolvePluralKey(this.transloco, key, count);
  }
}
