import { gzipSync } from 'node:zlib'
import { readdir, readFile } from 'node:fs/promises'
import { resolve } from 'node:path'

const rawLimit = 800 * 1024
const gzipLimit = 250 * 1024
const assetsDir = resolve(process.cwd(), 'dist', 'assets')
const names = (await readdir(assetsDir)).filter(name => name.endsWith('.js'))
const failures = []

for (const name of names) {
  const content = await readFile(resolve(assetsDir, name))
  const gzipBytes = gzipSync(content).byteLength
  if (content.byteLength > rawLimit || gzipBytes > gzipLimit) {
    failures.push(`${name}: ${(content.byteLength / 1024).toFixed(1)} KiB raw / ${(gzipBytes / 1024).toFixed(1)} KiB gzip`)
  }
}

if (failures.length > 0) {
  console.error('Web JavaScript bundle budget exceeded:')
  failures.forEach(value => console.error(`- ${value}`))
  process.exit(1)
}

console.log(`Web bundle budget passed for ${names.length} JavaScript assets (<= 800 KiB raw and <= 250 KiB gzip each).`)
