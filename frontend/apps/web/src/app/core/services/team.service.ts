import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AcceptInviteResult,
  ActivityItem,
  CreateTeamRequest,
  InvitableUser,
  InviteLink,
  InvitePreview,
  JoinRequest,
  PagedResult,
  SlugAvailability,
  TeamDetail,
  TeamInvitation,
  TeamMember,
  TeamNews,
  TeamPublic,
  TeamPublicDetail,
  TeamRole,
} from '../models/team.models';

/**
 * Team API client. Internal calls carry the session cookie (auth interceptor);
 * the public team info and invite preview hit anonymous endpoints. Every list is
 * paginated. Server-side authorization is the real boundary — this is UX only.
 */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/teams';
  private readonly invites = '/api/v1/invitations';

  // --- Create & identity ---------------------------------------------------

  createTeam(request: CreateTeamRequest): Observable<TeamDetail> {
    return this.http.post<TeamDetail>(this.base, request);
  }

  checkSlug(slug: string): Observable<SlugAvailability> {
    const params = new HttpParams().set('slug', slug);
    return this.http.get<SlugAvailability>(`${this.base}/slug-available`, { params });
  }

  getDetail(slug: string): Observable<TeamDetail> {
    return this.http.get<TeamDetail>(`${this.base}/${encodeURIComponent(slug)}`);
  }

  getPublic(slug: string): Observable<TeamPublic> {
    return this.http.get<TeamPublic>(`${this.base}/${encodeURIComponent(slug)}/public`);
  }

  /** Feature 009 — the public team page (overview + viewer relation + roster/activity/trainings). */
  getPublicDetail(slug: string): Observable<TeamPublicDetail> {
    return this.http.get<TeamPublicDetail>(`${this.base}/${encodeURIComponent(slug)}/public`);
  }

  /** Feature 009 — a signed-in non-member asks to join. */
  requestToJoin(slug: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(slug)}/join-requests`, {});
  }

  /** Feature 009 — pending join requests (admin only). */
  getJoinRequests(slug: string, skip = 0, take = 50): Observable<PagedResult<JoinRequest>> {
    return this.http.get<PagedResult<JoinRequest>>(`${this.base}/${encodeURIComponent(slug)}/join-requests`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  approveJoinRequest(slug: string, requestId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(slug)}/join-requests/${requestId}/approve`, {});
  }

  declineJoinRequest(slug: string, requestId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(slug)}/join-requests/${requestId}/decline`, {});
  }

  deleteTeam(slug: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(slug)}`);
  }

  /** Feature 007 — admin-only: set the beginners-welcome recruitment flag. */
  updateSettings(slug: string, beginnersWelcome: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/${encodeURIComponent(slug)}`, { beginnersWelcome });
  }

  // --- Tabs ----------------------------------------------------------------

  getMembers(slug: string, skip = 0, take = 50): Observable<PagedResult<TeamMember>> {
    return this.http.get<PagedResult<TeamMember>>(`${this.base}/${encodeURIComponent(slug)}/members`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  getActivity(slug: string, skip = 0, take = 20): Observable<PagedResult<ActivityItem>> {
    return this.http.get<PagedResult<ActivityItem>>(`${this.base}/${encodeURIComponent(slug)}/activity`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  getNews(slug: string, skip = 0, take = 20): Observable<PagedResult<TeamNews>> {
    return this.http.get<PagedResult<TeamNews>>(`${this.base}/${encodeURIComponent(slug)}/news`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  // --- Members & roles -----------------------------------------------------

  setRole(slug: string, userId: string, role: TeamRole): Observable<TeamMember> {
    return this.http.patch<TeamMember>(`${this.base}/${encodeURIComponent(slug)}/members/${userId}/role`, { role });
  }

  removeMember(slug: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(slug)}/members/${userId}`);
  }

  stepDown(slug: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(slug)}/members/me/step-down`, {});
  }

  // --- Invitations (admin) -------------------------------------------------

  getInvitations(slug: string, skip = 0, take = 50): Observable<PagedResult<TeamInvitation>> {
    return this.http.get<PagedResult<TeamInvitation>>(`${this.base}/${encodeURIComponent(slug)}/invitations`, {
      params: new HttpParams().set('skip', skip).set('take', take),
    });
  }

  getInviteLink(slug: string): Observable<InviteLink | null> {
    return this.http.get<InviteLink | null>(`${this.base}/${encodeURIComponent(slug)}/invitations/link`);
  }

  rotateInviteLink(slug: string): Observable<InviteLink> {
    return this.http.post<InviteLink>(`${this.base}/${encodeURIComponent(slug)}/invitations/link`, {});
  }

  revokeInvite(slug: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(slug)}/invitations/${invitationId}`);
  }

  createTargetedInvite(slug: string, userId: string): Observable<TeamInvitation> {
    return this.http.post<TeamInvitation>(`${this.base}/${encodeURIComponent(slug)}/invitations`, { userId });
  }

  searchUsers(slug: string, q: string, skip = 0, take = 20): Observable<PagedResult<InvitableUser>> {
    return this.http.get<PagedResult<InvitableUser>>(`${this.base}/${encodeURIComponent(slug)}/invitations/user-search`, {
      params: new HttpParams().set('q', q).set('skip', skip).set('take', take),
    });
  }

  // --- Invitee token flow --------------------------------------------------

  getInvitePreview(token: string): Observable<InvitePreview> {
    return this.http.get<InvitePreview>(`${this.invites}/${encodeURIComponent(token)}`);
  }

  acceptInvite(token: string): Observable<AcceptInviteResult> {
    return this.http.post<AcceptInviteResult>(`${this.invites}/${encodeURIComponent(token)}/accept`, {});
  }

  declineInvite(token: string): Observable<void> {
    return this.http.post<void>(`${this.invites}/${encodeURIComponent(token)}/decline`, {});
  }
}
