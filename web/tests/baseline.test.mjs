import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const signInSource = await readFile(new URL('../src/pages/SignInPage.tsx', import.meta.url), 'utf8')
const deferredSource = await readFile(new URL('../src/pages/DeferredPage.tsx', import.meta.url), 'utf8')
const clientSource = await readFile(new URL('../src/api/client.ts', import.meta.url), 'utf8')
const productionCopySources = await Promise.all([
  '../src/content/operational-copy.ts',
  '../src/pages/AgentOperationsPage.tsx',
  '../src/pages/SignInPage.tsx',
  '../src/pages/ProfilePage.tsx',
  '../src/components/ProfileForm.tsx',
  '../src/pages/InventoryImportPage.tsx',
].map(path => readFile(new URL(path, import.meta.url), 'utf8')))
const packageSource = JSON.parse(
  await readFile(new URL('../package.json', import.meta.url), 'utf8'),
)
const connectedStyles = await Promise.all([
  '../src/connected-system.css',
  '../src/connected-home.css',
  '../src/connected-reporting.css',
  '../src/connected-directories.css',
  '../src/connected-interactions.css',
].map(path => readFile(new URL(path, import.meta.url), 'utf8')))
const appShellSource = await readFile(new URL('../src/components/AppShell.tsx', import.meta.url), 'utf8')

test('authenticated shell identifies Advertified without vendor demo content', () => {
  assert.match(signInSource, /Advertified/)
  assert.doesNotMatch(signInSource, /Get started|Vite community|Count is/)
})

test('browser runtime versions are exact, not floating ranges', () => {
  assert.equal(packageSource.dependencies.react, '19.2.0')
  assert.equal(packageSource.dependencies['react-dom'], '19.2.0')
  assert.equal(packageSource.dependencies['react-router-dom'], '7.18.3')
  assert.equal(packageSource.dependencies.zod, '4.5.2')
  assert.equal(packageSource.dependencies.toastr, '2.1.4')
})

test('unsupported task and notification surfaces stay truthful', () => {
  assert.match(deferredSource, /Navigate to="\/home" replace/)
  assert.doesNotMatch(deferredSource, /mock task|sample notification/i)
})

test('API failures map stable codes without rendering server detail', () => {
  assert.match(clientSource, /safeMessages/)
  assert.doesNotMatch(clientSource, /problem\.data\.detail/)
})

test('production product language does not expose environment implementation terms', () => {
  const productLanguage = productionCopySources.join('\n')
  assert.doesNotMatch(productLanguage,
    /Local deterministic|Local development|development build|Local identity|Deterministic validation/u)
  assert.match(productLanguage, /Paid AI is disabled/)
  assert.match(productLanguage, /Secure workspace access/)
})

test('connected screens preserve approved readable type and non-cropping generic artwork', () => {
  const css = connectedStyles.join('\n')
  assert.doesNotMatch(css, /font-size:(?:7|7\.5|8|8\.5|8\.8|9|9\.5|10|10\.5|11)px/)
  assert.doesNotMatch(css, /out-of-home-real\.jpg['"]?\)\s*center\/cover/)
  assert.match(appShellSource, /approved-sidebar-brand-panel/)
  assert.match(appShellSource, /<img src="\/assets\/media-inventory\/out-of-home-real\.jpg" alt="" \/>/)
})
