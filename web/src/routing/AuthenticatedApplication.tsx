import { Navigate } from 'react-router-dom'
import { useSession } from '../auth/session-state'
import { WorkspaceProvider } from '../auth/WorkspaceContext'
import { AppShell } from '../components/AppShell'
import { LoadingState } from '../components/PageState'

export function AuthenticatedApplication() {
  const { session, loading } = useSession()
  if (loading && !session) return <LoadingState />
  if (!session?.authenticated) return <Navigate to="/sign-in" replace />
  return <WorkspaceProvider><AppShell /></WorkspaceProvider>
}
