import { defineConfig } from '@playwright/test'

const origin = 'http://127.0.0.1:3017'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: origin,
    reducedMotion: 'reduce',
    trace: 'off',
    viewport: { width: 390, height: 844 },
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 3017 --strictPort',
    url: `${origin}/sign-in`,
    reuseExistingServer: true,
  },
})
