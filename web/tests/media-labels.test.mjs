import assert from 'node:assert/strict'
import test from 'node:test'
import { channelLabel, mediaLabel, clientMediaCopy } from '../src/presentation/media-labels.ts'
import { humanizeCode } from '../src/presentation/format.ts'

test('client media labels explain codes without inventing a billboard classification', () => {
  for (const [code, label] of [['OOH', 'Outdoor advertising'], ['DOOH', 'Digital screens'],
    ['OOH_SITE', 'Outdoor placement'], ['DOOH_SCREEN', 'Digital screen'],
    ['OOH_ONLY', 'Outdoor advertising and digital screens only']]) {
    assert.equal(mediaLabel(code), label)
    assert.equal(humanizeCode(code, true), label)
  }
  assert.equal(channelLabel('RADIO'), 'Radio')
  assert.equal(mediaLabel('WALL_MURAL'), undefined)
  assert.equal(humanizeCode('WALL_MURAL', true), 'Wall Mural')
  assert.equal(clientMediaCopy('OOH and DOOH support neighbourhood coverage.'),
    'Outdoor advertising and Digital screens support neighbourhood coverage.')
  assert.equal(clientMediaCopy('OOH_SITE remains a retained code.'), 'OOH_SITE remains a retained code.')
})
