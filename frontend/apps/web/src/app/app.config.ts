import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { appRoutes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(appRoutes),
    // All API calls are relative ("/api/v1/...") and same-origin via the nginx
    // proxy, so httpOnly auth cookies stay first-party. The auth interceptor is
    // added by the security-boundary story (US2).
    provideHttpClient(withFetch()),
  ],
};
