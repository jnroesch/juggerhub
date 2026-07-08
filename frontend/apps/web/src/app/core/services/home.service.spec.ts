import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HomeService } from './home.service';

describe('HomeService', () => {
  let service: HomeService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(HomeService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getHome GETs /home', () => {
    service.getHome().subscribe();
    const req = httpMock.expectOne('/api/v1/home');
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('getUpNext passes skip/take', () => {
    service.getUpNext(20, 10).subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/home/up-next' && r.params.get('skip') === '20' && r.params.get('take') === '10',
    );
    req.flush({ items: [], totalCount: 0, skip: 20, take: 10 });
  });

  it('getNews GETs /home/news', () => {
    service.getNews().subscribe();
    const req = httpMock.expectOne((r) => r.url === '/api/v1/home/news');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });
});
