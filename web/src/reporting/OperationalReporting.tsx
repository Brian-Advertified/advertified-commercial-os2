import { useEffect, useState, type ReactNode } from 'react'
import { humanMessage } from '../api/client'
import { reportingApi, type OperationalReport, type ReportingFilters } from '../api/reporting-client'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'
import { masterDataDefinitions } from '../generated/master-data-codes'
import { channelLabel } from '../presentation/media-labels'

export function OperationalReporting({ tenantId }: { tenantId: string }) {
  const [filters, setFilters] = useState<ReportingFilters>({})
  const [applied, setApplied] = useState<ReportingFilters>({})
  const [report, setReport] = useState<OperationalReport | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void reportingApi.get(tenantId, applied).then(value => {
      if (active) { setReport(value); setError(null) }
    }).catch(failure => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, applied])
  if (error && !report) return <MessageState title="Operational reporting could not be opened" message={error} />
  if (!report) return <LoadingState label="Loading operational reporting" />
  return <div className="operational-reporting">
    <header className="reporting-command-header">
      <div><p className="eyebrow">Live commercial control</p><h2>Portfolio position</h2>
        <p>Workload, financial reconciliation and exceptions from canonical records.</p></div>
      <div className="reporting-freshness"><span>Position at</span>
        <strong>{formatDateTime(report.generatedAtUtc)}</strong></div>
    </header>
    <ReportingFilterForm report={report} filters={filters} setFilters={setFilters}
      apply={() => setApplied(filters)} clear={() => { setFilters({}); setApplied({}) }} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <MetricGrid report={report} />
    <CommercialTable report={report} />
    <ChannelTable report={report} />
    <StatusTable report={report} />
    <ExceptionTable report={report} />
  </div>
}

function ReportingFilterForm({ report, filters, setFilters, apply, clear }: {
  report: OperationalReport; filters: ReportingFilters
  setFilters: (value: ReportingFilters) => void; apply: () => void; clear: () => void
}) {
  const set = (name: keyof ReportingFilters, value: string) => setFilters({ ...filters, [name]: value })
  return <form className="reporting-filter-panel" onSubmit={event => { event.preventDefault(); apply() }}>
    <div className="reporting-filter-heading"><span>Portfolio lens</span>
      <small>Refine every figure below</small></div>
    <label>From <input type="date" value={filters.from ?? ''} onChange={e => set('from', e.target.value)} /></label>
    <label>To <input type="date" value={filters.to ?? ''} onChange={e => set('to', e.target.value)} /></label>
    <DimensionSelect label="Client" value={filters.clientAccountId} options={report.dimensions.clients}
      change={value => set('clientAccountId', value)} />
    <DimensionSelect label="Campaign" value={filters.campaignId} options={report.dimensions.campaigns}
      change={value => set('campaignId', value)} />
    <label>Channel <select value={filters.channel ?? ''} onChange={e => set('channel', e.target.value)}>
      <option value="">All channels</option>{masterDataDefinitions.channels.filter(item => item.isActive).map(item =>
        <option key={item.code} value={item.code}>{channelLabel(item.code)}</option>)}</select></label>
    <DimensionSelect label="Supplier" value={filters.supplierId} options={report.dimensions.suppliers}
      change={value => set('supplierId', value)} />
    <label>Status <input value={filters.status ?? ''} onChange={e => set('status', e.target.value)} placeholder="For example APPROVED" /></label>
    <DimensionSelect label="Owner" value={filters.ownerUserId} options={report.dimensions.users}
      change={value => set('ownerUserId', value)} />
    <DimensionSelect label="Reviewer" value={filters.reviewerUserId} options={report.dimensions.users}
      change={value => set('reviewerUserId', value)} />
    <div className="reporting-filter-actions"><button className="primary-button">Apply filters</button>
      <button type="button" className="secondary-button" onClick={clear}>Clear</button></div>
  </form>
}

function DimensionSelect({ label, value, options, change }: {
  label: string; value?: string; options: { id: string; label: string }[]; change: (value: string) => void
}) {
  return <label>{label}<select value={value ?? ''} onChange={event => change(event.target.value)}>
    <option value="">All</option>{options.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}
  </select></label>
}

