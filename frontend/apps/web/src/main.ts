import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { registerServiceWorker } from './app/core/pwa/register-service-worker';

// The push-only service worker (feature 054) is registered only after Angular is up — not as an
// app initializer (those run BEFORE bootstrap completes) and not in a component (the worker
// belongs to the origin, not to a view). The helper itself waits for `load` and swallows failure.
bootstrapApplication(App, appConfig)
  .then(() => registerServiceWorker())
  .catch((err) => console.error(err));
