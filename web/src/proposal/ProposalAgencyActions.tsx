import { useMemo, useState } from 'react'
import { proposalApi } from '../api/proposal-client'
import type { Proposal, ProposalApprover, ProposalRecipient } from '../api/proposal-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

type ActionProps = {
  tenantId: string
  proposal: Proposal
  recipients: ProposalRecipient[]
  approvers: ProposalApprover[]
  currentUserId: string | null
  busy: boolean
  onSubmitForApproval: (approverUserId: string) => Promise<void>
  onApprove: () => Promise<void>
  onReject: (reason: string) => Promise<void>
  onRender: () => Promise<void>
  onShare: (recipientUserId: string) => Promise<void>
}

export function ProposalAgencyActions(props: ActionProps) {
  const [recipient, setRecipient] = useState(props.recipients[0]?.userId ?? '')
  const eligibleApprovers = useMemo(
    () => props.approvers.filter(item => item.userId !== props.currentUserId),
    [props.approvers, props.currentUserId],
  )
  const [approver, setApprover] = useState(
    eligibleApprovers[0]?.userId ?? '',
  )
  const [rejectionReason, setRejectionReason] = useState('')
  return <aside className="proposal-action-panel" aria-label="Proposal actions">
    <ActionExplanation proposal={props.proposal} approvers={props.approvers} />
    <PrimaryAction {...props} recipient={recipient} setRecipient={setRecipient}
      approver={approver} setApprover={setApprover}
      eligibleApprovers={eligibleApprovers}
      rejectionReason={rejectionReason} setRejectionReason={setRejectionReason} />
    {props.proposal.document && <a className="secondary-button proposal-document-link"
      href={proposalApi.documentUrl(props.tenantId, props.proposal.document.id)}
      target="_blank" rel="noreferrer">Open proposal PDF</a>}
  </aside>
}

function PrimaryAction(props: ActionProps & {
  recipient: string
  setRecipient: (value: string) => void
  approver: string
  setApprover: (value: string) => void
  eligibleApprovers: ProposalApprover[]
  rejectionReason: string
  setRejectionReason: (value: string) => void
}) {
  const { proposal, busy } = props
  if (proposal.status === masterDataCodes.lifecycleStatuses.draft) {
    return <DraftApprovalActions {...props} />
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.inReview) {
    return <IndependentReviewActions {...props} />
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.rejected) {
    return <div className="inline-alert" role="status">
      {proposal.approvalRejectionReason ?? 'The proposal was not approved.'}
    </div>
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

function DraftApprovalActions(props: ActionProps & {
  approver: string
  setApprover: (value: string) => void
  eligibleApprovers: ProposalApprover[]
}) {
  return <div className="proposal-share-control">
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.onApprove()}>
      {props.busy ? 'Approving…' : 'Approve now'}
    </button>
    <label>Or choose a different approver
      <select value={props.approver} onChange={event => props.setApprover(event.target.value)}>
        <option value="">Choose an approver</option>
        {props.eligibleApprovers.map(item => <option value={item.userId} key={item.userId}>
          {item.displayName} · {item.email}
        </option>)}
      </select>
    </label>
    <button className="secondary-button" type="button"
      disabled={props.busy || !props.approver}
      onClick={() => void props.onSubmitForApproval(props.approver)}>
      {props.busy ? 'Sending…' : 'Send for approval'}
    </button>
    <p className="field-note">Approving your own work is allowed only when this workspace permits it.</p>
  </div>
}

function IndependentReviewActions(props: ActionProps & {
  rejectionReason: string
  setRejectionReason: (value: string) => void
}) {
  const assigned = props.proposal.approvalAssigneeUserId === props.currentUserId
  if (!assigned) {
    const approver = props.approvers.find(
      item => item.userId === props.proposal.approvalAssigneeUserId)
    return <p className="field-note">
      Waiting for {approver?.displayName ?? 'the assigned approver'} to review this proposal.
    </p>
  }
  return <div className="proposal-share-control">
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.onApprove()}>
      {props.busy ? 'Approving…' : 'Approve proposal'}
    </button>
    <label>Reason if you cannot approve it
      <textarea value={props.rejectionReason} maxLength={1000}
        onChange={event => props.setRejectionReason(event.target.value)}
        placeholder="Explain what needs to change." />
    </label>
    <button className="secondary-button" type="button"
      disabled={props.busy || !props.rejectionReason.trim()}
      onClick={() => void props.onReject(props.rejectionReason.trim())}>
      Reject and return
    </button>
  </div>
}

function ActionExplanation({ proposal, approvers }: {
  proposal: Proposal
  approvers: ProposalApprover[]
}) {
  const copy = actionCopy(proposal, approvers)
  return <div><p className="eyebrow eyebrow-light">Next action</p>
    <h2>{copy.title}</h2><p>{copy.detail}</p></div>
}

function actionCopy(proposal: Proposal, approvers: ProposalApprover[]) {
  if (proposal.status === masterDataCodes.lifecycleStatuses.draft) {
    return { title: 'Choose how this proposal is approved',
      detail: 'Approve it now when your workspace permits self-approval, or send this exact version to a named approver.' }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.inReview) {
    const approver = approvers.find(item => item.userId === proposal.approvalAssigneeUserId)
    return { title: 'Approval is in progress',
      detail: `${approver?.displayName ?? 'The assigned approver'} is reviewing this exact proposal version.` }
  }
  if (proposal.status === masterDataCodes.lifecycleStatuses.rejected) {
    return { title: 'Approval was not granted',
      detail: 'Review the reason below and prepare a new proposal version when the required changes are ready.' }
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
