import { readFile, realpath, writeFile } from 'node:fs/promises'
import { dirname, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const output = resolve(root, 'shared/contracts/openapi/advertified-commercial-api.v1.json')
const args = process.argv.slice(2)
if (args.length !== 2 || !['--url', '--input'].includes(args[0])) {
  throw new Error('Use --url <approved-local-api-origin> or --input <retained-pinned-test-export>.')
}
const contract = args[0] === '--url' ? await readConnectedApi(args[1]) : await readTestExport(args[1])
if (!contract || typeof contract !== 'object' || !String(contract.openapi ?? '').startsWith('3.') ||
    !contract.paths?.['/api/v1/session'] || !contract.components?.schemas) {
  throw new Error('The source is not the Advertified OpenAPI contract; no file was changed.')
}
await writeFile(output, `${JSON.stringify(contract, null, 2)}\n`, 'utf8')
console.log('Generated the retained OpenAPI contract. Rerun the live-contract comparison test to verify it.')

async function readConnectedApi(value) {
  const url = new URL(value)
  if (!['http:', 'https:'].includes(url.protocol) ||
      !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) ||
      url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    throw new Error('The API URL must be a local HTTP(S) origin without credentials, path, query or fragment.')
  }
  const response = await fetch(new URL('/swagger/v1/swagger.json', url), {
    headers: { Accept: 'application/json' }, redirect: 'error', signal: AbortSignal.timeout(30_000),
  })
  if (!response.ok) throw new Error(`OpenAPI endpoint returned HTTP ${response.status}.`)
  return response.json()
}

async function readTestExport(value) {
  const allowedRoot = await realpath(resolve(root, 'artifacts/backend-production-completion'))
  const source = await realpath(resolve(root, value))
  if (!source.startsWith(allowedRoot + sep) || !source.endsWith(sep + 'openapi.actual.json')) {
    throw new Error('Use the actual OpenAPI export retained by the pinned API test runner.')
  }
  const run = JSON.parse((await readFile(resolve(dirname(source), 'run.json'), 'utf8')).replace(/^\uFEFF/, ''))
  if (!String(run.sdkImage).includes('/dotnet/sdk:10.0.400-') || !String(run.sdkImage).includes('@sha256:')) {
    throw new Error('The export does not have a retained Docker-pinned SDK run receipt.')
  }
  return JSON.parse(await readFile(source, 'utf8'))
}
