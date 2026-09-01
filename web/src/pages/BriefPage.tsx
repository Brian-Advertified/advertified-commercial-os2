import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { humanMessage } from '../api/client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { CampaignBrief } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { BriefSourceHistory } from '../brief/BriefSourceHistory'
import { BriefStructuredReview } from '../brief/BriefStructuredReview'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'

const briefConfirmerRoles: readonly string[] =
  Object.values(opportunityCodes.briefConfirmerRole)

type BriefWorkspaceView = 'structured' | 'evidence' | 'source' | 'history'

const briefWorkspaceViews: readonly { id: BriefWorkspaceView; label: string }[] = [
  { id: 'structured', label: 'Structured Brief' },
  { id: 'evidence', label: 'Evidence and open items' },
  { id: 'source', label: 'Original source' },
  { id: 'history', label: 'Version history' },
]

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
  const model = useBriefRecord(tenantId, briefId)
  if (model.error && !model.record) {
    return <MessageState title="Brief could not be opened" message={model.error} />
  }
  if (!model.record) return <LoadingState label="Loading the campaign Brief" />
  return <BriefRecordContent record={model.record} error={model.error}
    busy={model.busy} token={token} canConfirm={canConfirm} confirm={model.confirm} />
}

function BriefRecordContent({ record, error, busy, token, canConfirm, confirm }: {
  record: CampaignBrief
  error: string | null
  busy: boolean
  token: string
  canConfirm: boolean
  confirm: (version: CampaignBrief['versions'][number], token: string) => Promise<void>
}) {
  const [view, setView] = useBriefWorkspaceView()
  const current = record.versions.at(-1)
  if (!current) return <MessageState title="Brief is incomplete"
    message="No retained Brief version is available for review." />
  const approved = current.status === opportunityCodes.status.approved
  const allowed = canConfirm && isConfirmable(current.status)
  const backTarget = record.brief.opportunityId
    ? `/opportunities/${record.brief.opportunityId}` : '/home'
  return <section className="brief-record-page" aria-labelledby="brief-title">
    <BriefCommandHeader record={record} approved={approved} allowed={allowed}
      busy={busy} backTarget={backTarget} onConfirm={() => confirm(current, token)} />
    <BriefSummaryStrip record={record} />
    <BriefWorkspaceNavigation view={view} onSelect={setView} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {view === 'structured' || view === 'evidence'
      ? <BriefStructuredReview version={current} view={view} />
      : <BriefSourceHistory record={record} view={view} />}
  </section>
}

function BriefCommandHeader({ record, approved, allowed, busy, backTarget, onConfirm }: {
  record: CampaignBrief
  approved: boolean
  allowed: boolean
  busy: boolean
  backTarget: string
  onConfirm: () => Promise<void>
}) {
  const current = record.versions.at(-1)!
  return <header className="brief-command-header">
    <div className="brief-command-copy">
      <Link className="text-action back-link" to={backTarget}>← Back to work</Link>
      <p className="eyebrow">{record.brief.clientName} · Campaign Brief · Version {current.versionNumber}</p>
      <h1 id="brief-title">{record.brief.title}</h1>
      <p>{current.objective}</p>
    </div>
    <div className="brief-command-actions">
      <span className={`brief-command-status ${statusTone(current.status)}`}>
        <span aria-hidden="true" />{humanizeCode(record.brief.status, true)}
      </span>
      {allowed && <button className="primary-button" type="button" disabled={busy}
        onClick={() => void onConfirm()}>{busy ? 'Confirming…' : 'Confirm this Brief'}</button>}
      {approved && <Link className="primary-button" to={`/planning/${current.id}`}>
        Continue to planning</Link>}
    </div>
  </header>
}

function BriefSummaryStrip({ record }: { record: CampaignBrief }) {
  const current = record.versions.at(-1)!
  const unresolvedConflicts = current.conflicts.filter(item => !item.resolved).length
  const openItems = current.unknowns.length + unresolvedConflicts
  return <dl className="brief-summary-strip" aria-label="Current Brief summary">
    <SummaryItem label="Current version" value={`Version ${current.versionNumber}`}
      detail={`${record.versions.length} retained · ${formatDateTime(current.createdAtUtc)}`} />
    <SummaryItem label="Source integrity" value={`${record.sources.length} source${record.sources.length === 1 ? '' : 's'}`}
      detail="Original content retained" />
    <SummaryItem label="Open review items" value={String(openItems)}
      detail={`${current.unknowns.length} questions · ${unresolvedConflicts} conflicts`} />
    <SummaryItem label="Evidence on version" value={String(current.evidenceItemIds.length)}
      detail={`${current.facts.length} recorded facts`} />
  </dl>
}

function SummaryItem({ label, value, detail }: {
  label: string
  value: string
  detail: string
}) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{detail}</small></div>
}

function BriefWorkspaceNavigation({ view, onSelect }: {
  view: BriefWorkspaceView
  onSelect: (view: BriefWorkspaceView) => void
}) {
  return <nav className="brief-workspace-navigation" aria-label="Brief record views">
    {briefWorkspaceViews.map(item => <a key={item.id} href={`#brief-${item.id}`}
      aria-current={view === item.id ? 'page' : undefined}
      onClick={() => onSelect(item.id)}>{item.label}</a>)}
  </nav>
}

function useBriefWorkspaceView() {
  const [view, setView] = useState<BriefWorkspaceView>(() => viewFromHash(window.location.hash))
  useEffect(() => {
    const syncFromHash = () => setView(viewFromHash(window.location.hash))
    window.addEventListener('hashchange', syncFromHash)
    return () => window.removeEventListener('hashchange', syncFromHash)
  }, [])
  return [view, setView] as const
}

function viewFromHash(hash: string): BriefWorkspaceView {
  const requested = hash.replace('#brief-', '')
  return briefWorkspaceViews.some(item => item.id === requested)
    ? requested as BriefWorkspaceView
    : 'structured'
}

function isConfirmable(status: string) {
  return status === opportunityCodes.status.draft ||
    status === opportunityCodes.status.inReview
}

function statusTone(status: string) {
  if (status === opportunityCodes.status.approved) return 'is-positive'
  if (status === opportunityCodes.status.inReview) return 'is-warning'
  return 'is-neutral'
}

function useBriefRecord(tenantId: string, briefId: string) {
  const [record, setRecord] = useState<CampaignBrief | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    setRecord(await briefApi.get(tenantId, briefId)); setError(null)
  }, [tenantId, briefId])
  useEffect(() => {
    let active = true
    void briefApi.get(tenantId, briefId)
      .then(value => { if (active) setRecord(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, briefId])
  async function confirm(version: CampaignBrief['versions'][number], antiforgeryToken: string) {
    setBusy(true); setError(null)
    try {
      if (version.status === opportunityCodes.status.draft) {
        await briefApi.confirm(tenantId, version, antiforgeryToken)
      } else {
        await briefApi.approve(tenantId, version, antiforgeryToken)
      }
      await load()
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }
  return { record, error, busy, confirm }
}
