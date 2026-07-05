import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AcceptInviteResult,
  CreateContactRequest,
  CreateEventRequest,
  EditEventRequest,
  EventAdmin,
  EventContact,
  EventDetail,
  EventInvitation,
  EventNews,
  InvitableUser,
  InviteLink,
  InvitePreview,
  PagedResult,
  Signup,
  SignupStatus,
} from '../models/event.models';

/** The three public participant groups, mapped to the backend's `group` query value. */
export type ParticipantGroup = 'joined' | 'awaiting' | 'waitlist';

/**
 * Event API client. The event page + its lists hit anonymous endpoints; sign-up and
 * every admin action carry the session cookie (auth interceptor). Server-side
 * authorization is the real boundary — this is UX only. Every list is paginated.
 */
@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/events';
  private readonly invites = '/api/v1/event-invitations';

  // --- Create / read / edit / cancel ---------------------------------------

  createEvent(request: CreateEventRequest): Observable<EventDetail> {
    return this.http.post<EventDetail>(this.base, request);
  }

  getEvent(id: string): Observable<EventDetail> {
    return this.http.get<EventDetail>(`${this.base}/${encodeURIComponent(id)}`);
  }

  editEvent(id: string, request: EditEventRequest): Observable<EventDetail> {
    return this.http.patch<EventDetail>(`${this.base}/${encodeURIComponent(id)}`, request);
  }

  cancelEvent(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(id)}/cancel`, {});
  }

  // --- Participants ---------------------------------------------------------

  getParticipants(id: string, group: ParticipantGroup, skip = 0, take = 50): Observable<PagedResult<Signup>> {
    return this.http.get<PagedResult<Signup>>(`${this.base}/${encodeURIComponent(id)}/participants`, {
      params: new HttpParams().set('group', group).set('skip', skip).set('take', take),
    });
  }

  signup(id: string, teamId: string | null): Observable<Signup> {
    return this.http.post<Signup>(`${this.base}/${encodeURIComponent(id)}/signup`, { teamId });
  }

  withdraw(id: string, signupId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/signup/${signupId}`);
  }

  approve(id: string, signupId: string): Observable<Signup> {
    return this.http.post<Signup>(`${this.base}/${encodeURIComponent(id)}/participants/${signupId}/approve`, {});
  }

  promote(id: string, signupId: string): Observable<Signup> {
    return this.http.post<Signup>(`${this.base}/${encodeURIComponent(id)}/participants/${signupId}/promote`, {});
  }

  removeParticipant(id: string, signupId: string): Observable<void> {
    return this.withdraw(id, signupId);
  }

  // --- News -----------------------------------------------------------------

  getNews(id: string, skip = 0, take = 20): Observable<PagedResult<EventNews>> {
    return this.http.get<PagedResult<EventNews>>(`${this.base}/${encodeURIComponent(id)}/news`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  postNews(id: string, body: string): Observable<EventNews> {
    return this.http.post<EventNews>(`${this.base}/${encodeURIComponent(id)}/news`, { body });
  }

  // --- Contacts -------------------------------------------------------------

  getContacts(id: string, skip = 0, take = 50): Observable<PagedResult<EventContact>> {
    return this.http.get<PagedResult<EventContact>>(`${this.base}/${encodeURIComponent(id)}/contacts`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  addContact(id: string, request: CreateContactRequest): Observable<EventContact> {
    return this.http.post<EventContact>(`${this.base}/${encodeURIComponent(id)}/contacts`, request);
  }

  updateContact(id: string, contactId: string, request: CreateContactRequest): Observable<EventContact> {
    return this.http.patch<EventContact>(`${this.base}/${encodeURIComponent(id)}/contacts/${contactId}`, request);
  }

  removeContact(id: string, contactId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/contacts/${contactId}`);
  }

  // --- Admins ---------------------------------------------------------------

  getAdmins(id: string, skip = 0, take = 50): Observable<PagedResult<EventAdmin>> {
    return this.http.get<PagedResult<EventAdmin>>(`${this.base}/${encodeURIComponent(id)}/admins`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  removeAdmin(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/admins/${userId}`);
  }

  // --- Co-admin invitations (admin) ----------------------------------------

  getInviteLink(id: string): Observable<InviteLink | null> {
    return this.http.get<InviteLink | null>(`${this.base}/${encodeURIComponent(id)}/invitations/link`);
  }

  rotateInviteLink(id: string): Observable<InviteLink> {
    return this.http.post<InviteLink>(`${this.base}/${encodeURIComponent(id)}/invitations/link`, {});
  }

  getInvitations(id: string, skip = 0, take = 50): Observable<PagedResult<EventInvitation>> {
    return this.http.get<PagedResult<EventInvitation>>(`${this.base}/${encodeURIComponent(id)}/invitations`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  createTargetedInvite(id: string, userId: string): Observable<EventInvitation> {
    return this.http.post<EventInvitation>(`${this.base}/${encodeURIComponent(id)}/invitations`, { userId });
  }

  revokeInvite(id: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}/invitations/${invitationId}`);
  }

  searchUsers(id: string, query: string, skip = 0, take = 20): Observable<PagedResult<InvitableUser>> {
    return this.http.get<PagedResult<InvitableUser>>(`${this.base}/${encodeURIComponent(id)}/invitations/user-search`, {
      params: new HttpParams().set('query', query).set('skip', skip).set('take', take),
    });
  }

  // --- Invitee token flow ---------------------------------------------------

  getInvitePreview(token: string): Observable<InvitePreview> {
    return this.http.get<InvitePreview>(`${this.invites}/${encodeURIComponent(token)}`);
  }

  acceptInvite(token: string): Observable<AcceptInviteResult> {
    return this.http.post<AcceptInviteResult>(`${this.invites}/${encodeURIComponent(token)}/accept`, {});
  }

  declineInvite(token: string): Observable<void> {
    return this.http.post<void>(`${this.invites}/${encodeURIComponent(token)}/decline`, {});
  }

  /** Local convenience — never the security boundary (the server enforces spots). */
  spotsLabel(detail: EventDetail): string {
    const remaining = Math.max(detail.participationLimit - detail.occupiedSpots, 0);
    return detail.isFull ? 'Full' : `${remaining} of ${detail.participationLimit} spots open`;
  }
}

export type { SignupStatus };
