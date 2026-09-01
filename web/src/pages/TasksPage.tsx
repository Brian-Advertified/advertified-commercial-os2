import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { HumanTask } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'

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
  return <TaskWorkspace tasks={tasks} />
}

function TaskWorkspace({ tasks }: { tasks: HumanTask[] }) {
  const taskTypes = new Set(tasks.map((task) => task.taskType)).size
  const resourceTypes = new Set(tasks.map((task) => task.resourceType)).size
  return <section className="operations-page" aria-labelledby="tasks-title">
    <header className="operations-command-header">
      <div><p className="eyebrow">Human checkpoints</p><h1 id="tasks-title">Assigned tasks</h1>
        <p>Only the named reviewer or approver can complete each recorded decision.</p></div>
    </header>
    <dl className="operations-context-strip">
      <div><dt>Pending decisions</dt><dd>{tasks.length}</dd></div>
      <div><dt>Decision types</dt><dd>{taskTypes}</dd></div>
      <div><dt>Resource types</dt><dd>{resourceTypes}</dd></div>
    </dl>
    <section className="operations-panel" aria-labelledby="task-queue-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Decision queue</p>
        <h2 id="task-queue-title">Work requiring your action</h2></div>
        <span>{tasks.length} pending</span></header>
      {tasks.length === 0 ? <div className="operations-empty-row operations-task-empty">
        <strong>You are clear</strong><p>No pending actions are assigned to you.</p>
      </div> : <div className="operations-table-scroll"><table className="operations-table operations-task-table">
        <thead><tr><th>Decision</th><th>Resource</th><th>Status</th><th>Assigned</th><th><span className="sr-only">Open</span></th></tr></thead>
        <tbody>{tasks.map((task) => <tr key={task.id}>
          <td><Link to={taskTarget(task)}><strong>{task.title}</strong></Link>
            <small>{task.whyItMatters}</small></td>
          <td>{humanizeCode(task.resourceType, true)}<small>Version {task.resourceVersion}</small></td>
          <td><span className="operations-state-label">{humanizeCode(task.taskType, true)}</span>
            <small>{humanizeCode(task.status)}</small></td>
          <td>{formatDateTime(task.createdAtUtc)}</td>
          <td><Link className="operations-row-action" to={taskTarget(task)} aria-label={`Open ${task.title}`}>→</Link></td>
        </tr>)}</tbody>
      </table></div>}
    </section>
  </section>
}

function taskTarget(task: HumanTask) {
  const resources = masterDataCodes.commercialResourceTypes
  if (task.resourceType === resources.creativeAsset) {
    return `/creative-assets/${task.resourceId}`
  }
  if (task.resourceType === resources.deliveryProof) {
    return `/delivery-proofs/${task.resourceId}`
  }
  if (task.resourceType === resources.performanceEvidence) {
    return `/performance-evidence/${task.resourceId}`
  }
  if (task.resourceType === resources.measurementReport) {
    return `/measurement-reports/${task.resourceId}`
  }
  if (task.resourceType === resources.campaign) {
    return `/campaigns/${task.resourceId}`
  }
  if (task.briefId) return `/briefs/${task.briefId}`
  if (task.opportunityId) return `/opportunities/${task.opportunityId}`
  return '/tasks'
}
