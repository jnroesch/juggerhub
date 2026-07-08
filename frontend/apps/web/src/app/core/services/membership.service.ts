import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { MyTeam, PagedResult } from '../models/home.models';
import { myTeamTarget } from '../../layout/nav-model';

/**
 * The signed-in player's team memberships (feature 008). Loaded once by the shell and used to
 * drive the nav "My team" target (0/1/many) and the dashboard's team-scoped modules. UX only —
 * the server enforces every entitlement.
 */
@Injectable({ providedIn: 'root' })
export class MembershipService {
  private readonly http = inject(HttpClient);

  private readonly teamsSig = signal<MyTeam[]>([]);
  private readonly loadedSig = signal(false);

  readonly teams = this.teamsSig.asReadonly();
  readonly loaded = this.loadedSig.asReadonly();
  readonly hasTeam = computed(() => this.teamsSig().length > 0);
  /** Where the "My team" destination should navigate for this player. */
  readonly myTeamTarget = computed(() => myTeamTarget(this.teamsSig()));

  /** Fetch the caller's teams (a small list; one page is plenty). Safe to call repeatedly. */
  load(): void {
    this.http
      .get<PagedResult<MyTeam>>('/api/v1/profiles/me/teams', {
        params: new HttpParams().set('skip', 0).set('take', 100),
      })
      .pipe(
        tap((page) => {
          this.teamsSig.set(page.items);
          this.loadedSig.set(true);
        }),
        catchError(() => {
          // Not signed in / transient — treat as no teams; the shell still renders.
          this.teamsSig.set([]);
          this.loadedSig.set(true);
          return of(null);
        }),
      )
      .subscribe();
  }

  /** Clear cached memberships (e.g. on sign-out). */
  clear(): void {
    this.teamsSig.set([]);
    this.loadedSig.set(false);
  }
}
