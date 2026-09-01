import { useCallback, useEffect, useState } from 'react'
import { humanMessage } from '../api/client'
import { fundingApi } from '../api/funding-client'
import type { FundingWorkspace } from '../api/funding-schemas'
import { notifications } from '../notifications/notifications'

export function useFundingWorkspace(tenantId: string) {
  const [workspace, setWorkspace] = useState<FundingWorkspace | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    const value = await fundingApi.getWorkspace(tenantId)
    setWorkspace(value)
    setError(null)
  }, [tenantId])

  useEffect(() => {
    let active = true
    void fundingApi.getWorkspace(tenantId)
      .then(value => { if (active) setWorkspace(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId])

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

  return { workspace, error, busy, run }
}
