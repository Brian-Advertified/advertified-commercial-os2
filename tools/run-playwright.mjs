import { spawn } from 'node:child_process'
import net from 'node:net'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const toolsDir = path.dirname(fileURLToPath(import.meta.url))
const webDir = path.resolve(toolsDir, '../web')
const playwrightCli = path.join(webDir, 'node_modules', '@playwright', 'test', 'cli.js')

const port = await reserveFreePort()
const args = ['test', ...process.argv.slice(2)]

console.log(`Playwright parent selected http://127.0.0.1:${port}.`)
const child = spawn(process.execPath, [playwrightCli, ...args], {
  cwd: webDir,
  env: { ...process.env, ADVERTIFIED_PLAYWRIGHT_PORT: String(port) },
  stdio: 'inherit',
})

child.on('error', error => {
  console.error(error)
  process.exitCode = 1
})
child.on('exit', (code, signal) => {
  if (signal) {
    console.error(`Playwright exited from signal ${signal}.`)
    process.exitCode = 1
    return
  }
  process.exitCode = code ?? 1
})

function reserveFreePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer()
    server.unref()
    server.on('error', reject)
    server.listen({ host: '127.0.0.1', port: 0, exclusive: true }, () => {
      const address = server.address()
      if (!address || typeof address === 'string') {
        server.close(() => reject(new Error('Unable to allocate a Playwright port.')))
        return
      }
      const selected = address.port
      server.close(error => error ? reject(error) : resolve(selected))
    })
  })
}
