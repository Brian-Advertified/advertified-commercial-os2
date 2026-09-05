import assert from 'node:assert/strict'
import test from 'node:test'
import { bindPendingCommandsToActor, clearPendingCommandKeys, reserveCommandKey } from '../src/api/pending-command-keys.ts'
import { parseGeometry, validateGeometry } from '../src/components/map/geojson.ts'

test('ambiguous command retries preserve identity; edits, resources and actors do not', async () => {
  clearPendingCommandKeys()
  bindPendingCommandsToActor('actor-a')
  const init = { method: 'POST', body: '{"value":1}' }
  const first = await reserveCommandKey('/tenants/a/resources/one', init, 'first', 3)
  assert.equal((await reserveCommandKey('/tenants/a/resources/one', init, 'retry', 3)).key, first.key)
  assert.notEqual((await reserveCommandKey('/tenants/a/resources/two', init, 'other', 3)).key, first.key)
  assert.notEqual((await reserveCommandKey('/tenants/a/resources/one', init, 'version', 4)).key, first.key)
  assert.notEqual((await reserveCommandKey('/tenants/a/resources/one', { ...init, body: '{"value":2}' }, 'edited', 3)).key, first.key)
  bindPendingCommandsToActor('actor-b')
  assert.equal((await reserveCommandKey('/tenants/a/resources/one', init, 'new-actor', 3)).key, 'new-actor')
})

test('same file metadata with different content cannot share a command identity', async () => {
  clearPendingCommandKeys()
  const form = value => {
    const result = new FormData()
    result.append('source', new File([value], 'source.csv', { type: 'text/csv', lastModified: 1 }))
    return { method: 'POST', body: result }
  }
  const first = await reserveCommandKey('/upload', form('one'), 'one')
  assert.equal((await reserveCommandKey('/upload', form('one'), 'retry')).key, first.key)
  assert.equal((await reserveCommandKey('/upload', form('two'), 'two')).key, 'two')
})

test('changing session during asynchronous hashing cancels the unsent action', async () => {
  bindPendingCommandsToActor('actor-a')
  const pending = reserveCommandKey('/upload', { method: 'POST', body: '{}' }, 'old-session')
  bindPendingCommandsToActor('actor-b')
  await assert.rejects(pending, /session changed/u)
})

test('map geometry rejects malformed, unclosed, out-of-range and oversized shapes', () => {
  for (const value of [
    { type: 'Point', coordinates: [181, 0] },
    { type: 'Point', coordinates: [0, Infinity] },
    { type: 'Point', coordinates: [[1, 2]] },
    { type: 'GeometryCollection', geometries: [] },
    { type: 'Polygon', coordinates: [[[0, 0], [0, 1], [1, 1], [1, 0]]] },
    { type: 'MultiPoint', coordinates: Array.from({ length: 10001 }, () => [0, 0]) },
  ]) assert.equal(validateGeometry(value), null)
  assert.equal(parseGeometry('{broken'), null)
  const source = { type: 'Point', coordinates: [18, -33], arbitrary: 'discard' }
  const normalized = validateGeometry(source)
  source.coordinates[0] = 200
  assert.deepEqual(normalized, { type: 'Point', coordinates: [18, -33] })
})
