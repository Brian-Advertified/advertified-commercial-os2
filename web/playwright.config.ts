import { defineConfig } from '@playwright/test'

const configuredPort = Number(process.env.ADVERTIFIED_PLAYWRIGHT_PORT)
if (!Number.isInteger(configuredPort) || configuredPort < 1024 || configuredPort > 65535) {
  throw new Error('ADVERTIFIED_PLAYWRIGHT_PORT must be set by tools/run-playwright.mjs.')
}
const testOrigin = `http://127.0.0.1:${configuredPort}`

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
    command: `npm run dev -- --host 127.0.0.1 --port ${configuredPort} --strictPort`,
    url: `${testOrigin}/sign-in`,
    reuseExistingServer: process.env.PLAYWRIGHT_REUSE_SERVER === 'true',
  },
})
