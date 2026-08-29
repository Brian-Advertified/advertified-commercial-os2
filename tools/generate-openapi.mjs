import { spawn } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const output = resolve(root, 'shared/contracts/openapi/advertified-commercial-api.v1.json')
const assembly = resolve(root, 'api/bin/Release/net10.0/Advertified.Commercial.Api.dll')
const env = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Development',
  ConnectionStrings__CommercialDatabase: 'Host=localhost;Database=openapi-contract;Username=openapi-contract',
  InventoryProtection__ObjectStoreMode: 'InMemory',
  InventoryProtection__ScannerMode: 'Deterministic',
  AgentRuntime__Mode: 'Disabled',
}

const child = spawn('dotnet', [
  'tool', 'run', 'swagger', 'tofile',
  '--output', output,
  assembly,
  'v1',
], { cwd: root, env, stdio: 'inherit', shell: false })

child.on('exit', (code) => process.exit(code ?? 1))
child.on('error', (error) => {
  console.error(error)
  process.exit(1)
})
