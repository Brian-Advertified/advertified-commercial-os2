import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const appSource = await readFile(new URL('../src/App.tsx', import.meta.url), 'utf8')
const packageSource = JSON.parse(
  await readFile(new URL('../package.json', import.meta.url), 'utf8'),
)

test('foundation shell identifies Advertified without vendor demo content', () => {
  assert.match(appSource, /Advertified Commercial OS/)
  assert.doesNotMatch(appSource, /Get started|Vite community|Count is/)
})

test('React runtime versions are exact, not floating ranges', () => {
  assert.equal(packageSource.dependencies.react, '19.2.0')
  assert.equal(packageSource.dependencies['react-dom'], '19.2.0')
})
