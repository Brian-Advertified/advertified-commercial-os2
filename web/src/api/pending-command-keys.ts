// Session-memory retry identities. Payloads and credentials are never stored here.
const pending = new Map<string, string>()
const maximumPendingCommands = 100
let currentActor: string | null = null
let sessionEpoch = 0

export function clearPendingCommandKeys() {
  pending.clear()
  sessionEpoch += 1
}

export function bindPendingCommandsToActor(actorId: string) {
  if (currentActor !== actorId) clearPendingCommandKeys()
  currentActor = actorId
}

export async function reserveCommandKey(
  path: string,
  init: RequestInit,
  proposedKey?: string,
  expectedVersion?: number,
): Promise<{ fingerprint: string; key: string } | null> {
  if (!proposedKey) return null
  const epoch = sessionEpoch
  const actorId = currentActor
  const body = await bodyIdentity(init.body)
  const fingerprint = await digest(new TextEncoder().encode(JSON.stringify({
    actorId, path, method: (init.method ?? 'GET').toUpperCase(),
    expectedVersion: expectedVersion ?? null, body,
  })))
  if (epoch !== sessionEpoch) throw new Error('The session changed before this command could be sent.')
  const existing = pending.get(fingerprint)
  if (existing) return { fingerprint, key: existing }
  if (pending.size >= maximumPendingCommands) {
    throw new Error('Too many unresolved commands. Reconcile pending work before continuing.')
  }
  pending.set(fingerprint, proposedKey)
  return { fingerprint, key: proposedKey }
}

export function completeCommand(fingerprint: string) {
  pending.delete(fingerprint)
}

async function bodyIdentity(body: BodyInit | null | undefined): Promise<unknown> {
  if (body === undefined || body === null) return null
  if (typeof body === 'string') return body
  if (body instanceof FormData) {
    const fields = []
    for (const [name, value] of body.entries()) {
      fields.push([name, typeof value === 'string' ? value : {
        name: value.name, type: value.type,
        hash: await digest(await value.arrayBuffer()),
      }])
    }
    return fields
  }
  throw new Error('This command body does not support a recoverable retry identity.')
}

async function digest(value: BufferSource): Promise<string> {
  const bytes = new Uint8Array(await crypto.subtle.digest('SHA-256', value))
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('')
}
