import { useState } from 'react'
import { proposalApi } from '../api/proposal-client'
import type { Proposal, ProposalRecipient } from '../api/proposal-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

type ActionProps = {
  tenantId: string
  proposal: Proposal
  recipients: ProposalRecipient[]
  busy: boolean
  onApprove: () => Promise<void>
  onRender: () => Promise<void>
  onShare: (recipientUserId: string) => Promise<void>
}

export function ProposalAgencyActions(props: ActionProps) {
  const [recipient, setRecipient] = useState(props.recipients[0]?.userId ?? '')
  return <aside className="proposal-action-panel" aria-label="Proposal actions">
    <ActionExplanation proposal={props.proposal} />
    <PrimaryAction {...props} recipient={recipient} setRecipient={setRecipient} />
    {props.proposal.document && <a className="secondary-button proposal-document-link"
      href={proposalApi.documentUrl(props.tenantId, props.proposal.document.id)}
      target="_blank" rel="noreferrer">Open proposal PDF</a>}
  </aside>
}

function PrimaryAction(props: ActionProps & {
  recipient: string
  setRecipient: (value: string) => void
}) {
  const { proposal, busy } = props
  if (proposal.status === masterDataCodes.lifecycleStatuses.draft) {
    return <button className="primary-button" type="button" disabled={busy}
      onClick={() => void props.onApprove()}>{busy ? 'Approving…' : 'Approve proposal'}</button>
  }
  if (proposal.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!proposal.document) {
    return <button className="primary-button" type="button" disabled={busy}
      onClick={() => void props.onRender()}>{busy ? 'Creating PDF…' : 'Create branded PDF'}</button>
  }
  return <div className="proposal-share-control"><label>Client recipient
    <select value={props.recipient} onChange={event => props.setRecipient(event.target.value)}>
      <option value="">Choose a client approver</option>
      {props.recipients.map(item => <option value={item.userId} key={item.userId}>
        {item.displayName} · {item.email}
      </option>)}
    </select></label>
    <button className="primary-button" type="button" disabled={busy || !props.recipient}
      onClick={() => void props.onShare(props.recipient)}>{busy ? 'Sharing…' : 'Share with client'}</button></div>
}

function ActionExplanation({ proposal }: { proposal: Proposal }) {
  const copy = actionCopy(proposal)
  return <div><p className="eyebrow eyebrow-light">Next action</p>
    <h2>{copy.title}</h2><p>{copy.detail}</p></div>
}

function actionCopy(proposal: Proposal) {
  if (proposal.status === masterDataCodes.lifecycleStatuses.draft) {
    return { title: 'Approve the exact proposal version',
      detail: 'Approval freezes the wording and exact approved media-plan bindings.' }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.approved && !proposal.document) {
    return { title: 'Create the client document',
      detail: 'The PDF is generated only from this approved structured proposal.' }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.approved) {
    return { title: 'Choose who should review it',
      detail: 'Sharing makes the proposal visible to the selected client approver. It does not book media.' }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.sent) {
    return { title: 'Waiting for the client decision',
      detail: 'The assigned client approver can select one route or decline the proposal.' }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.selected) {
    return { title: 'Client choice recorded',
      detail: 'Only the selected immutable option can continue into booking and finance.' }
  }
  return { title: 'Client response recorded', detail: 'The proposal is closed without a selected route.' }
}
