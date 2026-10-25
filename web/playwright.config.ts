import os from 'node:os'
import path from 'node:path'
import { defineConfig, devices } from '@playwright/test'

const repoRoot = path.resolve(import.meta.dirname, '..')
const port = 5090
// A fresh database per run, so every run starts from the seeded demo data.
const database = path.join(os.tmpdir(), `lmstutor-e2e-${Date.now()}.db`)

/**
 * End-to-end smoke tests against the real server serving the production build of the SPA (same origin,
 * real CSP), with the demo providers and Chromium's fake microphone.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    locale: 'es-CO',
    trace: 'retain-on-failure',
    permissions: ['microphone'],
    launchOptions: {
      args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'],
    },
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'dotnet run --project ../src/LmsTutor.Server --no-launch-profile -c Release',
    url: `http://localhost:${port}/health`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: {
      ASPNETCORE_URLS: `http://localhost:${port}`,
      ASPNETCORE_ENVIRONMENT: 'Production',
      ASPNETCORE_WEBROOT: path.join(repoRoot, 'web', 'dist'),
      ConnectionStrings__Default: `Data Source=${database}`,
      Tutoring__Provider: 'Fake',
      Tutoring__FakeLatencyMilliseconds: '300',
      Speech__Provider: 'Fake',
      Speech__FakeLatencyMilliseconds: '300',
      RateLimiting__TutorRequestsPerMinute: '100',
      RateLimiting__LoginAttemptsPerMinute: '100',
    },
  },
})
