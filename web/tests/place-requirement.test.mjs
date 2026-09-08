import assert from 'node:assert/strict'
import test from 'node:test'
import { placeRequirement } from '../src/brief-intake/place-requirement.ts'

const supplied = { name: 'Synthetic pharmacy branch', latitude: '-26.2', longitude: '28.04',
  radius: '500', source: 'fixture:verified-branch-directory' }

test('place entry retains exact coordinates, radius and evidence without self-verifying', () => {
  const result = placeRequirement(supplied)
  assert.deepEqual(JSON.parse(result.geoJson), { type: 'Point', coordinates: [28.04, -26.2] })
  assert.equal(result.radiusMetres, 500)
  assert.equal(result.sourceLocator, supplied.source)
  assert.equal(result.isVerified, false)
  assert.equal(result.priority, 'REQUIRED')
})

test('a branch name cannot substitute for missing or invalid location evidence', () => {
  for (const invalid of [{ latitude: '' }, { longitude: '' }, { latitude: '91' },
    { longitude: '-181' }, { radius: '0' }, { radius: 'Infinity' }, { latitude: 'NaN' },
    { source: ' ' }, { name: ' ' }]) {
    assert.equal(placeRequirement({ ...supplied, ...invalid }), null, JSON.stringify(invalid))
  }
})
