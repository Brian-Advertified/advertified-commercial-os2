import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, humanMessage } from '../api/client'
import { selectedWorkspaceStorageSchema, type Workspace } from '../api/schemas'
import { useSession } from './session-state'
import { WorkspaceContext } from './workspace-state'
const storageKey = 'advertified.workspace'

function readStoredTenantId(): string | null {
  const value = sessionStorage.getItem(storageKey)
  if (!value) return null
  try {
    const parsed = selectedWorkspaceStorageSchema.safeParse(JSON.parse(value))
    if (parsed.success) return parsed.data.tenantId
  } catch {
    // Invalid browser state is cleared below.
  }
  sessionStorage.removeItem(storageKey)
  return null
}

export function WorkspaceProvider({ children }: { children: React.ReactNode }) {
  const { session } = useSession()
  const [workspaces, setWorkspaces] = useState<Workspace[]>([])
  const [selected, setSelected] = useState<Workspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const reload = useCallback(async () => {
    if (!session?.authenticated) return
    setLoading(true)
    setError(null)
    try {
      const items = await api.listWorkspaces()
      const storedTenantId = readStoredTenantId()
      setWorkspaces(items)
      setSelected(items.find((item) => item.tenantId === storedTenantId) ?? null)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setLoading(false)
    }
  }, [session?.authenticated])

  useEffect(() => {
    if (!session?.authenticated) return
    let active = true
    void api.listWorkspaces().then((items) => {
      if (!active) return
      const storedTenantId = readStoredTenantId()
      setWorkspaces(items)
      setSelected(items.find((item) => item.tenantId === storedTenantId) ?? null)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [session?.authenticated])

  const select = useCallback((workspace: Workspace) => {
    sessionStorage.setItem(storageKey, JSON.stringify({ tenantId: workspace.tenantId }))
    setSelected(workspace)
  }, [])

  const value = useMemo(
    () => ({ workspaces, selected, loading, error, select, reload }),
    [error, loading, reload, select, selected, workspaces],
  )
  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>
}
