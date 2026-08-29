import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { AgentRun } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

export function RunPage() {
  const { selected, loading } = useWorkspace()
  const { runId } = useParams()
  const [run, setRun] = useState<AgentRun | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected || !runId) return
    let active = true
    void opportunityApi.getRun(selected.tenantId, runId)
      .then((value) => { if (active) setRun(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected, runId])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!runId) return <Navigate to="/opportunities" replace />
  if (error) return <MessageState title="Agent run could not be opened" message={error} />
  if (!run) return <LoadingState label="Loading durable agent run" />

  return (
    <section aria-labelledby="run-title">
      <Link className="text-action back-link" to={`/opportunities/${run.opportunityId}`}>
        ← Opportunity
      </Link>
      <header className="page-heading page-heading-split">
        <div><p className="eyebrow">Durable deterministic execution</p>
          <h1 id="run-title">{humanize(run.runKind)}</h1></div>
        <span className="status-chip">{run.status}</span>
      </header>
      <article className="detail-card run-card">
        <dl className="record-grid">
          <div><dt>Current step</dt><dd>{run.currentStep ?? 'Complete'}</dd></div>
          <div><dt>Attempts</dt><dd>{run.attempts}</dd></div>
          <div><dt>Incremental cost</dt><dd>{run.incrementalCostMinor}</dd></div>
          <div><dt>Record version</dt><dd>{run.version}</dd></div>
        </dl>
        {run.errorCode && <p className="inline-alert">{run.errorCode}</p>}
        {run.recoveryAction && <p>{run.recoveryAction}</p>}
      </article>
    </section>
  )
}

function humanize(code: string): string {
  return code.toLowerCase().replaceAll('_', ' ')
}
