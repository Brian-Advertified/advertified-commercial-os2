import { defineConfig } from '@playwright/test'

const testPort = 43917
const testOrigin = `http://127.0.0.1:${testPort}`

export default defineConfig({
  testDir: './e2e',
  testIgnore: '**/*.connected.spec.ts',
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: testOrigin,
    reducedMotion: 'reduce',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1280, height: 800 } } },
    { name: 'compact', use: { viewport: { width: 390, height: 844 } } },
  ],
  webServer: {
    command: `npm run dev -- --host 127.0.0.1 --port ${testPort} --strictPort`,
    url: `${testOrigin}/sign-in`,
    reuseExistingServer: process.env.PLAYWRIGHT_REUSE_SERVER === 'true',
  },
})
