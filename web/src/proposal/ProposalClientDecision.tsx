import type { Proposal } from '../api/proposal-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { ProposalOptionCard } from './ProposalOptionCard'

export function ProposalClientDecision({ proposal, busy, onSelect, onDecline }: {
  proposal: Proposal
  busy: boolean
  onSelect: (optionId: string) => Promise<void>
  onDecline: () => Promise<void>
}) {
  const inventoryCurrent = proposal.inventoryReviewStatus ===
    masterDataCodes.proposalInventoryReviewStatuses.current
  const canRespond = proposal.status === masterDataCodes.lifecycleStatuses.sent
  const decisionOpen = canRespond && inventoryCurrent
  const selectedOptionId = proposal.decision?.optionId ?? null
  return <section className="proposal-client-decision" aria-labelledby="proposal-options-title">
    <DecisionHeading inventoryCurrent={inventoryCurrent} canRespond={canRespond} />
    <div className="proposal-options-grid">{proposal.options.map(option =>
      <ProposalOptionCard key={option.id} option={option}
        selected={selectedOptionId === option.id} decisionMode={decisionOpen}
        busy={busy} onSelect={() => onSelect(option.id)} />)}</div>
    {canRespond && <div className="proposal-decline-row"><div><strong>None of these routes fit?</strong>
      <p>{inventoryCurrent
        ? 'Declining closes this proposal without creating a booking.'
        : 'You may decline this proposal, but you cannot accept outdated inventory.'}</p></div>
      <button className="text-danger-button" type="button" disabled={busy}
        onClick={() => void onDecline()}>Decline proposal</button></div>}
    {proposal.status === masterDataCodes.lifecycleStatuses.selected &&
      <p className="proposal-decision-confirmation">Your selected route has been recorded. No media is booked until the next commercial steps are completed.</p>}
    {proposal.status === masterDataCodes.lifecycleStatuses.declined &&
      <p className="proposal-decision-confirmation is-declined">This proposal was declined. No media has been booked.</p>}
  </section>
}

function DecisionHeading({ inventoryCurrent, canRespond }: { inventoryCurrent: boolean; canRespond: boolean }) {
  return <div className="proposal-section-heading"><div><p className="eyebrow">Your media choices</p>
    <h2 id="proposal-options-title">{inventoryCurrent
      ? 'Choose the route that best fits the campaign'
      : 'These choices require updated supplier inventory'}</h2>
    <p>{inventoryCurrent
      ? 'Each option is tied to a separately approved media plan with its own budget, media and running periods.'
      : 'The proposal remains available for reference, but an updated version is required before you can select a route.'}</p></div>
    {canRespond && <span className="status-chip">{inventoryCurrent
      ? 'Decision required' : 'Inventory update under review'}</span>}</div>
}
