import { defineConfig, devices } from '@playwright/test';
import { nxE2EPreset } from '@nx/playwright/preset';
import { workspaceRoot } from '@nx/devkit';

// Base URL of the app under test. Locally this is the Nx dev server; in the
// Docker test overlay (US4) BASE_URL points at the running frontend container,
// in which case we do NOT start a dev server.
const baseURL = process.env['BASE_URL'] || 'http://localhost:4200';
const usesExternalServer = Boolean(process.env['BASE_URL']);

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
