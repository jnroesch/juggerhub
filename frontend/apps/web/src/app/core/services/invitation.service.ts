import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyInvitation, PagedResult } from '../models/team.models';

/**
 * The signed-in player's own pending team invitations (feature 023 — the "My team" home).
 * Read-only here; accepting/declining reuses the existing token endpoints on TeamService.
 * The server scopes the list to the authenticated subject — this is UX only.
 */
@Injectable({ providedIn: 'root' })
export class InvitationService {
  private readonly http = inject(HttpClient);

  /** Fetch the caller's usable (pending + unexpired) targeted invitations, newest-first. */
  listMine(skip = 0, take = 100): Observable<PagedResult<MyInvitation>> {
    return this.http.get<PagedResult<MyInvitation>>('/api/v1/profiles/me/invitations', {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }
}
