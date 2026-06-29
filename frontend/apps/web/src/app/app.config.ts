import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';
import { appRoutes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(appRoutes),
    // All API calls are relative ("/api/v1/...") and same-origin via the nginx
    // proxy, so httpOnly auth cookies stay first-party. The auth interceptor
    // attaches credentials and routes 401s toward sign-in.
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
  ],
};
