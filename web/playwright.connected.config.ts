import { defineConfig } from '@playwright/test'

const connectedOrigin = process.env.ADVERTIFIED_CONNECTED_ORIGIN ?? 'http://localhost:3017'

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.connected.spec.ts',
  fullyParallel: false,
  workers: 1,
  forbidOnly: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: connectedOrigin,
    reducedMotion: 'reduce',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'connected-desktop', use: { viewport: { width: 1280, height: 800 } } },
  ],
})
