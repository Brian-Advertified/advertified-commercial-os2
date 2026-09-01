import { useCallback, useEffect, useState } from 'react'
import { humanMessage } from '../api/client'
import { notifications } from '../notifications/notifications'

export function useResourceRecord<T>(loader: () => Promise<T>) {
  const [record, setRecord] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const value = await loader()
    setRecord(value)
    setError(null)
  }, [loader])

  useEffect(() => {
    let active = true
    void loader()
      .then(value => { if (active) setRecord(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [loader])

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setError(null)
    try {
      await action()
      await load()
      notifications.success(success)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  return { record, error, busy, run }
}
