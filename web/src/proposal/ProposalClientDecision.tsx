import type { Proposal } from '../api/proposal-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { ProposalOptionCard } from './ProposalOptionCard'

export function ProposalClientDecision({ proposal, busy, onSelect, onDecline }: {
  proposal: Proposal
  busy: boolean
  onSelect: (optionId: string) => Promise<void>
  onDecline: () => Promise<void>
}) {
  const decisionOpen = proposal.status === masterDataCodes.lifecycleStatuses.sent
  const selectedOptionId = proposal.decision?.optionId ?? null
  return <section className="proposal-client-decision" aria-labelledby="proposal-options-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Your media choices</p>
      <h2 id="proposal-options-title">Choose the route that best fits the campaign</h2>
      <p>Each option is tied to a separately approved media plan with its own budget, media and running periods.</p></div>
      {decisionOpen && <span className="status-chip">Decision required</span>}</div>
    <div className="proposal-options-grid">{proposal.options.map(option =>
      <ProposalOptionCard key={option.id} option={option}
        selected={selectedOptionId === option.id} decisionMode={decisionOpen}
        busy={busy} onSelect={() => onSelect(option.id)} />)}</div>
    {decisionOpen && <div className="proposal-decline-row"><div><strong>None of these routes fit?</strong>
      <p>Declining closes this proposal without creating a booking.</p></div>
      <button className="text-danger-button" type="button" disabled={busy}
        onClick={() => void onDecline()}>Decline proposal</button></div>}
    {proposal.status === masterDataCodes.lifecycleStatuses.selected &&
      <p className="proposal-decision-confirmation">Your selected route has been recorded. No media is booked until the next commercial steps are completed.</p>}
    {proposal.status === masterDataCodes.lifecycleStatuses.declined &&
      <p className="proposal-decision-confirmation is-declined">This proposal was declined. No media has been booked.</p>}
  </section>
}
