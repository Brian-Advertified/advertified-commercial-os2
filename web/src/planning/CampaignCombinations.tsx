import type { Shortlist } from '../api/planning-schemas'
import { formatMoney } from '../presentation/format'
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
    <div className="shortlist-grid">{combinations.alternatives.map(alternative =>
      <section className="buy-assessment" key={alternative.candidateIds.join(',')}>
        <strong>{formatMoney(alternative.campaignSupplierCostMinor, alternative.currency)}</strong>
        <p>{copy.supplier}</p>
        <ul>{alternative.candidateIds.map(id => <li key={id}>{candidates.get(id)?.name ?? id}</li>)}</ul>
        {editable && <button type="button" className="secondary-button" disabled={busy}
          onClick={() => onChoose(alternative.candidateIds)}>{copy.use}</button>}
      </section>)}</div>
    {editable && <small>{copy.selection}</small>}
  </details>
}
