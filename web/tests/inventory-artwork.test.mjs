import assert from 'node:assert/strict'
import test from 'node:test'
import { inventoryArtwork } from '../src/inventory/inventoryArtwork.ts'

const cases = [
  ['METRO FM — Monday-Friday 06:00-09:00', 'SABC', 'metro-fm-mono.webp'],
  ['5FM — Monday-Friday 06:00-09:00', 'SABC', '5fm-mono.webp'],
  ['Kaya 959 Weekend Package', 'Kaya 959', 'kaya-959-mono.webp'],
  ['SABC1 — Morning Live', 'SABC', 'sabc-1-mono.webp'],
  ['SABC2 — Muvhango', 'SABC', 'sabc-2-mono.webp'],
  ['SABC3 — The Insider SA', 'SABC', 'sabc-3-mono.webp'],
  ['SABC News — The Full View', 'SABC', 'sabc-news.png'],
  ['RSG — Monday-Friday 06:00-09:00', 'SABC', 'rsg-mono.webp'],
  ['Umhlobo Wenene FM — Monday-Friday 06:00-09:00', 'SABC', 'umhlobo-wenene-fm-mono.webp'],
  ['Radio 2000 — Monday-Friday 06:00-09:00', 'SABC', 'radio-2000-mono.webp'],
]

test('inventory artwork follows the station or television channel identity', () => {
  for (const [name, supplierName, expectedFile] of cases) {
    assert.match(inventoryArtwork({ name, supplierName }) ?? '', new RegExp(`${expectedFile}$`))
  }
})
