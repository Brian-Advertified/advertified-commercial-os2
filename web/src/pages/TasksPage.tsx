import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { HumanTask } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

export function TasksPage() {
  const { selected, loading } = useWorkspace()
  const [tasks, setTasks] = useState<HumanTask[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected) return
    let active = true
    void opportunityApi.listTasks(selected.tenantId).then((result) => {
      if (active) setTasks(result)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    })
    return () => { active = false }
  }, [selected])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (error) return <MessageState title="Tasks could not be loaded" message={error} />
  if (!tasks) return <LoadingState label="Loading assigned tasks" />
  return (
    <section aria-labelledby="tasks-title">
      <header className="page-heading page-heading-split">
        <div><p className="eyebrow">Human checkpoints</p><h1 id="tasks-title">Assigned tasks</h1>
          <p>Only the named reviewer or approver can complete each checkpoint.</p></div>
        <span className="status-chip">{tasks.length} pending</span>
      </header>
      <div className="record-stack">
        {tasks.length === 0 && <article className="detail-card"><h2>You are clear</h2><p>No pending actions are assigned to you.</p></article>}
        {tasks.map((task) => (
          <Link className="record-card" to={task.briefId
            ? `/briefs/${task.briefId}` : `/opportunities/${task.opportunityId}`} key={task.id}>
            <div><span className="status-chip">{task.taskType.replaceAll('_', ' ')}</span><h2>{task.title}</h2></div>
            <p>{task.whyItMatters}</p><span className="record-arrow" aria-hidden="true">→</span>
          </Link>
        ))}
      </div>
    </section>
  )
}
