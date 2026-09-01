import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { OpportunityDetail } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { OpportunityActions } from '../components/OpportunityActions'
import { formatDateTime, humanizeCode } from '../presentation/format'

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
  return <OpportunityWorkspace detail={detail} tenantId={tenantId} reload={load} error={error} />
}

function OpportunityWorkspace({ detail, tenantId, reload, error }: {
  detail: OpportunityDetail; tenantId: string; reload: () => Promise<void>; error: string | null
}) {
  const opportunity = detail.opportunity
  const metrics = [
    ['Record version', String(opportunity.version)],
    ['Retained sources', String(detail.sources.length)],
    ['Evidence claims', String(detail.evidenceItems.length)],
    ['Last updated', formatDateTime(opportunity.updatedAtUtc)],
  ]
  return <section className="operations-page" aria-labelledby="opportunity-title">
    <Link className="text-action back-link" to="/opportunities">← Opportunities</Link>
    <header className="operations-command-header">
      <div><p className="eyebrow">Opportunity qualification</p>
        <h1 id="opportunity-title">{opportunity.title}</h1>
        <p>{opportunity.objectiveSummary ?? 'Objective not supplied'}</p></div>
      <span className="operations-state-label">{humanizeCode(opportunity.stage)}</span>
    </header>
    <dl className="operations-context-strip operations-context-four">
      {metrics.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
    </dl>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <section className="operations-next-action" aria-labelledby="next-action-title">
      <div><p className="eyebrow">Next governed action</p><h2 id="next-action-title">{detail.nextAction}</h2></div>
      <p>Actions use record version {opportunity.version}; refresh if another person changed it.</p>
    </section>
    <OpportunityActions detail={detail} tenantId={tenantId} reload={reload} />
    <div className="operations-split-workspace operations-evidence-workspace">
      <EvidenceSection detail={detail} />
      <InterpretationSection detail={detail} />
    </div>
    <StrategySection detail={detail} />
    {detail.briefId && <section className="operations-linked-record">
      <div><p className="eyebrow">Campaign Brief</p><h2>Canonical Brief available</h2>
        <p>Facts, assumptions, unknowns and every version remain visible.</p></div>
      <Link className="secondary-button" to={`/briefs/${detail.briefId}`}>Review the canonical Brief</Link>
    </section>}
    <RunSection detail={detail} />
  </section>
}

function EvidenceSection({ detail }: { detail: OpportunityDetail }) {
  return <section className="operations-panel" aria-labelledby="evidence-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Retained evidence</p>
      <h2 id="evidence-title">Sources and reviewed claims</h2></div>
      <span>{detail.sources.length} source record(s)</span></header>
    <div className="operations-register">
      {detail.sources.length === 0 && <div className="operations-empty-row"><strong>No source registered</strong></div>}
      {detail.sources.map((source) => <div className="operations-register-row" key={source.id}>
        <span><strong>{source.title}</strong><small>File-integrity evidence · SHA-256 {source.contentHash.slice(0, 12)}…</small></span>
        <span><small>Type</small>{humanizeCode(source.type)}</span>
        <span><small>Capture</small>{humanizeCode(source.captureStatus)}</span>
      </div>)}
    </div>
    <div className="operations-claim-list">
      {detail.evidenceItems.map((item) => <blockquote key={item.id}>
        <p>{item.excerpt}</p><footer>{humanizeCode(item.claimType)} · {humanizeCode(item.reviewStatus)}</footer>
      </blockquote>)}
    </div>
    {detail.evidenceSet && <p className="operations-panel-footer">
      Evidence set v{detail.evidenceSet.versionNumber} · {humanizeCode(detail.evidenceSet.status)}
    </p>}
  </section>
}

function InterpretationSection({ detail }: { detail: OpportunityDetail }) {
  return <section className="operations-panel" aria-labelledby="interpretation-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Interpretation and angles</p>
      <h2 id="interpretation-title">{detail.interpretation
        ? `Interpretation ${humanizeCode(detail.interpretation.status)}` : 'Awaiting interpretation'}</h2></div>
      <span>{detail.angles.length} angle(s)</span></header>
    {detail.interpretation ? <Artifact value={detail.interpretation.artifactJson} />
      : <div className="operations-empty-row"><p>Approved evidence has not yet been interpreted.</p></div>}
    <div className="operations-angle-list">
      {detail.angles.map((angle) => <div className={angle.status === opportunityCodes.angleStatus.selected
        ? 'operations-angle-row is-selected' : 'operations-angle-row'} key={angle.id}>
        <span><strong>{angle.rank}</strong><small>Rank</small></span>
        <span><strong>{angle.title}</strong><small>{angle.rationale}</small></span>
        <span><strong>{Math.round(angle.confidence * 100)}%</strong><small>{humanizeCode(angle.status)}</small></span>
      </div>)}
    </div>
  </section>
}

function StrategySection({ detail }: { detail: OpportunityDetail }) {
  if (!detail.strategy) return null
  return <section className="operations-panel operations-full-panel" aria-labelledby="strategy-section-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Strategy version</p>
      <h2 id="strategy-section-title"><Link to={`/strategies/${detail.strategy.id}`}>
        Version {detail.strategy.versionNumber}</Link></h2></div>
      <span className="operations-state-label">{humanizeCode(detail.strategy.status)}</span></header>
    <div className="operations-strategy-grid"><Artifact value={detail.strategy.artifactJson} />
      <ObjectionRegister detail={detail} /></div>
  </section>
}

function ObjectionRegister({ detail }: { detail: OpportunityDetail }) {
  return <div className="operations-objections"><h3>Critic objections</h3>
    {detail.strategy?.objections.length === 0 && <p>No objections recorded.</p>}
    {detail.strategy?.objections.map((item) => <div key={item.id}>
      <span><strong>{humanizeCode(item.severity, true)}</strong><small>{item.fieldPath}</small></span>
      <p>{item.evidenceGap}</p><small>{item.resolution ?? 'Unresolved'}</small>
    </div>)}
  </div>
}

function RunSection({ detail }: { detail: OpportunityDetail }) {
  if (detail.runs.length === 0) return null
  return <section className="operations-panel operations-full-panel" aria-labelledby="run-history-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Workflow history</p>
      <h2 id="run-history-title">Recorded runs</h2></div><span>{detail.runs.length} run(s)</span></header>
    <div className="operations-table-scroll"><table className="operations-table"><thead><tr>
      <th>Workflow</th><th>Status</th><th>Attempts</th><th>Recovery</th><th><span className="sr-only">Open</span></th>
    </tr></thead><tbody>{detail.runs.map((run) => <tr key={run.id}>
      <td><Link to={`/runs/${run.id}`}><strong>{humanizeCode(run.runKind, true)}</strong></Link></td>
      <td>{humanizeCode(run.status)}</td><td>{run.attempts}</td>
      <td>{run.recoveryAction ?? 'Not required'}</td>
      <td><Link className="operations-row-action" to={`/runs/${run.id}`} aria-label={`Open ${humanizeCode(run.runKind)}`}>→</Link></td>
    </tr>)}</tbody></table></div>
  </section>
}

function Artifact({ value }: { value: string }) {
  const entries = artifactEntries(value)
  if (!entries) return <p className="operations-artifact-text">{value}</p>
  return <dl className="operations-artifact-list">{entries.map(([label, content]) =>
    <div key={label}><dt>{label}</dt><dd>{content}</dd></div>)}</dl>
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
