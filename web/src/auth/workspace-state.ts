import { createContext, useContext } from 'react'
import type { Workspace } from '../api/schemas'

export type WorkspaceState = {
  workspaces: Workspace[]
  selected: Workspace | null
  loading: boolean
  error: string | null
  select: (workspace: Workspace) => void
  reload: () => Promise<void>
}

export const WorkspaceContext = createContext<WorkspaceState | null>(null)

export function useWorkspace(): WorkspaceState {
  const context = useContext(WorkspaceContext)
  if (!context) throw new Error('WorkspaceProvider is required.')
  return context
}
