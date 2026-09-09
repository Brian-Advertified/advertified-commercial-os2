import type { Shortlist } from '../api/planning-schemas'
import { formatMoney, formatNumber } from '../presentation/format'
import { combinationContent as copy } from './campaign-combination-content'

export function CampaignCombinations({ shortlist, editable, busy, onChoose }: {
  shortlist: Shortlist; editable: boolean; busy: boolean; onChoose: (ids: string[]) => void
}) {
  const combinations = shortlist.campaignCombinations
  if (!combinations) return null
  const candidates = new Map(shortlist.candidates.map(item => [item.id, item]))
  return <details className="buy-assessment"><summary>{copy.title}</summary>
    <p>{copy.explanation}</p><p>{copy.caveat}</p>
    {combinations.searchTruncated && <p>{copy.truncated}</p>}
    {combinations.missingCostCandidateCount > 0 && <p>{copy.missing} {combinations.missingCostCandidateCount}</p>}
    {combinations.alternatives.length === 0 && <p>{copy.none}</p>}
    <div className="shortlist-grid">{combinations.alternatives.map((alternative, index) =>
      <section className="buy-assessment" key={alternative.candidateIds.join(',')}>
        <h4>{copy.option} {index + 1}</h4>
        <strong>{formatMoney(alternative.campaignSupplierCostMinor, alternative.currency)}</strong>
        <p>{copy.supplier}</p>
        <ul>{alternative.candidateIds.map(id => <li key={id}>{candidates.get(id)?.name ?? copy.unknownCandidate}</li>)}</ul>
        {alternative.comparison && <RelativeComparison value={alternative.comparison} currency={alternative.currency}
          names={new Map(shortlist.candidates.map(item => [item.id, item.name]))} />}
        {alternative.audienceForecast && <AudienceForecast value={alternative.audienceForecast}
          names={new Map(shortlist.candidates.map(item => [item.id, item.name]))} />}
        {editable && <button type="button" className="secondary-button" disabled={busy}
          onClick={() => onChoose(alternative.candidateIds)}>{copy.use}</button>}
      </section>)}</div>
    {editable && <small>{copy.selection}</small>}
  </details>
}

type Alternative = NonNullable<Shortlist['campaignCombinations']>['alternatives'][number]
type Comparison = NonNullable<Alternative['comparison']>
type AudienceForecastValue = NonNullable<Alternative['audienceForecast']>

function AudienceForecast({ value, names }: { value: AudienceForecastValue; names: Map<string, string> }) {
  const facts = [
    [copy.grossReach, value.grossReach], [copy.deduplicatedReach, value.deduplicatedReach],
    [copy.duplicatedReach, value.duplicatedReach], [copy.impressions, value.totalImpressions],
    [copy.frequency, value.averageFrequency],
  ] as const
  return <div><h4>{copy.audienceForecast}</h4>
    <dl className="buy-assessment-facts">{facts.map(([label, metric]) => <div key={label}>
      <dt>{label}</dt><dd>{metric === null ? '—' : formatNumber(metric, label === copy.frequency ? 2 : 0)}</dd>
    </div>)}</dl>
    {value.measurementSource && <p><strong>{copy.audienceBasis}: </strong>
      {[value.measurementSource, value.measurementPeriod, value.universe].filter(Boolean).join(' · ')}</p>}
    {value.incrementalReach.some(item => item.incrementalReach !== null) && <div><strong>{copy.incremental}</strong><ul>
      {value.incrementalReach.map(item => <li key={item.candidateId}>{names.get(item.candidateId) ?? copy.unknownCandidate}: {' '}
        {item.incrementalReach === null ? 'Not measurable from current evidence' : formatNumber(item.incrementalReach, 0)}</li>)}
    </ul></div>}
    {value.evidenceGaps.length > 0 && <p><strong>{copy.audienceGaps}: </strong>{value.evidenceGaps.join(' · ')}</p>}
  </div>
}

function RelativeComparison({ value, currency, names }: { value: Comparison; currency: string; names: Map<string, string> }) {
  return <div><h4>{copy.comparison}</h4>
    <p>{value.supplierCostDeltaMinor === 0 ? copy.sameCost
      : `${value.supplierCostDeltaMinor > 0 ? '+' : '−'}${formatMoney(Math.abs(value.supplierCostDeltaMinor), currency)}`}</p>
    {value.addedCandidateIds.length === 0 && value.removedCandidateIds.length === 0 && <p>{copy.unchanged}</p>}
    <Changes label={copy.added} ids={value.addedCandidateIds} names={names} />
    <Changes label={copy.removed} ids={value.removedCandidateIds} names={names} />
    <dl className="buy-assessment-facts">
      <div><dt>{copy.targetCount}</dt><dd>{value.measuredTargetCandidateCount}</dd></div>
      <div><dt>{copy.missingDelivery}</dt><dd>{value.missingDeliveryCandidateCount}</dd></div>
      <div><dt>{copy.supplierCount}</dt><dd>{value.distinctInventoryWorkspaceCount}</dd></div>
    </dl>
    {value.plannedChannelRoles.length > 0 && <p><strong>{copy.roles}: </strong>{value.plannedChannelRoles.join(' · ')}</p>}
  </div>
}

function Changes({ label, ids, names }: { label: string; ids: string[]; names: Map<string, string> }) {
  return ids.length > 0 ? <div><strong>{label}</strong><ul>{ids.map(id =>
    <li key={id}>{names.get(id) ?? copy.unknownCandidate}</li>)}</ul></div> : null
}
