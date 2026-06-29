import { defineConfig, devices } from '@playwright/test';
import { nxE2EPreset } from '@nx/playwright/preset';
import { workspaceRoot } from '@nx/devkit';

// Base URL of the app under test. Locally this is the Nx dev server; in the
// Docker test overlay (US4) it is overridden to the running frontend container.
const baseURL = process.env['BASE_URL'] || 'http://localhost:4200';

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
  /* Run the local dev server before tests. When BASE_URL points at an already
     running stack (the Docker overlay), reuseExistingServer short-circuits this. */
  webServer: {
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
