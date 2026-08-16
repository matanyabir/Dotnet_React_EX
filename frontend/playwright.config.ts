import { defineConfig, devices } from '@playwright/test';
import { API_PORT, API_URL, WEB_PORT, WEB_URL } from './e2e/support/ports';

/**
 * End-to-end configuration.
 *
 * These tests drive a real browser against the real API — no mocked network,
 * with one deliberate exception noted in `auth.spec.ts`. That is the point of
 * having them: the unit and integration suites already prove each side in
 * isolation, so the failures left are the ones that only appear when the two are
 * wired together, and a browser is the only thing that exercises the session
 * cookie the way a user does.
 *
 * Both servers are started by Playwright on ports of their own — 5199 and 5273
 * rather than the usual 5099 and 5173 — so a run cannot collide with, or quietly
 * borrow, a dev server someone already has open. That matters more than it
 * sounds: the frontend falls back to `localhost:5099` when told nothing, and a
 * suite that silently tested against a developer's own API, with their own data,
 * would pass or fail for reasons no one could reproduce.
 */

export default defineConfig({
  testDir: './e2e',

  // Tickets live in one JSON file behind one API, so tests share a dataset.
  // Running them one at a time means a list assertion cannot be broken by a
  // ticket another test filed a moment earlier. The suite is small; the
  // determinism is worth more than the seconds.
  fullyParallel: false,
  workers: 1,

  // A test that is the only one allowed to pass is not a test. Locally a `.only`
  // left behind is a nuisance; in CI it silently shrinks the suite.
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,

  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],

  // Roomier than the 30s default, because the first test of a run pays for
  // things none of the others do: Vite's first dependency optimisation, the
  // first React render, and a .NET process that has not JIT-compiled a request
  // path yet. That is a cold-start cost, not a slow test, and timing out on it
  // makes the suite fail on whichever test happens to be alphabetically first.
  timeout: 60_000,
  expect: { timeout: 10_000 },

  use: {
    baseURL: WEB_URL,

    // Kept for the first retry rather than always: traces are large, and the run
    // that failed is the only one worth opening.
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: [
    {
      // Seeds a temp dataset before handing over to `dotnet run` — see the
      // script for why that is not inlined here.
      command: 'node e2e/support/start-api.mjs',
      url: `${API_URL}/health`,

      // Never adopt a server this run did not start. An already-running API is
      // pointed at the tracked dataset and has whatever state a developer left
      // in it, which is exactly what the temp copy exists to avoid.
      reuseExistingServer: false,

      // A cold `dotnet run` compiles first.
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
      env: { E2E_API_PORT: String(API_PORT) },
    },
    {
      // --host 127.0.0.1 is not decoration. Vite's default binds `localhost`,
      // which resolves to ::1 here, and the app would then be served from a
      // different host than the API. Different hosts are a different *site* to
      // a browser, even on loopback, so SameSite=Lax would strip the session
      // cookie from every request the app makes and the suite would fail with
      // an admin who cannot stay signed in. Same host, different port — exactly
      // the arrangement `npm run dev` uses against the API on 5099.
      command: `npm run dev -- --host 127.0.0.1 --port ${WEB_PORT} --strictPort`,
      url: WEB_URL,
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',

      // Vite exposes prefixed variables from the environment, which is how the
      // app is told to talk to this run's API instead of its 5099 default.
      env: { VITE_API_BASE_URL: API_URL },
    },
  ],
});