function MetricGrid({ report }: { report: OperationalReport }) {
  const values = Object.entries(report.metrics)
  return <section className="reporting-section" aria-labelledby="report-metrics">
    <div className="reporting-section-heading"><div><p className="eyebrow">Portfolio</p>
      <h2 id="report-metrics">Connected workload</h2></div></div>
    <dl className="reporting-metric-grid">{values.map(([label, value]) =>
      <div key={label}><dt>{humanizeCode(label, true)}</dt><dd>{value}</dd></div>)}</dl></section>
}

function CommercialTable({ report }: { report: OperationalReport }) {
  const total = report.commercialTotals
  const rows = [['Supplier cost', total.supplierCostMinor], ['Markup', total.markupMinor],
    ['Commission', total.commissionMinor], ['Management fee', total.managementFeeMinor],
    ['All fees', total.feesMinor], ['VAT', total.vatMinor], ['Client total', total.clientTotalMinor]] as const
  return <ReportSection eyebrow="Commercial control" title="Booked reconciliation"
    detail={`${total.bookingCount} booking ${total.bookingCount === 1 ? 'line' : 'lines'} in scope.`}>
    <ReportTable><table><thead><tr><th>Component</th><th>Amount</th></tr></thead><tbody>{rows.map(([label, value]) =>
      <tr className={label === 'Client total' ? 'reporting-total-row' : undefined} key={label}>
        <td>{label}</td><td>{formatMoney(value, total.currency)}</td></tr>)}</tbody></table></ReportTable></ReportSection>
}

function ChannelTable({ report }: { report: OperationalReport }) {
  return <ReportSection eyebrow="Investment" title="Approved allocation by channel">
    <ReportTable empty={report.channelSpend.length === 0}><table><thead><tr><th>Channel</th>
    <th>Plans</th><th>Supplier cost</th><th>Fees</th><th>VAT</th><th>Client price</th></tr></thead>
    <tbody>{report.channelSpend.map(row => <tr key={`${row.channel}-${row.currency}`}><td>{humanizeCode(row.channel, true)}</td>
      <td>{row.planCount}</td><td>{formatMoney(row.supplierCostMinor, row.currency)}</td>
      <td>{formatMoney(row.feesMinor, row.currency)}</td><td>{formatMoney(row.vatMinor, row.currency)}</td>
      <td>{formatMoney(row.clientPriceMinor, row.currency)}</td></tr>)}</tbody></table></ReportTable></ReportSection>
}

function StatusTable({ report }: { report: OperationalReport }) {
  return <ReportSection eyebrow="Flow health" title="Stage and status ageing">
    <ReportTable empty={report.statuses.length === 0}><table><thead><tr><th>Area</th><th>Status</th>
    <th>Records</th><th>Oldest age</th></tr></thead><tbody>{report.statuses.map(row =>
      <tr key={`${row.area}-${row.status}`}><td>{row.area}</td><td>{humanizeCode(row.status, true)}</td>
        <td>{row.count}</td><td>{row.oldestAgeDays} {row.oldestAgeDays === 1 ? 'day' : 'days'}</td></tr>)}
      </tbody></table></ReportTable></ReportSection>
}

function ExceptionTable({ report }: { report: OperationalReport }) {
  return <ReportSection eyebrow="Attention" title="Exceptions and blocked work">
    <ReportTable empty={report.exceptions.length === 0}><table><thead><tr><th>Issue</th><th>Records</th></tr></thead>
      <tbody>{report.exceptions.map(row => <tr key={row.code}><td>{row.label}</td><td>{row.count}</td></tr>)}</tbody>
    </table></ReportTable></ReportSection>
}

function ReportSection({ eyebrow, title, detail, children }: {
  eyebrow: string; title: string; detail?: string; children: ReactNode
}) {
  return <section className="reporting-section"><div className="reporting-section-heading"><div>
    <p className="eyebrow">{eyebrow}</p><h2>{title}</h2>{detail && <p>{detail}</p>}
  </div></div>{children}</section>
}

function ReportTable({ children, empty = false }: { children: ReactNode; empty?: boolean }) {
  if (empty) return <p className="reporting-empty">No canonical records match this portfolio lens.</p>
  return <div className="reporting-table-scroll">{children}</div>
}
