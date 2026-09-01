import type { ReactNode } from 'react'
import type { BriefVersion } from '../api/schemas'
import { formatMoney, humanizeCode } from '../presentation/format'

type BriefReviewView = 'structured' | 'evidence'

export function BriefStructuredReview({ version, view }: {
  version: BriefVersion
  view: BriefReviewView
}) {
  return view === 'evidence'
    ? <BriefEvidencePanel version={version} />
    : <StructuredBriefPanel version={version} />
}

function StructuredBriefPanel({ version }: { version: BriefVersion }) {
  return <section className="brief-workspace-panel" id="brief-structured"
    aria-labelledby="brief-structured-title">
    <header className="brief-panel-heading">
      <div><p className="eyebrow">Current interpretation</p>
        <h2 id="brief-structured-title">Structured Brief</h2></div>
      <p>Version {version.versionNumber} keeps the commercial direction in one reviewable ledger.</p>
    </header>
    <dl className="brief-detail-ledger">
      <LedgerRow label="Business problem" value={textValue(version.businessProblem)} />
      <LedgerRow label="Campaign objective" value={textValue(version.objective)} />
      <LedgerRow label="Audience direction" value={<ListValue values={version.audiences} />} />
      <LedgerRow label="Geography" value={<ListValue values={version.geographies} />} />
      <LedgerRow label="Budget" value={budgetLabel(version)} />
      <LedgerRow label="Timing" value={textValue(version.timing)} />
      <LedgerRow label="Tax treatment" value={version.vatStatus
        ? humanizeCode(version.vatStatus, true) : 'Not supplied'} />
      <LedgerRow label="Known fees" value={feesLabel(version)} />
      <LedgerRow label="Constraints" value={<ListValue values={version.constraints} />} />
      <LedgerRow label="Measurement" value={<ListValue values={version.measurement} />} />
    </dl>
  </section>
}

function BriefEvidencePanel({ version }: { version: BriefVersion }) {
  const rowCount = version.facts.length + version.assumptions.length +
    version.unknowns.length + version.conflicts.length + version.evidenceItemIds.length
  return <section className="brief-workspace-panel" id="brief-evidence"
    aria-labelledby="brief-evidence-title">
    <header className="brief-panel-heading">
      <div><p className="eyebrow">Decision support</p>
        <h2 id="brief-evidence-title">Evidence and open items</h2></div>
      <p>See what is recorded as fact, what still needs checking and which evidence records are linked.</p>
    </header>
    <EvidenceSummary version={version} />
    <div className="brief-table-scroll">
      <table className="brief-evidence-table">
        <thead><tr><th>Classification</th><th>Field</th><th>Recorded value</th><th>Review state</th></tr></thead>
        <tbody>
          {version.facts.map((fact, index) => <EvidenceRow key={`fact-${index}`}
            kind="Fact" field="Brief" value={fact} state="Retained on this version" />)}
          {version.assumptions.map((item, index) => <EvidenceRow key={`assumption-${index}`}
            kind="Assumption" field={humanizeCode(item.fieldPath, true)} value={item.value}
            state={`Check: ${item.validationNeeded} Impact: ${item.impact}`} />)}
          {version.unknowns.map((item, index) => <EvidenceRow key={`unknown-${index}`}
            kind="Unknown" field={humanizeCode(item.fieldPath, true)} value={item.question}
            state={item.isBlocking ? 'Answer required before approval' : 'Open, not blocking'} />)}
          {version.conflicts.map((item, index) => <EvidenceRow key={`conflict-${index}`}
            kind="Conflict" field={humanizeCode(item.fieldPath, true)} value={item.description}
            state={item.resolved
              ? `Resolved: ${item.resolution ?? 'Resolution recorded'}`
              : `Unresolved · ${humanizeCode(item.severity, true)}`} />)}
          {version.evidenceItemIds.map((id, index) => <EvidenceRow key={id}
            kind="Evidence reference" field={`Reference ${index + 1}`}
            value="Linked evidence record" state="Retained on this version" />)}
          {rowCount === 0 && <tr><td className="brief-table-empty" colSpan={4} data-label="">
            No evidence classifications or open review items are retained on this version.</td></tr>}
        </tbody>
      </table>
    </div>
  </section>
}

function EvidenceSummary({ version }: { version: BriefVersion }) {
  return <dl className="brief-evidence-summary" aria-label="Brief evidence summary">
    <Count label="Facts" value={version.facts.length} />
    <Count label="Assumptions" value={version.assumptions.length} />
    <Count label="Unknowns" value={version.unknowns.length} />
    <Count label="Conflicts" value={version.conflicts.length} />
    <Count label="Evidence references" value={version.evidenceItemIds.length} />
  </dl>
}

function Count({ label, value }: { label: string; value: number }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function EvidenceRow({ kind, field, value, state }: {
  kind: string
  field: string
  value: ReactNode
  state: string
}) {
  return <tr><td className="brief-evidence-kind" data-label="Classification">{kind}</td>
    <td data-label="Field">{field}</td><td data-label="Recorded value">{value}</td>
    <td data-label="Review state">{state}</td></tr>
}

function LedgerRow({ label, value }: { label: string; value: ReactNode }) {
  return <div className="brief-ledger-row"><dt>{label}</dt><dd>{value}</dd></div>
}

function ListValue({ values }: { values: string[] }) {
  return values.length > 0
    ? <ul>{values.map((value, index) => <li key={`${value}-${index}`}>{value}</li>)}</ul>
    : <span className="brief-not-supplied">Not supplied</span>
}

function textValue(value: string) {
  return value || 'Not supplied'
}

function budgetLabel(version: BriefVersion) {
  if (version.budgetUnknown || version.budgetMinor === null) return 'Not supplied'
  if (!version.currency) return 'Amount retained · currency not supplied'
  return formatMoney(version.budgetMinor, version.currency)
}

function feesLabel(version: BriefVersion) {
  if (version.feesMinor === null) return 'Not supplied'
  return version.currency
    ? formatMoney(version.feesMinor, version.currency)
    : 'Amount retained · currency not supplied'
}
