import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const signInSource = await readFile(new URL('../src/pages/SignInPage.tsx', import.meta.url), 'utf8')
const deferredSource = await readFile(new URL('../src/pages/DeferredPage.tsx', import.meta.url), 'utf8')
const clientSource = await readFile(new URL('../src/api/client.ts', import.meta.url), 'utf8')
const packageSource = JSON.parse(
  await readFile(new URL('../package.json', import.meta.url), 'utf8'),
)

test('authenticated shell identifies Advertified without vendor demo content', () => {
  assert.match(signInSource, /Advertified/)
  assert.doesNotMatch(signInSource, /Get started|Vite community|Count is/)
})

test('React runtime versions are exact, not floating ranges', () => {
  assert.equal(packageSource.dependencies.react, '19.2.0')
  assert.equal(packageSource.dependencies['react-dom'], '19.2.0')
  assert.equal(packageSource.dependencies['react-router-dom'], '7.18.3')
  assert.equal(packageSource.dependencies.zod, '4.5.2')
  assert.equal(packageSource.dependencies['react-toastify'], '11.1.0')
})

test('unsupported task and notification surfaces stay truthful', () => {
  assert.match(deferredSource, /does not invent queue entries or counts/)
  assert.doesNotMatch(deferredSource, /mock task|sample notification/i)
})

test('API failures map stable codes without rendering server detail', () => {
  assert.match(clientSource, /safeMessages/)
  assert.doesNotMatch(clientSource, /problem\.data\.detail/)
})
