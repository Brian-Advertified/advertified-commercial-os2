import type { Proposal } from '../api/proposal-schemas'

export function ProposalCampaignDirection({ proposal }: { proposal: Proposal }) {
  const context = proposal.campaignContext
  if (!context) return null
  return <section className="proposal-campaign-direction" id="proposal-direction" aria-labelledby="proposal-direction-title">
    <header><div><p className="eyebrow">Campaign direction</p>
      <h2 id="proposal-direction-title">Why these media choices exist</h2>
      <p>The client decision is anchored to the approved Brief and audience direction, not only the media line items below.</p></div></header>
    <div className="proposal-direction-proof" aria-label="Advertified planning proof">
      <strong>{context.inventoryOptionsEvaluated}</strong><span>inventory options evaluated</span>
      <strong>{context.eligibleInventoryOptions}</strong><span>eligible choices</span>
      <strong>{context.suppliersEvaluated}</strong><span>suppliers searched</span>
      <strong>{context.selectedPlacements}</strong><span>placements carried forward</span>
    </div>
    <div className="proposal-direction-grid">
      <DirectionItem label="Business challenge" value={context.businessProblem} />
      <DirectionItem label="Objective" value={context.objective} />
      <DirectionList label="Target audiences" values={context.targetAudiences}
        empty={context.audienceDirectionConsistent ? 'No target audience names are retained.' : 'Audience direction differs between plan options.'} />
      <DirectionList label="Priority geography" values={context.geographies} empty="No priority geography is retained." />
      <DirectionItem label="Why this audience" value={context.targetingRationale ?? 'No targeting rationale is retained.'} />
      <DirectionItem label="Positioning" value={context.positioningStatement ?? 'No positioning direction is retained.'} />
      <DirectionList label="Success measures" values={context.successMeasures}
        empty="No explicit success measure is retained in the approved Brief." />
    </div>
    {!context.audienceDirectionConsistent && <footer>
      The approved proposal options use different audience-set lineages. Compare the outcome and media composition of each route rather than assuming one shared audience direction.
    </footer>}
  </section>
}

function DirectionItem({ label, value }: { label: string; value: string }) {
  return <article><small>{label}</small><p>{value}</p></article>
}

function DirectionList({ label, values, empty }: { label: string; values: string[]; empty: string }) {
  return <article><small>{label}</small>{values.length > 0
    ? <ul>{values.map(value => <li key={value}>{value}</li>)}</ul>
    : <p>{empty}</p>}</article>
}
