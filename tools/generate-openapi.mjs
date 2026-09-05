import { writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const output = resolve(root, 'shared/contracts/openapi/advertified-commercial-api.v1.json')
const connectedUrl = readConnectedUrl(process.argv.slice(2))
await generateFromConnectedApi(connectedUrl)

async function generateFromConnectedApi(baseUrl) {
  const endpoint = new URL('/swagger/v1/swagger.json', ensureTrailingSlash(baseUrl))
  const response = await fetch(endpoint, {
    headers: { Accept: 'application/json' }, redirect: 'error', signal: AbortSignal.timeout(30_000),
  })
  if (!response.ok) {
    throw new Error(`OpenAPI endpoint returned HTTP ${response.status}.`)
  }
  const contract = await response.json()
  await writeFile(output, `${JSON.stringify(contract, null, 2)}\n`, 'utf8')
  console.log(`Generated OpenAPI from ${endpoint.origin}.`)
}

function readConnectedUrl(args) {
  if (args.length !== 2 || args[0] !== '--url') {
    throw new Error('Use --url <approved-running-local-api-origin>. Build through the Docker-pinned path first.')
  }
  const value = new URL(args[1])
  if (!['http:', 'https:'].includes(value.protocol) ||
      !['localhost', '127.0.0.1', '[::1]'].includes(value.hostname) ||
      value.username || value.password || value.pathname !== '/' || value.search || value.hash) {
    throw new Error('The connected API URL must be a local HTTP(S) origin without credentials, path, query or fragment.')
  }
  return value
}

function ensureTrailingSlash(value) {
  return value.href.endsWith('/') ? value.href : `${value.href}/`
}
