import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { measurementIndexApi, type IndexPage } from '../api/measurement-index-client'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'

export function MeasurementIndexPage() {
  return <PagedIndex title="Measurement" load={measurementIndexApi.campaigns}>
    {campaign => <SummaryRow title={campaign.title} updated={campaign.updatedAtUtc}
      to={`/campaigns/${campaign.id}#measurement`}
      meta={`${humanizeCode(campaign.status, true)} · ${campaign.evidenceCount} reviewed evidence item(s) · ${campaign.reportCount} report(s)`} />}
  </PagedIndex>
}

export function ReportsIndexPage() {
  return <PagedIndex title="Reports" load={measurementIndexApi.reports}>
    {report => <SummaryRow title={`${report.campaignTitle} · Report ${report.versionNumber}`}
      updated={report.updatedAtUtc} to={`/measurement-reports/${report.id}`}
      meta={`${humanizeCode(report.status, true)} · ${report.evidenceCount} evidence source(s)`} />}
  </PagedIndex>
}

type LoadPage<T> = (tenantId: string, cursor: string | null) => Promise<IndexPage<T>>

function PagedIndex<T extends { id: string }>({ title, load, children }: {
  title: string; load: LoadPage<T>; children: (item: T) => ReactNode
}) {
  const { selected, loading: workspaceLoading } = useWorkspace()
  const tenantId = selected?.tenantId
  const [navigation, setNavigation] = useState({ tenantId, cursor: null as string | null })
  const cursor = navigation.tenantId === tenantId ? navigation.cursor : null
  const [retry, setRetry] = useState(0)
  const key = pageResultKey(tenantId, cursor, retry)
  const [result, setResult] = useState<{
    key: string; page?: IndexPage<T>; error?: string
  } | null>(null)
  useEffect(() => {
    if (!tenantId) return
    let active = true
    void load(tenantId, cursor).then(page => {
      if (active) setResult({ key, page })
    }).catch((failure: unknown) => {
      if (active) setResult({ key, error: humanMessage(failure) })
    })
    return () => { active = false }
  }, [tenantId, cursor, key, load])
  if (workspaceLoading || (tenantId && result?.key !== key)) return <LoadingState />
  if (!tenantId) return <MessageState title={title} message="Select a workspace to continue." />
  return <IndexResults title={title} page={result?.page} error={result?.error}
    cursor={cursor} onRetry={() => setRetry(value => value + 1)}
    onNavigate={next => setNavigation({ tenantId, cursor: next })}>{children}</IndexResults>
}

function pageResultKey(tenantId: string | undefined, cursor: string | null, retry: number) {
  return `${tenantId ?? ''}:${cursor ?? ''}:${retry}`
}

function IndexResults<T extends { id: string }>({ title, page, error, cursor, onRetry, onNavigate, children }: {
  title: string; page?: IndexPage<T>; error?: string; cursor: string | null
  onRetry: () => void; onNavigate: (cursor: string | null) => void; children: (item: T) => ReactNode
}) {
  return <section className="approved-work-index" aria-label={title}>
    <header className="approved-work-index-header"><h1>{title}</h1></header>
    {error && <><MessageState title={`${title} could not be opened`} message={error} />
      <button type="button" onClick={onRetry}>Retry</button></>}
    <div className="approved-work-index-list">
      {page?.items.map(item => <div key={item.id}>{children(item)}</div>)}
      {page?.items.length === 0 && <p>No results are available.</p>}
    </div>
    {cursor && <button type="button" onClick={() => onNavigate(null)}>First page</button>}
    {page?.nextCursor && <button type="button" onClick={() => onNavigate(page.nextCursor)}>Next page</button>}
  </section>
}

function SummaryRow({ title, meta, updated, to }: {
  title: string; meta: string; updated: string; to: string
}) {
  return <Link className="approved-work-index-row" to={to}>
    <div><strong>{title}</strong><small>{meta}</small></div>
    <time>{formatDateTime(updated)}</time>
  </Link>
}
