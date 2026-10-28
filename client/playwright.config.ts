import { defineConfig, devices } from '@playwright/test'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

// Dedicated ports, so the tests don't use (or collide with) a running dev setup on 5080 / 5180.
const API_PORT = 5091
const UI_PORT = 5191
// The server loads saved diagrams on startup; an empty directory gives every run a clean state.
const storage = mkdtempSync(join(tmpdir(), 'kandinsky-e2e-'))

export default defineConfig({
  testDir: './e2e',
  // All tests share one server and the tabs show every diagram on it, so they run one after another.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: `http://localhost:${UI_PORT}`,
    viewport: { width: 1400, height: 900 },
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 900 } } }],
  webServer: [
    {
      command: `dotnet run --project ../server/Kandinsky.Server --no-launch-profile --urls http://localhost:${API_PORT}`,
      url: `http://localhost:${API_PORT}/api/diagrams`,
      env: { Storage__Path: storage, ASPNETCORE_ENVIRONMENT: 'Development' },
      reuseExistingServer: false,
      timeout: 180_000,
    },
    {
      command: `npx vite --port ${UI_PORT} --strictPort`,
      url: `http://localhost:${UI_PORT}`,
      env: { KANDINSKY_API_URL: `http://localhost:${API_PORT}` },
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
})
