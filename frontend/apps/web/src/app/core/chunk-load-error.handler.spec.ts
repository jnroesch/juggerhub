import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ChunkLoadErrorHandler } from './chunk-load-error.handler';

describe('ChunkLoadErrorHandler', () => {
  function make(): { handler: ChunkLoadErrorHandler; reload: jest.SpyInstance } {
    TestBed.configureTestingModule({ providers: [ChunkLoadErrorHandler, provideRouter([])] });
    const handler = TestBed.inject(ChunkLoadErrorHandler);
    // reloadPage is a protected seam over window.location.reload (which can't run under jsdom).
    const reload = jest.spyOn(handler as unknown as { reloadPage: () => void }, 'reloadPage').mockImplementation(() => undefined);
    return { handler, reload };
  }

  beforeEach(() => sessionStorage.clear());

  it('reloads once on a failed dynamic import (stale chunk)', () => {
    const { handler, reload } = make();
    handler.handleError(new Error('Failed to fetch dynamically imported module: http://x/chunk-ABC.js'));
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('does not reload a second time — a broken deploy must not loop', () => {
    const { handler, reload } = make();
    handler.handleError(new Error('Failed to fetch dynamically imported module'));
    handler.handleError(new Error('Failed to fetch dynamically imported module'));
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('leaves unrelated errors alone (logs, never reloads)', () => {
    const { handler, reload } = make();
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    handler.handleError(new TypeError('cannot read properties of undefined'));
    expect(reload).not.toHaveBeenCalled();
    expect(spy).toHaveBeenCalled();
    spy.mockRestore();
  });

  it('recognises the ChunkLoadError variant too', () => {
    const { handler, reload } = make();
    handler.handleError(new Error('ChunkLoadError: Loading chunk 42 failed'));
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
