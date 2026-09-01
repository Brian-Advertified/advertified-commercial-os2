import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { AgentRun } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'

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
  return <RunWorkspace run={run} />
}

function RunWorkspace({ run }: { run: AgentRun }) {
  const metrics = [
    ['Current step', run.currentStep ? humanizeCode(run.currentStep) : 'Complete'],
    ['Attempts', String(run.attempts)],
    ['Recorded cost', `${run.incrementalCostMinor} minor units`],
    ['Last updated', formatDateTime(run.updatedAtUtc)],
  ]
  return <section className="operations-page" aria-labelledby="run-title">
    <Link className="text-action back-link" to={`/opportunities/${run.opportunityId}`}>
      ← Opportunity
    </Link>
    <header className="operations-command-header">
      <div><p className="eyebrow">Workflow progress</p>
        <h1 id="run-title">{humanizeCode(run.runKind, true)}</h1>
        <p>Persisted progress, retry history and recovery guidance for this workflow.</p></div>
      <span className="operations-state-label">{humanizeCode(run.status)}</span>
    </header>
    <dl className="operations-context-strip operations-context-four">
      {metrics.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
    </dl>
    <section className="operations-panel operations-run-panel" aria-labelledby="run-state-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Current state</p>
        <h2 id="run-state-title">{run.recoveryAction ? 'Human review required' : 'Workflow record'}</h2></div>
        <span>Record v{run.version}</span></header>
      {run.errorCode && <div className="operations-exception" role="status">
        <strong>This workflow could not continue.</strong>
        <p>Review the recovery guidance before retrying any action.</p>
        <details><summary>Support reference</summary><code>{run.errorCode}</code></details>
      </div>}
      {run.recoveryAction ? <div className="operations-recovery">
        <p className="eyebrow">Recovery guidance</p><p>{run.recoveryAction}</p>
      </div> : <div className="operations-empty-row">
        <strong>No recovery action is recorded</strong>
        <p>The persisted state does not currently require human intervention.</p>
      </div>}
    </section>
  </section>
}
