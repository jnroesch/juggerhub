import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Pompfe } from '../../shared/pompfen.catalog';
import { PagedResult } from '../models/profile.models';
import {
  ApplyRequest,
  InviteRequest,
  MarketInvitableUser,
  MarketListing,
  MarketListingCard,
  MarketRequest,
  MyListing,
  MyMarket,
  MyMarketRequest,
  PostListingRequest,
  RecruitingPartyCard,
  RecruitingSettings,
  SetRecruitingRequest,
} from '../models/market.models';

/**
 * Event marketplace API client (feature 017). The board is public; every write and inbox read is
 * authenticated and re-checked server-side (this is UX only). Every list is paginated.
 */
@Injectable({ providedIn: 'root' })
export class MarketService {
  private readonly http = inject(HttpClient);
  private readonly api = '/api/v1';

  private page(skip?: number, take?: number, extra?: Record<string, string>): HttpParams {
    let params = new HttpParams();
    if (skip != null) params = params.set('skip', skip);
    if (take != null) params = params.set('take', take);
    for (const [k, v] of Object.entries(extra ?? {})) params = params.set(k, v);
    return params;
  }

  private events(eventId: string): string {
    return `${this.api}/events/${encodeURIComponent(eventId)}/market`;
  }

  private party(partyId: string): string {
    return `${this.api}/parties/${encodeURIComponent(partyId)}`;
  }

  // --- Board (public) -------------------------------------------------------

  freeAgents(eventId: string, position?: Pompfe | null, skip?: number, take?: number): Observable<PagedResult<MarketListingCard>> {
    const params = this.page(skip, take, position ? { position } : undefined);
    return this.http.get<PagedResult<MarketListingCard>>(`${this.events(eventId)}/free-agents`, { params });
  }

  recruitingParties(eventId: string, position?: Pompfe | null, skip?: number, take?: number): Observable<PagedResult<RecruitingPartyCard>> {
    const params = this.page(skip, take, position ? { position } : undefined);
    return this.http.get<PagedResult<RecruitingPartyCard>>(`${this.events(eventId)}/parties`, { params });
  }

  // --- Caller's context + listing -------------------------------------------

  myMarket(eventId: string): Observable<MyMarket> {
    return this.http.get<MyMarket>(`${this.events(eventId)}/me`);
  }

  postListing(eventId: string, request: PostListingRequest): Observable<MarketListing> {
    return this.http.post<MarketListing>(`${this.events(eventId)}/listing`, request);
  }

  editListing(eventId: string, request: PostListingRequest): Observable<MarketListing> {
    return this.http.put<MarketListing>(`${this.events(eventId)}/listing`, request);
  }

  takeDownListing(eventId: string): Observable<void> {
    return this.http.delete<void>(`${this.events(eventId)}/listing`);
  }

  // --- Recruiting -----------------------------------------------------------

  getRecruiting(partyId: string): Observable<RecruitingSettings> {
    return this.http.get<RecruitingSettings>(`${this.party(partyId)}/recruiting`);
  }

  setRecruiting(partyId: string, request: SetRecruitingRequest): Observable<RecruitingSettings> {
    return this.http.put<RecruitingSettings>(`${this.party(partyId)}/recruiting`, request);
  }

  // --- Handshake ------------------------------------------------------------

  apply(partyId: string, request: ApplyRequest): Observable<MarketRequest> {
    return this.http.post<MarketRequest>(`${this.party(partyId)}/market/applications`, request);
  }

  invite(partyId: string, request: InviteRequest): Observable<MarketRequest> {
    return this.http.post<MarketRequest>(`${this.party(partyId)}/market/invites`, request);
  }

  listApplications(partyId: string, skip?: number, take?: number): Observable<PagedResult<MarketRequest>> {
    return this.http.get<PagedResult<MarketRequest>>(`${this.party(partyId)}/market/applications`, { params: this.page(skip, take) });
  }

  listSentInvites(partyId: string, skip?: number, take?: number): Observable<PagedResult<MarketRequest>> {
    return this.http.get<PagedResult<MarketRequest>>(`${this.party(partyId)}/market/invites`, { params: this.page(skip, take) });
  }

  searchUsers(partyId: string, query: string, skip?: number, take?: number): Observable<PagedResult<MarketInvitableUser>> {
    return this.http.get<PagedResult<MarketInvitableUser>>(`${this.party(partyId)}/market/user-search`, { params: this.page(skip, take, { query }) });
  }

  // --- Request actions ------------------------------------------------------

  accept(requestId: string): Observable<MarketRequest> {
    return this.http.post<MarketRequest>(`${this.api}/market/requests/${encodeURIComponent(requestId)}/accept`, {});
  }

  declineRequest(requestId: string): Observable<MarketRequest> {
    return this.http.post<MarketRequest>(`${this.api}/market/requests/${encodeURIComponent(requestId)}/decline`, {});
  }

  revoke(requestId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/market/requests/${encodeURIComponent(requestId)}/revoke`, {});
  }

  // --- Dashboard ------------------------------------------------------------

  mine(skip?: number, take?: number): Observable<PagedResult<MyMarketRequest>> {
    return this.http.get<PagedResult<MyMarketRequest>>(`${this.api}/market/mine`, { params: this.page(skip, take) });
  }

  myListings(skip?: number, take?: number): Observable<PagedResult<MyListing>> {
    return this.http.get<PagedResult<MyListing>>(`${this.api}/market/mine/listings`, { params: this.page(skip, take) });
  }
}
