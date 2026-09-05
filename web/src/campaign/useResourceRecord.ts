import { useLayoutEffect, useMemo, useRef, useState } from 'react'
import { humanMessage } from '../api/client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { notifications } from '../notifications/notifications'

type ResourceState<T> = {
  scope: object
  record: T | null
  error: string | null
  busy: boolean
}

export function useResourceRecord<T>(loader: () => Promise<T>) {
  const { session } = useSession()
  const { selected } = useWorkspace()
  const identity = `${selected?.membershipId}:${selected?.version}:${session?.antiforgeryToken}`
  const scope = useMemo(() => ({ loader, identity }), [loader, identity])
  const control = useRef<{ scope: object | null; epoch: number; busy: boolean }>({ scope: null, epoch: 0, busy: false })
  const [state, setState] = useState<ResourceState<T> | null>(null)
  const current = state?.scope === scope ? state : null

  useLayoutEffect(() => {
    const active = control.current
    active.scope = scope
    active.busy = false
    const epoch = ++active.epoch
    void scope.loader().then(record => {
      if (active.scope === scope && active.epoch === epoch)
        setState({ scope, record, error: null, busy: false })
    }).catch((failure: unknown) => {
      if (active.scope === scope && active.epoch === epoch)
        setState({ scope, record: null, error: humanMessage(failure), busy: false })
    })
    return () => { active.scope = null; active.epoch++ }
  }, [scope])

  async function run(action: () => Promise<unknown>, success: string) {
    if (control.current.scope !== scope || control.current.busy || !current?.record) return
    control.current.busy = true
    const epoch = ++control.current.epoch
    const isCurrent = () => control.current.scope === scope && control.current.epoch === epoch
    setState({ ...current, busy: true, error: null })
    try {
      await action()
      if (!isCurrent()) return
      const record = await scope.loader()
      if (!isCurrent()) return
      setState({ scope, record, busy: false, error: null })
      notifications.success(success)
    } catch (failure) {
      if (isCurrent()) setState({ ...current, busy: false, error: humanMessage(failure) })
    } finally {
      if (isCurrent()) control.current.busy = false
    }
  }

  const { record, error, busy } = current ?? { record: null, error: null, busy: false }
  return { record, error, busy, run }
}
