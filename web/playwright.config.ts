import { defineConfig } from '@playwright/test'

const testPort = 43918
const testOrigin = `http://127.0.0.1:${testPort}`

export default defineConfig({
  testDir: './e2e',
  testIgnore: [
    '**/*.connected.spec.ts',
    '**/session-durability.*.spec.ts',
  ],
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: testOrigin,
    reducedMotion: 'reduce',
    trace: 'off',
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
