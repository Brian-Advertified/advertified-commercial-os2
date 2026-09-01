import { spawn } from 'node:child_process'
import { writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const output = resolve(root, 'shared/contracts/openapi/advertified-commercial-api.v1.json')
const assembly = resolve(root, 'api/bin/Release/net10.0/Advertified.Commercial.Api.dll')
const connectedUrl = readConnectedUrl(process.argv.slice(2))

if (connectedUrl) {
  await generateFromConnectedApi(connectedUrl)
} else {
  generateFromAssembly()
}

async function generateFromConnectedApi(baseUrl) {
  const endpoint = new URL('/swagger/v1/swagger.json', ensureTrailingSlash(baseUrl))
  const response = await fetch(endpoint, { headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error(`OpenAPI endpoint returned HTTP ${response.status}.`)
  }
  const contract = await response.json()
  await writeFile(output, `${JSON.stringify(contract, null, 2)}\n`, 'utf8')
  console.log(`Generated OpenAPI from ${endpoint.origin}.`)
}

function generateFromAssembly() {
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ConnectionStrings__CommercialDatabase:
      'Host=localhost;Database=openapi-contract;Username=openapi-contract',
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

  child.on('exit', code => process.exit(code ?? 1))
  child.on('error', error => {
    console.error(error)
    process.exit(1)
  })
}

function readConnectedUrl(args) {
  if (args.length === 0) return null
  if (args.length !== 2 || args[0] !== '--url') {
    throw new Error('Use --url <connected-api-origin> or omit arguments.')
  }
  const value = new URL(args[1])
  if (!['http:', 'https:'].includes(value.protocol)) {
    throw new Error('The connected API URL must use HTTP or HTTPS.')
  }
  return value
}

function ensureTrailingSlash(value) {
  return value.href.endsWith('/') ? value.href : `${value.href}/`
}
