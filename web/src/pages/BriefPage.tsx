import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { humanMessage } from '../api/client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { BriefVersion, CampaignBrief } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { formatMoney } from '../presentation/format'

const briefConfirmerRoles: readonly string[] =
  Object.values(opportunityCodes.briefConfirmerRole)

export function BriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const { briefId } = useParams()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefId || !session) return <Navigate to="/briefs/new" replace />
  return <BriefRecord tenantId={selected.tenantId} briefId={briefId}
    token={session.antiforgeryToken}
    canConfirm={briefConfirmerRoles.includes(selected.roleCode)} />
}

function BriefRecord({ tenantId, briefId, token, canConfirm }: {
  tenantId: string
  briefId: string
  token: string
  canConfirm: boolean
}) {
  const [record, setRecord] = useState<CampaignBrief | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    setRecord(await briefApi.get(tenantId, briefId)); setError(null)
  }, [tenantId, briefId])

  useEffect(() => {
    let active = true
    void briefApi.get(tenantId, briefId)
      .then((brief) => { if (active) setRecord(brief) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, briefId])
  if (error && !record) return <MessageState title="Brief could not be opened" message={error} />
  if (!record) return <LoadingState label="Loading the campaign Brief" />
  const current = record.versions.at(-1)
  async function confirm() {
    if (!current) return
    setBusy(true); setError(null)
    try {
      if (current.status === opportunityCodes.status.draft) {
        await briefApi.confirm(tenantId, current, token)
      }
      else await briefApi.approve(tenantId, current, token)
      await load()
    } catch (failure) { setError(humanMessage(failure)) } finally { setBusy(false) }
  }
  return <section aria-labelledby="brief-title">
    <Link className="text-action back-link" to={record.brief.opportunityId
      ? `/opportunities/${record.brief.opportunityId}` : '/briefs/new'}>← Back</Link>
    <header className="page-heading page-heading-split"><div><p className="eyebrow">Campaign Brief</p>
      <h1 id="brief-title">{record.brief.title}</h1><p>One canonical source, every version retained.</p></div>
      <span className="status-chip">{record.brief.status.replaceAll('_', ' ')}</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {current && <BriefOverview version={current} busy={busy} confirm={confirm}
      allowed={canConfirm} />}
    <SourcePanel record={record} />
    <VersionHistory versions={record.versions} />
  </section>
}

function BriefOverview({ version, busy, confirm, allowed }: {
  version: BriefVersion
  busy: boolean
  confirm: () => Promise<void>
  allowed: boolean
}) {
  const canConfirm = allowed && (version.status === opportunityCodes.status.draft ||
    version.status === opportunityCodes.status.inReview)
  return <article className="next-action-card brief-overview">
    <div className="page-heading-split"><div><p className="eyebrow eyebrow-light">Current version</p>
      <h2>Version {version.versionNumber}: {version.objective}</h2></div>
      {canConfirm && <button className="primary-button" type="button" disabled={busy}
        onClick={() => void confirm()}>{busy ? 'Confirming…' : 'Confirm this Brief'}</button>}
      {version.status === opportunityCodes.status.approved &&
        <Link className="primary-button" to={`/planning/${version.id}`}>Start planning</Link>}</div>
    <p><strong>Business problem:</strong> {version.businessProblem}</p>
    <p><strong>Timing:</strong> {version.timing}</p>
    <p><strong>Budget:</strong> {version.budgetUnknown ? 'Not supplied' : formatMoney(
      version.budgetMinor ?? 0, version.currency ?? opportunityCodes.currency.zar, 2)}</p>
    <TagList label="Audience direction" values={version.audiences} />
    <TagList label="Geographies" values={version.geographies} />
    {version.unknowns.length > 0 && <div><h3>Still to confirm</h3><ul>
      {version.unknowns.map((item) => <li key={`${item.fieldPath}-${item.question}`}>{item.question}</li>)}
    </ul></div>}
  </article>
}

function SourcePanel({ record }: { record: CampaignBrief }) {
  return <article className="detail-card"><p className="eyebrow">Original source</p>
    {record.sources.map((source) => <details key={source.id}>
      <summary>{source.title} · SHA-256 {source.contentHash.slice(0, 12)}…</summary>
      <p className="source-copy">{source.content}</p>
    </details>)}</article>
}

function VersionHistory({ versions }: { versions: BriefVersion[] }) {
  return <article className="detail-card"><p className="eyebrow">Version comparison</p>
    <h2>{versions.length} retained version{versions.length === 1 ? '' : 's'}</h2>
    <div className="version-grid">{versions.map((version) => <section key={version.id}>
      <span className="status-chip">{version.status}</span><h3>Version {version.versionNumber}</h3>
      <p>{version.objective}</p><small>Created {new Date(version.createdAtUtc).toLocaleString()}</small>
      {version.requestedChanges && <p>Requested: {version.requestedChanges}</p>}
    </section>)}</div>
  </article>
}

function TagList({ label, values }: { label: string; values: string[] }) {
  return <div><strong>{label}:</strong> {values.length ? values.join(', ') : 'Not supplied'}</div>
}
