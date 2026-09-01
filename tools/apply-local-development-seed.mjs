import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const containerName = 'advertified-dev-postgres-1'
const repositoryRoot = fileURLToPath(new URL('../', import.meta.url))
const seedPath = fileURLToPath(
  new URL('../infrastructure/development/seed-local-workspace.sql', import.meta.url),
)

const inspect = spawnSync('docker', ['inspect', containerName], {
  cwd: repositoryRoot,
  encoding: 'utf8',
  maxBuffer: 1024 * 1024,
  shell: false,
})
if (inspect.status !== 0) {
  fail('The exact Advertified development PostgreSQL container is not available.', inspect)
}

const record = JSON.parse(inspect.stdout)[0]
const labels = record?.Config?.Labels ?? {}
const unsafeMount = (record?.Mounts ?? []).some((mount) =>
  String(mount.Source).toLowerCase().includes('docker.sock') ||
  String(mount.Destination).toLowerCase().includes('docker.sock'))
const allowed =
  record?.Name === `/${containerName}` &&
  record?.State?.Running === true &&
  labels['com.docker.compose.project'] === 'advertified-dev' &&
  labels['com.docker.compose.service'] === 'postgres' &&
  record?.HostConfig?.Privileged !== true &&
  record?.HostConfig?.NetworkMode !== 'host' &&
  !unsafeMount

if (!allowed) {
  throw new Error(
    'Refusing to seed a container outside the exact non-production Advertified database.',
  )
}

const sql = readFileSync(seedPath, 'utf8')
const apply = spawnSync('docker', [
  'exec',
  '--user', 'postgres',
  '--interactive',
  containerName,
  'psql',
  '--set', 'ON_ERROR_STOP=1',
  '--username', 'advertified',
  '--dbname', 'advertified',
], {
  cwd: repositoryRoot,
  encoding: 'utf8',
  input: sql,
  maxBuffer: 4 * 1024 * 1024,
  shell: false,
})
if (apply.status !== 0) {
  fail('The local development seed failed safely.', apply)
}

process.stdout.write(
  'Applied the idempotent local development workspace and proposal prerequisites.\n',
)

function fail(message, result) {
  const detail = String(result.stderr || result.stdout || '').trim()
  throw new Error(detail ? `${message} ${detail}` : message)
}
