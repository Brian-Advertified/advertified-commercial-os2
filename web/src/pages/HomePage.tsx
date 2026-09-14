import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import type { Workspace } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { homeCopy } from '../content/home-copy'
import { ConnectedOperatorHome } from '../home/ConnectedOperatorHome'
import { homeAudience, loadDashboard, type DashboardData } from '../home/dashboard-data'
import { RoleHomeDashboard } from '../home/RoleHomeDashboard'

export function HomePage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!homeAudience(selected.roleCode)) return <MessageState
    title={homeCopy.noAccessTitle} message={homeCopy.noAccessMessage} />
  return <HomeData key={`${selected.tenantId}:${selected.roleCode}`} workspace={selected} />
}

function HomeData({ workspace }: { workspace: Workspace }) {
  const model = useDashboard(workspace)
  if (model.error) return <DashboardError error={model.error} retry={model.retry} />
  if (!model.data) return <LoadingState label={`Preparing ${workspace.name}`} />
  if (homeAudience(workspace.roleCode) === 'operator') return <ConnectedOperatorHome data={model.data} />
  return <RoleHomeDashboard roleCode={workspace.roleCode} displayName={model.data.user.displayName}
    workspaceName={workspace.name} currency={model.data.tenant.currencyCode}
    campaigns={model.data.campaigns} bookings={model.data.bookings} tasks={model.data.tasks}
    planning={model.data.planning} proposals={model.data.proposals} rfqs={model.data.rfqs}
    inventory={model.data.inventory} />
}

function useDashboard(workspace: Workspace) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    void loadDashboard(workspace)
      .then(value => { if (active) setData(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [workspace, attempt])
  function retry() { setData(null); setError(null); setAttempt(value => value + 1) }
  return { data, error, retry }
}

function DashboardError({ error, retry }: { error: string; retry: () => void }) {
  return <><MessageState title={homeCopy.unavailableTitle} message={error} />
    <p>{homeCopy.unavailableNote}</p>
    <button type="button" className="primary-button" onClick={retry}>{homeCopy.retry}</button></>
}
