import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PartyService } from './party.service';

/**
 * Feature 059 — editing and deleting party news. Neither call is retried by the browser (the
 * retry interceptor only repeats GET/HEAD); these pin the address and the verb each one uses.
 */
describe('PartyService news', () => {
  let service: PartyService;
  let httpMock: HttpTestingController;
  const id = '0199a7c2-0000-7000-8000-00000000e001';
  const postId = '0199a7c2-3d41-7b10-9e2f-5c7d1a0b4e21';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    service = TestBed.inject(PartyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('PATCHes the new text to the post', () => {
    service.editNews(id, postId, 'Meet at 09:00.').subscribe();

    const req = httpMock.expectOne(`/api/v1/parties/${id}/news/${postId}`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ body: 'Meet at 09:00.' });
    req.flush({});
  });

  it('DELETEs the post', () => {
    service.deleteNews(id, postId).subscribe();

    const req = httpMock.expectOne(`/api/v1/parties/${id}/news/${postId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
