import { useEffect, useState, type ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import {
  measurementIndexApi,
  type IndexPage,
  type MeasurementCampaignSummary,
  type MeasurementReportSummary,
} from '../api/measurement-index-client'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'
import { OperationalReporting } from '../reporting/OperationalReporting'

export function MeasurementIndexPage() {
  return <PagedIndex<MeasurementCampaignSummary> title="Campaign measurement"
    subtitle="Campaign-level evidence, reporting readiness and approved outcomes."
    load={measurementIndexApi.campaigns}
    metrics={items => [
      ['Campaigns', items.length, 'Campaigns in this result window'],
      ['Evidence', items.reduce((sum, item) => sum + item.evidenceCount, 0), 'Reviewed evidence items'],
      ['Reports', items.reduce((sum, item) => sum + item.reportCount, 0), 'Measurement reports'],
    ]}
    row={campaign => <IndexLink key={campaign.id} to={`/campaigns/${campaign.id}#measurement`}
      title={campaign.title}
      meta={`${humanizeCode(campaign.status, true)} · ${campaign.evidenceCount} reviewed evidence item(s) · ${campaign.reportCount} report(s)`}
      action={measurementAction(campaign)} updatedAtUtc={campaign.updatedAtUtc} />} />
}

export function ReportsIndexPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <section className="approved-work-index" aria-label="Reporting">
    <header className="approved-work-index-header"><div><h1>Reporting</h1>
      <p>Portfolio, operational and commercial intelligence across the current workspace.</p></div>
      <ReportingTabs /></header>
    <OperationalReporting tenantId={selected.tenantId} />
    <section className="reporting-measurement-index" aria-labelledby="approved-report-title">
      <h2 id="approved-report-title">Approved measurement reports</h2>
      <MeasurementReportIndex />
    </section>
  </section>
}

function MeasurementReportIndex() {
  return <PagedIndex<MeasurementReportSummary> title="Measurement reports" compact
    subtitle="Approved campaign-level reports backed by reviewed evidence."
    load={measurementIndexApi.reports}
    metrics={items => [['Reports', items.length, 'Reports in this result window']]}
    row={report => <IndexLink key={report.id} to={`/measurement-reports/${report.id}`}
      title={`${report.campaignTitle} · Report ${report.versionNumber}`}
      meta={`${humanizeCode(report.status, true)} · ${report.evidenceCount} evidence source(s)`}
      action="Review sourced interpretation" updatedAtUtc={report.updatedAtUtc} />} />
}

function ReportingTabs() {
  return <nav className="approved-reporting-tabs" aria-label="Reporting views">
    <Link to="/measurement">Campaign measurement</Link><Link to="/reports">Operations</Link>
  </nav>
}

type Metric = [label: string, value: number | string, note: string]
type LoadPage<T> = (tenantId: string, cursor: string | null) => Promise<IndexPage<T>>

function PagedIndex<T>({ title, subtitle, load, row, metrics, compact = false }: {
  title: string; subtitle: string; load: LoadPage<T>; row: (item: T) => ReactNode
  metrics: (items: T[]) => Metric[]; compact?: boolean
}) {
  const state = usePagedIndex(load)
  if (state.loading) return <LoadingState />
  if (!state.selected) return <Navigate to="/workspaces" replace />
  if (state.error) return <MessageState title={`${title} could not be opened`} message={state.error} />
  if (!state.page) return <LoadingState label={`Loading ${title.toLowerCase()}`} />
  return <PagedIndexContent title={title} subtitle={subtitle} compact={compact} page={state.page}
    metrics={metrics(state.page.items)} row={row} loadOlder={state.loadOlder} />
}

function usePagedIndex<T>(load: LoadPage<T>) {
  const { selected, loading: workspaceLoading } = useWorkspace()
  const tenantId = selected?.tenantId
  const [cursor, setCursor] = useState<string | null>(null)
  const key = `${tenantId ?? ''}:${cursor ?? ''}`
  const [result, setResult] = useState<{ key: string; page?: IndexPage<T>; error?: string } | null>(null)
  useEffect(() => {
    if (!tenantId) return
    let active = true
    void load(tenantId, cursor).then(page => { if (active) setResult({ key, page }) })
      .catch((failure: unknown) => { if (active) setResult({ key, error: humanMessage(failure) }) })
    return () => { active = false }
  }, [tenantId, cursor, key, load])
  const current = currentPageResult(result, key)
  return {
    selected,
    loading: workspaceLoading || Boolean(tenantId && !current),
    error: current?.error,
    page: current?.page,
    loadOlder: (next: string) => setCursor(next),
  }
}

function currentPageResult<T>(result: { key: string; page?: IndexPage<T>; error?: string } | null, key: string) {
  return result?.key === key ? result : null
}

function PagedIndexContent<T>({ title, subtitle, compact, page, metrics, row, loadOlder }: {
  title: string; subtitle: string; compact: boolean; page: IndexPage<T>; metrics: Metric[]
  row: (item: T) => ReactNode; loadOlder: (cursor: string) => void
}) {
  return <section className={`approved-work-index${compact ? ' is-nested' : ''}`}
    aria-labelledby={compact ? undefined : 'measurement-index-title'}>
    {!compact && <header className="approved-work-index-header"><div>
      <h1 id="measurement-index-title">{title}</h1><p>{subtitle}</p></div><ReportingTabs /></header>}
    {!compact && <div className="approved-work-queue-summary">{metrics.map(([label, value, note]) =>
      <article key={label}><span>{label}</span><strong>{value}</strong><small>{note}</small></article>)}</div>}
    <div className="approved-work-index-list">
      {page.items.length === 0 && <MeasurementEmpty title={title} />}
      {page.items.map(row)}
    </div>
    {page.nextCursor && <button className="secondary-button approved-index-more" type="button"
      onClick={() => loadOlder(page.nextCursor!)}>Load older</button>}
  </section>
}

function MeasurementEmpty({ title }: { title: string }) {
  const message = title === 'Campaign measurement'
    ? 'Campaigns appear here when delivery reaches the measurement stage.'
    : 'Approved measurement reports will appear here when they are generated.'
  return <article className="approved-work-index-empty"><strong>No {title.toLowerCase()} yet</strong>
    <p>{message}</p></article>
}

function IndexLink({ to, title, meta, action, updatedAtUtc }: {
  to: string; title: string; meta: string; action: string; updatedAtUtc: string
}) {
  return <Link className="approved-work-index-row" to={to}><span aria-hidden="true">↗</span>
    <div><strong>{title}</strong><small>{meta}</small><em>{action}</em></div>
    <time>{formatDateTime(updatedAtUtc)}</time><span aria-hidden="true">→</span></Link>
}

function measurementAction(campaign: MeasurementCampaignSummary) {
  if (campaign.evidenceCount === 0) return 'Add or review delivery evidence'
  if (campaign.reportCount === 0) return 'Prepare measurement report'
  return 'Review campaign outcome'
}
