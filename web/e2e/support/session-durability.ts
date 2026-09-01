import { mkdir, readFile, rm } from 'node:fs/promises'
import path from 'node:path'
import type { BrowserContext } from '@playwright/test'

export const durableSessionStatePath = path.join(
  process.cwd(),
  'tmp',
  'durable-browser-session.json',
)

export async function prepareDurableSessionStatePath() {
  await mkdir(path.dirname(durableSessionStatePath), { recursive: true })
}

export async function restoreDurableSessionCookies(context: BrowserContext) {
  const state = JSON.parse(
    await readFile(durableSessionStatePath, 'utf8'),
  ) as { cookies: Parameters<BrowserContext['addCookies']>[0] }
  await context.addCookies(state.cookies)
}

export async function removeDurableSessionState() {
  await rm(durableSessionStatePath, { force: true })
}
