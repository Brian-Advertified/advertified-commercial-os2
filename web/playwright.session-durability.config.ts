import { defineConfig } from '@playwright/test'

const connectedOrigin = process.env.ADVERTIFIED_CONNECTED_ORIGIN ?? 'http://localhost:3017'

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/session-durability.*.spec.ts',
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: connectedOrigin,
    reducedMotion: 'reduce',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'session-durability', use: { viewport: { width: 1280, height: 800 } } },
  ],
})
