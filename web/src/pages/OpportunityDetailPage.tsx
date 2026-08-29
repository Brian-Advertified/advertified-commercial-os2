import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { OpportunityDetail } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { OpportunityActions } from '../components/OpportunityActions'

export function OpportunityDetailPage() {
  const { selected, loading } = useWorkspace()
  const { opportunityId } = useParams()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!opportunityId) return <Navigate to="/opportunities" replace />
  return <OpportunityRecord tenantId={selected.tenantId} opportunityId={opportunityId} />
}

function OpportunityRecord({ tenantId, opportunityId }: { tenantId: string; opportunityId: string }) {
  const [detail, setDetail] = useState<OpportunityDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(async () => {
    try {
      setDetail(await opportunityApi.get(tenantId, opportunityId))
      setError(null)
    } catch (failure) {
      setError(humanMessage(failure))
    }
  }, [tenantId, opportunityId])

  useEffect(() => {
    let active = true
    void opportunityApi.get(tenantId, opportunityId).then((value) => {
      if (active) setDetail(value)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    })
    return () => { active = false }
  }, [tenantId, opportunityId])
  if (error && !detail) return <MessageState title="Opportunity could not be opened" message={error} />
  if (!detail) return <LoadingState label="Loading opportunity record" />
  const opportunity = detail.opportunity
  return (
    <section aria-labelledby="opportunity-title">
      <Link className="text-action back-link" to="/opportunities">← Opportunities</Link>
      <header className="page-heading page-heading-split">
        <div><p className="eyebrow">Opportunity qualification</p>
          <h1 id="opportunity-title">{opportunity.title}</h1>
          <p>{opportunity.objectiveSummary ?? 'Objective not supplied'}</p></div>
        <span className="status-chip">{humanize(opportunity.stage)}</span>
      </header>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <article className="next-action-card opportunity-next">
        <p className="eyebrow eyebrow-light">Next governed action</p>
        <h2>{detail.nextAction}</h2>
        <p>Record version {opportunity.version}. Refresh before acting if another person changed it.</p>
      </article>
      <OpportunityActions detail={detail} tenantId={tenantId} reload={load} />
      <div className="qualification-grid">
        <EvidenceSection detail={detail} />
        <AgentSection detail={detail} />
      </div>
      <StrategySection detail={detail} />
      <RunSection detail={detail} />
    </section>
  )
}

function EvidenceSection({ detail }: { detail: OpportunityDetail }) {
  return (
    <article className="detail-card qualification-card">
      <p className="eyebrow">Retained evidence</p><h2>{detail.sources.length} source record(s)</h2>
      {detail.sources.map((source) => (
        <div className="lineage-item" key={source.id}>
          <strong>{source.title}</strong><span>{source.type} · {source.captureStatus}</span>
          <small>SHA-256 {source.contentHash.slice(0, 12)}…</small>
        </div>
      ))}
      {detail.evidenceItems.map((item) => (
        <blockquote className="evidence-claim" key={item.id}>
          <p>{item.excerpt}</p><footer>{item.claimType} · {item.reviewStatus}</footer>
        </blockquote>
      ))}
      {detail.evidenceSet && <p className="lineage-note">
        Evidence set v{detail.evidenceSet.versionNumber}: {detail.evidenceSet.status}
      </p>}
    </article>
  )
}

function AgentSection({ detail }: { detail: OpportunityDetail }) {
  return (
    <article className="detail-card qualification-card">
      <p className="eyebrow">Interpretation and angles</p>
      <h2>{detail.interpretation ? `Interpretation ${detail.interpretation.status}` : 'Not generated'}</h2>
      {detail.interpretation && <Artifact value={detail.interpretation.artifactJson} />}
      <div className="angle-stack">
        {detail.angles.map((angle) => (
          <div className={`angle-card${
            angle.status === opportunityCodes.angleStatus.selected ? ' angle-selected' : ''
          }`} key={angle.id}>
            <span>#{angle.rank} · {Math.round(angle.confidence * 100)}%</span>
            <strong>{angle.title}</strong><p>{angle.rationale}</p>
          </div>
        ))}
      </div>
    </article>
  )
}

function StrategySection({ detail }: { detail: OpportunityDetail }) {
  if (!detail.strategy) return null
  return (
    <article className="detail-card strategy-card">
      <div className="page-heading-split"><div><p className="eyebrow">Strategy version</p>
        <h2><Link to={`/strategies/${detail.strategy.id}`}>
          Version {detail.strategy.versionNumber}
        </Link></h2></div>
        <span className="status-chip">{detail.strategy.status}</span></div>
      <Artifact value={detail.strategy.artifactJson} />
      <h3>Critic objections</h3>
      {detail.strategy.objections.map((item) => (
        <div className="objection-card" key={item.id}>
          <strong>{item.severity}: {item.fieldPath}</strong><p>{item.evidenceGap}</p>
          <small>{item.resolution ?? 'Unresolved'}</small>
        </div>
      ))}
    </article>
  )
}

function RunSection({ detail }: { detail: OpportunityDetail }) {
  if (detail.runs.length === 0) return null
  return (
    <article className="detail-card run-card"><p className="eyebrow">Durable agent runs</p>
      <div className="run-list">{detail.runs.map((run) => (
        <div key={run.id}><strong><Link to={`/runs/${run.id}`}>
          {humanize(run.runKind)}
        </Link></strong>
          <span>{run.status} · {run.attempts} attempt(s) · cost {run.incrementalCostMinor}</span>
          {run.recoveryAction && <small>{run.recoveryAction}</small>}</div>
      ))}</div>
    </article>
  )
}

function Artifact({ value }: { value: string }) {
  let formatted = value
  try { formatted = JSON.stringify(JSON.parse(value), null, 2) } catch { /* validated by API */ }
  return <pre className="artifact-json">{formatted}</pre>
}

function humanize(code: string): string {
  return code.toLowerCase().replaceAll('_', ' ')
}
