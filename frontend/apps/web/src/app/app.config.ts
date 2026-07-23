import {
  ApplicationConfig,
  ErrorHandler,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';
import { appRoutes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { ChunkLoadErrorHandler } from './core/chunk-load-error.handler';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Self-heal a stale lazy-chunk after a frontend redeploy (reload once, then give up) so an open
    // tab isn't stranded on "Failed to fetch dynamically imported module".
    { provide: ErrorHandler, useClass: ChunkLoadErrorHandler },
    // withComponentInputBinding lets route params bind straight to component inputs — chat's
    // /chat/:conversationId feeds ChatConversationComponent's `conversationId` input this way,
    // so the open conversation is driven by the URL rather than a manual subscription.
    provideRouter(appRoutes, withComponentInputBinding()),
    // All API calls are relative ("/api/v1/...") and same-origin via the nginx
    // proxy, so httpOnly auth cookies stay first-party. The auth interceptor
    // attaches credentials and routes 401s toward sign-in.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
  ],
};
