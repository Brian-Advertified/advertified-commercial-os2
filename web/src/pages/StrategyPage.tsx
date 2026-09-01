import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { Strategy } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { humanizeCode } from '../presentation/format'

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
  return <StrategyWorkspace strategy={strategy} />
}

function StrategyWorkspace({ strategy }: { strategy: Strategy }) {
  const unresolved = strategy.objections.filter((item) => !item.resolution).length
  const metrics = [
    ['Strategy version', String(strategy.versionNumber)],
    ['Record version', String(strategy.version)],
    ['Objections', String(strategy.objections.length)],
    ['Unresolved', String(unresolved)],
  ]
  return <section className="operations-page" aria-labelledby="strategy-title">
    <Link className="text-action back-link" to={`/opportunities/${strategy.opportunityId}`}>
      ← Opportunity
    </Link>
    <header className="operations-command-header">
      <div><p className="eyebrow">Evidence-bound recommendation</p>
        <h1 id="strategy-title">Strategy version {strategy.versionNumber}</h1>
        <p>Review the recommendation and every recorded objection before a human decision.</p></div>
      <span className="operations-state-label">{humanizeCode(strategy.status)}</span>
    </header>
    <dl className="operations-context-strip operations-context-four">
      {metrics.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
    </dl>
    {strategy.rejectionReason && <p className="inline-alert">{strategy.rejectionReason}</p>}
    <div className="operations-split-workspace operations-strategy-workspace">
      <section className="operations-panel" aria-labelledby="strategy-record-title">
        <header className="operations-panel-header"><div><p className="eyebrow">Recommendation</p>
          <h2 id="strategy-record-title">Structured strategy record</h2></div></header>
        <StrategyArtifact value={strategy.artifactJson} />
      </section>
      <ObjectionPanel strategy={strategy} />
    </div>
  </section>
}

function ObjectionPanel({ strategy }: { strategy: Strategy }) {
  return <section className="operations-panel" aria-labelledby="strategy-objections-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Challenge register</p>
      <h2 id="strategy-objections-title">Critic objections</h2></div>
      <span>{strategy.objections.length} recorded</span></header>
    <div className="operations-objections operations-objections-standalone">
      {strategy.objections.length === 0 && <div className="operations-empty-row">
        <strong>No objections recorded</strong>
      </div>}
      {strategy.objections.map((item) => <div key={item.id}>
        <span><strong>{humanizeCode(item.severity, true)}</strong><small>{item.fieldPath}</small></span>
        <p>{item.evidenceGap}</p>
        <small>{item.resolution ?? 'Unresolved'} · {item.recommendedResolution}</small>
      </div>)}
    </div>
  </section>
}

function StrategyArtifact({ value }: { value: string }) {
  const entries = artifactEntries(value)
  if (!entries) return <p className="operations-artifact-text">{value}</p>
  return <dl className="operations-artifact-list operations-artifact-tall">
    {entries.map(([label, content]) => <div key={label}><dt>{label}</dt><dd>{content}</dd></div>)}
  </dl>
}

function artifactEntries(value: string): Array<[string, string]> | null {
  try {
    const parsed: unknown = JSON.parse(value)
    if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') return null
    return Object.entries(parsed).map(([key, content]) => [
      humanizeCode(key, true), displayArtifactValue(content),
    ])
  } catch { return null }
}

function displayArtifactValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return 'Not supplied'
  if (typeof value === 'string') return value
  if (Array.isArray(value)) return value.map(displayArtifactValue).join(' · ')
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}
