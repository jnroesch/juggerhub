import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ShowcaseImage,
  ShowcaseOwner,
  ShowcaseUploadFailure,
} from '../models/showcase.models';

/**
 * Showcase gallery API client (feature 046 / #99), serving both surfaces.
 *
 * Image bytes are never modeled here: the browser loads them straight from the URLs below,
 * carrying the session cookie exactly as it does for avatars. There is no blob fetching and
 * no `Authorization` header to plumb.
 *
 * **Nothing here retries.** Every write is a browser-hop mutation, and a timed-out upload may
 * already have been applied — retrying it would silently spend a slot in a five-picture
 * gallery (constitution Principle VII).
 */
@Injectable({ providedIn: 'root' })
export class ShowcaseService {
  private readonly http = inject(HttpClient);

  list(owner: ShowcaseOwner): Observable<ShowcaseImage[]> {
    return this.http.get<ShowcaseImage[]>(this.base(owner));
  }

  upload(owner: ShowcaseOwner, file: File): Observable<ShowcaseImage> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ShowcaseImage>(this.writeBase(owner), form);
  }

  setCaption(owner: ShowcaseOwner, imageId: string, caption: string | null): Observable<void> {
    return this.http.patch<void>(`${this.writeBase(owner)}/${imageId}`, { caption });
  }

  remove(owner: ShowcaseOwner, imageId: string): Observable<void> {
    return this.http.delete<void>(`${this.writeBase(owner)}/${imageId}`);
  }

  /** Submit the complete new order; the server refuses anything that is not a permutation. */
  reorder(owner: ShowcaseOwner, imageIds: readonly string[]): Observable<void> {
    return this.http.put<void>(`${this.writeBase(owner)}/order`, { imageIds });
  }

  /** The address the browser renders a picture from. */
  imageUrl(owner: ShowcaseOwner, imageId: string): string {
    return `${this.base(owner)}/${imageId}/image`;
  }

  /**
   * Classify an upload failure so the caller can say what actually went wrong rather than
   * "something failed" (spec FR-016). The server's own reason text is not shown: it is
   * written for the API, and the UI has translated copy for each case.
   */
  classifyUploadFailure(error: unknown): ShowcaseUploadFailure {
    if (!(error instanceof HttpErrorResponse)) {
      return 'unknown';
    }

    if (error.status === 409) {
      return 'full';
    }

    if (error.status === 503) {
      return 'unavailable';
    }

    // 413 comes from the request-size limit before the file ever reaches the processor.
    if (error.status === 413) {
      return 'size';
    }

    if (error.status === 400) {
      const detail = String(error.error?.detail ?? '').toLowerCase();
      if (detail.includes('large') || detail.includes('big') || detail.includes('dimension')) {
        return 'size';
      }
      if (detail.includes('type') || detail.includes('format')) {
        return 'type';
      }
      return 'unreadable';
    }

    return 'unknown';
  }

  /** Where a gallery is READ from — addressed by the owner everyone can name. */
  private base(owner: ShowcaseOwner): string {
    return owner.kind === 'profile'
      ? `/api/v1/profiles/${encodeURIComponent(owner.handle)}/showcase`
      : `/api/v1/teams/${encodeURIComponent(owner.slug)}/showcase`;
  }

  /**
   * Where a gallery is WRITTEN to. A profile's writes go through `me`, never through a
   * handle: the server acts on the authenticated subject alone, so "whose gallery" is not
   * a parameter a client can supply. A team's writes stay on the slug — there the actor and
   * the owner are genuinely different, and the server checks admin rights on the team.
   */
  private writeBase(owner: ShowcaseOwner): string {
    return owner.kind === 'profile'
      ? '/api/v1/profiles/me/showcase'
      : `/api/v1/teams/${encodeURIComponent(owner.slug)}/showcase`;
  }
}
