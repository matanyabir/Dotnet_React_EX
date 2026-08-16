/**
 * Boots the API for an end-to-end run, against a throwaway copy of the dataset.
 *
 * The API persists to a JSON file, and in development that file is
 * `backend/MX.Api/Data/dataset.json` — which git tracks. A suite that files and
 * edits tickets would otherwise leave its rubbish in someone's `git diff`, so
 * this seeds a temp copy from the pristine `dataset.json` at the repo root, the
 * same seed the .NET integration tests use, and points the API at it. Uploads go
 * to a temp directory for the same reason.
 *
 * Playwright starts this once per run, via `webServer` in playwright.config.ts.
 * It is a script rather than a shell one-liner in that config because the config
 * module is re-evaluated in every worker process, and re-seeding the dataset
 * mid-run would delete the tickets the running tests just filed.
 */

import { spawn } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = fileURLToPath(new URL('.', import.meta.url));
const repoRoot = resolve(here, '..', '..', '..');

const port = process.env.E2E_API_PORT ?? '5199';

const workspace = mkdtempSync(join(tmpdir(), 'mx-e2e-'));
const dataFile = join(workspace, 'dataset.json');
const uploads = join(workspace, 'uploads');

copyFileSync(join(repoRoot, 'dataset.json'), dataFile);
mkdirSync(uploads, { recursive: true });

// The SDK is not always the one on PATH — Homebrew's is too old for .slnx here —
// so prefer the user-local install when it exists.
const localDotnet = join(process.env.HOME ?? '', '.dotnet', 'dotnet');
const dotnet = existsSync(localDotnet) ? localDotnet : 'dotnet';

console.log(`[e2e] dataset  ${dataFile}`);
console.log(`[e2e] uploads  ${uploads}`);
console.log(`[e2e] api      http://127.0.0.1:${port}`);

const api = spawn(
  dotnet,
  [
    'run',
    '--project',
    join(repoRoot, 'backend', 'MX.Api'),

    // launchSettings.json pins port 5099 and would fight the port chosen here.
    // Skipping it also drops the environment it sets, so that is named below.
    '--no-launch-profile',
    '--urls',
    `http://127.0.0.1:${port}`,
  ],
  {
    stdio: 'inherit',
    env: {
      ...process.env,

      // Development is where the committed signing key and the admin account
      // live; without it the host fails to start on the missing key.
      ASPNETCORE_ENVIRONMENT: 'Development',

      Storage__DataFilePath: dataFile,
      Storage__UploadsDirectory: uploads,

      // Comfortably above what the suite spends signing in — every test shares
      // one bucket, because the limit counts by client address — but low enough
      // that zz-login-rate-limit.spec.ts can exhaust it in a second or two. The
      // window is stretched so it cannot quietly reset mid-run and leave that
      // spec chasing a limit that keeps refilling.
      Auth__LoginRateLimit__PermitLimit: '25',
      Auth__LoginRateLimit__WindowSeconds: '3600',

      // No network and no API key: summaries are generated locally and mail is
      // recorded in memory. Both are already the defaults; stating them keeps a
      // developer's own appsettings from making the run depend on a live service.
      Ai__Provider: 'Stub',
      Email__Provider: 'Mock',
    },
  },
);

// Playwright stops the web server by signalling this process. Without passing
// that on, `dotnet run` and the app beneath it would outlive the run and hold
// the port against the next one.
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => api.kill(signal));
}

api.on('exit', (code, signal) => process.exit(signal ? 1 : (code ?? 0)));
