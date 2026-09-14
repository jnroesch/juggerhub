import { HttpErrorResponse } from '@angular/common/http';

import { problemDetail } from './problem';

const FALLBACK = 'Dieses Bild konnte nicht gespeichert werden.';

describe('problemDetail', () => {
  it('returns the detail of a ProblemDetails body', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'Invalid icon', detail: 'Use a PNG, JPEG, or WebP image.' },
    });

    expect(problemDetail(error, FALLBACK)).toBe('Use a PNG, JPEG, or WebP image.');
  });

  it('falls back when a gateway answers with an HTML error page', () => {
    // Angular cannot parse the page as JSON, so `error` is the raw document (#293). Rendering it
    // put a whole 502 page inside the badge-icon dialog.
    const error = new HttpErrorResponse({
      status: 502,
      error:
        '<html> <head><title>502 Bad Gateway</title></head> <body> <center><h1>502 Bad Gateway</h1>' +
        '</center> <hr><center>cloudflare</center> </body> </html>',
    });

    expect(problemDetail(error, FALLBACK)).toBe(FALLBACK);
  });

  it('falls back for a body carrying no usable detail', () => {
    expect(problemDetail(new HttpErrorResponse({ status: 413, error: null }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(new HttpErrorResponse({ status: 500, error: {} }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(new HttpErrorResponse({ status: 500, error: { detail: '   ' } }), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(new HttpErrorResponse({ status: 500, error: { detail: 42 } }), FALLBACK)).toBe(FALLBACK);
  });

  it('falls back for anything that is not an HTTP error', () => {
    expect(problemDetail(new Error('boom'), FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(undefined, FALLBACK)).toBe(FALLBACK);
  });
});
