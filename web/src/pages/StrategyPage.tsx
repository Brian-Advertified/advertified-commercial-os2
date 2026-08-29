import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { Strategy } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

export function StrategyPage() {
  const { selected, loading } = useWorkspace()
  const { strategyId } = useParams()
  const [strategy, setStrategy] = useState<Strategy | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected || !strategyId) return
    let active = true
    void opportunityApi.getStrategy(selected.tenantId, strategyId)
      .then((value) => { if (active) setStrategy(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected, strategyId])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!strategyId) return <Navigate to="/opportunities" replace />
  if (error) return <MessageState title="Strategy could not be opened" message={error} />
  if (!strategy) return <LoadingState label="Loading strategy" />

  return (
    <section aria-labelledby="strategy-title">
      <Link className="text-action back-link" to={`/opportunities/${strategy.opportunityId}`}>
        ← Opportunity
      </Link>
      <header className="page-heading page-heading-split">
        <div><p className="eyebrow">Governed strategy artefact</p>
          <h1 id="strategy-title">Strategy version {strategy.versionNumber}</h1></div>
        <span className="status-chip">{strategy.status}</span>
      </header>
      {strategy.rejectionReason && <p className="inline-alert">{strategy.rejectionReason}</p>}
      <article className="detail-card strategy-card">
        <pre className="artifact-json">{formatJson(strategy.artifactJson)}</pre>
        <h2>Critic objections</h2>
        {strategy.objections.map((item) => (
          <div className="objection-card" key={item.id}>
            <strong>{item.severity}: {item.fieldPath}</strong>
            <p>{item.evidenceGap}</p><small>{item.resolution ?? 'Unresolved'}</small>
          </div>
        ))}
      </article>
    </section>
  )
}

function formatJson(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2) } catch { return value }
}
