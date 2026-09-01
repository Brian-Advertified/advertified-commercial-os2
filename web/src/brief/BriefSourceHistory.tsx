import type { ReactNode } from 'react'
import type { CampaignBrief } from '../api/schemas'
import { formatDateTime, humanizeCode } from '../presentation/format'

type SourceHistoryView = 'source' | 'history'

export function BriefSourceHistory({ record, view }: {
  record: CampaignBrief
  view: SourceHistoryView
}) {
  return view === 'source'
    ? <BriefSourcePanel record={record} />
    : <BriefHistoryPanel record={record} />
}

function BriefSourcePanel({ record }: { record: CampaignBrief }) {
  return <section className="brief-workspace-panel" id="brief-source"
    aria-labelledby="brief-source-title">
    <header className="brief-panel-heading">
      <div><p className="eyebrow">Original request</p>
        <h2 id="brief-source-title">Client source retained verbatim</h2></div>
      <p>Read-only source content remains separate from every structured interpretation and revision.</p>
    </header>
    <p className="brief-integrity-note"><strong>Immutable source.</strong> The full SHA-256 reference
      identifies the exact content received; this screen does not alter it.</p>
    <div className="brief-source-ledger">
      {record.sources.map(source => <article className="brief-source-record" key={source.id}>
        <dl className="brief-source-metadata">
          <SourceFact label="Source title" value={source.title} />
          <SourceFact label="Source type" value={humanizeCode(source.sourceType, true)} />
          <SourceFact label="Received" value={formatDateTime(source.createdAtUtc)} />
          <SourceFact label="SHA-256" value={<code>{source.contentHash}</code>} />
        </dl>
        <pre className="brief-source-copy">{source.content}</pre>
      </article>)}
      {record.sources.length === 0 && <p className="brief-empty-row">
        No retained source is available for this Brief.</p>}
    </div>
  </section>
}

function SourceFact({ label, value }: { label: string; value: ReactNode }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function BriefHistoryPanel({ record }: { record: CampaignBrief }) {
  const sourceTitles = new Map(record.sources.map(source => [source.id, source.title]))
  return <section className="brief-workspace-panel" id="brief-history"
    aria-labelledby="brief-history-title">
    <header className="brief-panel-heading">
      <div><p className="eyebrow">Retained decisions</p>
        <h2 id="brief-history-title">Version history</h2></div>
      <p>{record.versions.length} immutable version{record.versions.length === 1 ? '' : 's'} retained.</p>
    </header>
    <div className="brief-table-scroll">
      <table className="brief-history-table">
        <thead><tr><th>Version</th><th>Status</th><th>Created</th>
          <th>Objective and source</th><th>Review note</th></tr></thead>
        <tbody>{[...record.versions].reverse().map(version => <tr key={version.id}>
          <td data-label="Version"><strong>Version {version.versionNumber}</strong></td>
          <td data-label="Status">{humanizeCode(version.status, true)}</td>
          <td data-label="Created">{formatDateTime(version.createdAtUtc)}</td>
          <td data-label="Objective and source"><span>{version.objective}</span><small>
            Source: {sourceTitles.get(version.sourceId) ?? 'Retained source'}</small></td>
          <td data-label="Review note">{reviewNote(version)}</td>
        </tr>)}</tbody>
      </table>
    </div>
  </section>
}

function reviewNote(version: CampaignBrief['versions'][number]) {
  if (version.requestedChanges) return version.requestedChanges
  if (version.rejectionReason) return version.rejectionReason
  return 'No review note recorded.'
}
