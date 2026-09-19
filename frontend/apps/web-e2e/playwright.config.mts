import { defineConfig, devices } from '@playwright/test';
import { nxE2EPreset } from '@nx/playwright/preset';
import { workspaceRoot } from '@nx/devkit';

// Base URL of the app under test. Locally this is the Nx dev server; in the
// Docker test overlay (US4) BASE_URL points at the running frontend container,
// in which case we do NOT start a dev server.
const baseURL = process.env['BASE_URL'] || 'http://localhost:4200';
const usesExternalServer = Boolean(process.env['BASE_URL']);

// Service workers exist only in a secure context: HTTPS, or plain HTTP on localhost. The Docker
// test overlay reaches the frontend as http://frontend:8080, which is neither, so
// `navigator.serviceWorker` is simply undefined there and the PWA shell's registration test
// (feature 054) cannot run. Chromium's flag below makes it treat exactly that one origin as secure
// — nothing else changes, and it is not added for localhost, where it is unnecessary.
//
// The flag is honoured by full Chromium (new headless, `channel: 'chromium'`, bundled in the
// Playwright image) but NOT by the default headless shell — verified: the shell reports
// isSecureContext=false with the flag set, full Chromium reports true. So the channel switches
// together with the flag, and only then.
const insecureNonLocalOrigin = /^http:\/\/(?!localhost(?::|\/|$)|127\.0\.0\.1)/.test(baseURL);
const secureContextShim = insecureNonLocalOrigin
  ? {
      channel: 'chromium' as const,
      launchOptions: { args: [`--unsafely-treat-insecure-origin-as-secure=${new URL(baseURL).origin}`] },
    }
  : {};

/**
 * Generated as a .mts file so Node forces ESM regardless of workspace `type`.
 *
 * Responsive usability is a hard gate (FR-025/FR-026, SC-009): every e2e runs at
 * a representative desktop AND a representative mobile viewport.
 */
export default defineConfig({
  ...nxE2EPreset(import.meta.dirname, { testDir: './src' }),
  /* Shared settings for all the projects below. */
  use: {
    baseURL,
    /* Collect trace when retrying the failed test. */
    trace: 'on-first-retry',
    ...secureContextShim,
  },
  /* Run the local dev server before tests — but only when targeting localhost.
     In the Docker overlay BASE_URL points at the running frontend container, so
     no dev server is started. */
  webServer: usesExternalServer
    ? undefined
    : {
        command: 'npx nx run web:serve',
        url: 'http://localhost:4200',
        reuseExistingServer: true,
        cwd: workspaceRoot,
      },
  projects: [
    {
      name: 'desktop-chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
    },
  ],
});
