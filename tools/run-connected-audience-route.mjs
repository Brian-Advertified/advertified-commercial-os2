import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const id = process.argv[2]?.trim()
if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id ?? '')) {
  throw new Error('Provide the exact Brief version UUID to inspect.')
}
const regenerate = process.argv.slice(3).includes('--regenerate')
const env = {
  ...process.env,
  ADVERTIFIED_AUDIENCE_BRIEF_VERSION: id,
  ADVERTIFIED_AUDIENCE_REGENERATE: regenerate ? '1' : '0',
}
const result = spawnSync(process.execPath, [
  path.join(root, 'web/node_modules/@playwright/test/cli.js'), 'test',
  'e2e/audience-strategy-connected.connected.spec.ts',
  '--config=playwright.connected.config.ts', '--workers=1', '--retries=0',
], { cwd: path.join(root, 'web'), env, stdio: 'inherit', timeout: 120_000 })
if (result.error) throw result.error
process.exitCode = result.status ?? 1
