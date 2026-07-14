import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/profile.models';
import {
  CreatePartyInviteRequest,
  CreatePartyNewsRequest,
  FormPartyRequest,
  Party,
  PartyContext,
  PartyInvitableUser,
  PartyInvitation,
  PartyInviteLink,
  PartyInvitePreview,
  PartyMember,
  PartyNews,
  PartyRequestCard,
  PartyRosterGroup,
} from '../models/party.models';

/**
 * Party API client (feature 016). Forming a party starts from an event; the crew is gathered inside
 * the team; applying hands off to the event's own sign-up flow. Server-side authorization is the
 * real boundary — this is UX only. Every list is paginated.
 */
@Injectable({ providedIn: 'root' })
export class PartyService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/parties';
  private readonly invites = '/api/v1/party-invitations';
  private readonly events = '/api/v1/events';
  private readonly teams = '/api/v1/teams';

  private page(skip?: number, take?: number): HttpParams {
    let params = new HttpParams();
    if (skip != null) params = params.set('skip', skip);
    if (take != null) params = params.set('take', take);
    return params;
  }

  // --- Discovery ------------------------------------------------------------

  getPartyContext(eventId: string): Observable<PartyContext> {
    return this.http.get<PartyContext>(`${this.events}/${encodeURIComponent(eventId)}/party-context`);
  }

  getTeamPartyRequests(slug: string, skip?: number, take?: number): Observable<PagedResult<PartyRequestCard>> {
    return this.http.get<PagedResult<PartyRequestCard>>(
      `${this.teams}/${encodeURIComponent(slug)}/party-requests`,
      { params: this.page(skip, take) },
    );
  }

  // --- Lifecycle ------------------------------------------------------------

  formParty(request: FormPartyRequest): Observable<Party> {
    return this.http.post<Party>(this.base, request);
  }

  getParty(id: string): Observable<Party> {
    return this.http.get<Party>(`${this.base}/${encodeURIComponent(id)}`);
  }

  apply(id: string): Observable<Party> {
    return this.http.post<Party>(`${this.base}/${encodeURIComponent(id)}/apply`, {});
  }

  withdraw(id: string): Observable<Party> {
    return this.http.post<Party>(`${this.base}/${encodeURIComponent(id)}/withdraw`, {});
  }

  disband(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}`);
  }

  // --- Roster ---------------------------------------------------------------

  listMembers(id: string, group: PartyRosterGroup, skip?: number, take?: number): Observable<PagedResult<PartyMember>> {
    const params = this.page(skip, take).set('group', group);
    return this.http.get<PagedResult<PartyMember>>(`${this.base}/${encodeURIComponent(id)}/members`, { params });
  }

  join(id: string): Observable<PartyMember> {
    return this.http.post<PartyMember>(`${this.base}/${encodeURIComponent(id)}/join`, {});
  }

  decline(id: string): Observable<PartyMember> {
    return this.http.post<PartyMember>(`${this.base}/${encodeURIComponent(id)}/decline`, {});
  }

  leave(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(id)}/leave`, {});
  }

  removeMember(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/members/${encodeURIComponent(userId)}`);
  }

  nudge(id: string, userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(id)}/members/${encodeURIComponent(userId)}/nudge`, {});
  }

  // --- News -----------------------------------------------------------------

  listNews(id: string, skip?: number, take?: number): Observable<PagedResult<PartyNews>> {
    return this.http.get<PagedResult<PartyNews>>(`${this.base}/${encodeURIComponent(id)}/news`, { params: this.page(skip, take) });
  }

  postNews(id: string, request: CreatePartyNewsRequest): Observable<PartyNews> {
    return this.http.post<PartyNews>(`${this.base}/${encodeURIComponent(id)}/news`, request);
  }

  // --- Co-admin invitations -------------------------------------------------

  getInviteLink(id: string): Observable<PartyInviteLink | null> {
    return this.http.get<PartyInviteLink | null>(`${this.base}/${encodeURIComponent(id)}/invitations/link`);
  }

  rotateInviteLink(id: string): Observable<PartyInviteLink> {
    return this.http.post<PartyInviteLink>(`${this.base}/${encodeURIComponent(id)}/invitations/link`, {});
  }

  listInvites(id: string, skip?: number, take?: number): Observable<PagedResult<PartyInvitation>> {
    return this.http.get<PagedResult<PartyInvitation>>(`${this.base}/${encodeURIComponent(id)}/invitations`, { params: this.page(skip, take) });
  }

  createInvite(id: string, request: CreatePartyInviteRequest): Observable<PartyInvitation> {
    return this.http.post<PartyInvitation>(`${this.base}/${encodeURIComponent(id)}/invitations`, request);
  }

  searchMembers(id: string, query: string, skip?: number, take?: number): Observable<PagedResult<PartyInvitableUser>> {
    const params = this.page(skip, take).set('query', query);
    return this.http.get<PagedResult<PartyInvitableUser>>(`${this.base}/${encodeURIComponent(id)}/invitations/member-search`, { params });
  }

  revokeInvite(id: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/invitations/${encodeURIComponent(invitationId)}`);
  }

  previewInvite(token: string): Observable<PartyInvitePreview> {
    return this.http.get<PartyInvitePreview>(`${this.invites}/${encodeURIComponent(token)}`);
  }

  acceptInvite(token: string): Observable<{ partyId: string }> {
    return this.http.post<{ partyId: string }>(`${this.invites}/${encodeURIComponent(token)}/accept`, {});
  }

  declineInvite(token: string): Observable<void> {
    return this.http.post<void>(`${this.invites}/${encodeURIComponent(token)}/decline`, {});
  }
}
