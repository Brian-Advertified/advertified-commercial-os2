// One canonical browser journey at a time. A connector timeout is not permission to repeat it.
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import fs from 'node:fs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const args = process.argv.slice(2)
const env = { ...process.env }
delete env.ADVERTIFIED_CANARY_RESUME_BRIEF_VERSION
delete env.ADVERTIFIED_CANARY_INSPECT_BRIEF_VERSION
let mode = 'fresh'
for (let index = 0; index < args.length; index += 1) {
  if (args[index] === '--workers=1') continue
  if (!['--resume-brief-version', '--inspect-brief-version'].includes(args[index]) || mode !== 'fresh') {
    throw new Error('Select at most one exact saved Brief version to inspect or resume.')
  }
  mode = args[index] === '--inspect-brief-version' ? 'inspect' : 'resume'
  const id = args[++index]
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id ?? '')) {
    throw new Error('Provide the exact saved Brief version UUID.')
  }
  env[mode === 'inspect' ? 'ADVERTIFIED_CANARY_INSPECT_BRIEF_VERSION' : 'ADVERTIFIED_CANARY_RESUME_BRIEF_VERSION'] = id
}
const evidence = path.join(root, 'artifacts/production-readiness/preview')
fs.mkdirSync(evidence, { recursive: true })
const lock = path.join(evidence, 'inventory-canary.lock.json')
// Retain the lock on interruption; inspect the exact PID before any retry.
const handle = fs.openSync(lock, 'wx')
fs.writeFileSync(handle, JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString(), mode }))
fs.closeSync(handle)
let status = 1
try {
  console.log(`Connected canary mode: ${mode}. Only fresh mode starts from a new Brief.`)
  const result = spawnSync(process.execPath, [
    path.join(root, 'web/node_modules/@playwright/test/cli.js'), 'test',
    'e2e/inventory-brief-to-proposal.connected.spec.ts', '--config=playwright.connected.config.ts',
    '--workers=1', '--retries=0',
  ], { cwd: path.join(root, 'web'), env, stdio: 'inherit', timeout: 210_000 })
  if (result.error) throw result.error
  status = result.status ?? 1
  const receipt = { finishedAt: new Date().toISOString(), exitCode: status, mode,
    resumedBriefVersion: env.ADVERTIFIED_CANARY_RESUME_BRIEF_VERSION ?? null,
    inspectedBriefVersion: env.ADVERTIFIED_CANARY_INSPECT_BRIEF_VERSION ?? null }
  const suffix = receipt.finishedAt.replace(/[:.]/g, '-')
  fs.writeFileSync(path.join(evidence, `inventory-canary-${suffix}.json`), JSON.stringify(receipt, null, 2))
  if (mode !== 'inspect') fs.writeFileSync(path.join(evidence, 'inventory-canary-run.json'), JSON.stringify(receipt, null, 2))
  fs.unlinkSync(lock)
} finally {
  process.exitCode = status
}
