import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { proposalApi } from '../api/proposal-client'
import type { Proposal, ProposalRecipient, ProposalUpdateInput } from '../api/proposal-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { ProposalAgencyActions } from '../proposal/ProposalAgencyActions'
import { ProposalClientDecision } from '../proposal/ProposalClientDecision'
import { ProposalEditor } from '../proposal/ProposalEditor'
import { humanizeCode } from '../presentation/format'
import { ProposalOptionCard } from '../proposal/ProposalOptionCard'

const preparationRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

type ProposalContext = {
  tenantId: string
  proposalId: string
  token: string
  canPrepare: boolean
}
type ProposalAction = (action: () => Promise<Proposal>) => Promise<void>

export function ProposalPage() {
  const route = z.guid().safeParse(useParams().proposalId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/home" replace />
  return <ProposalRecord tenantId={selected.tenantId} proposalId={route.data}
    token={session.antiforgeryToken} canPrepare={preparationRoles.has(selected.roleCode)} />
}

function ProposalRecord(context: ProposalContext) {
  const state = useProposalRecord(context)
  if (state.error && !state.proposal) {
    return <MessageState title="Proposal could not be opened" message={state.error} />
  }
  if (!state.proposal) return <LoadingState label="Loading proposal" />
  return <ProposalContent {...context} {...state} proposal={state.proposal} />
}

function useProposalRecord({ tenantId, proposalId, canPrepare }: ProposalContext) {
  const [proposal, setProposal] = useState<Proposal | null>(null)
  const [recipients, setRecipients] = useState<ProposalRecipient[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const value = await proposalApi.get(tenantId, proposalId)
    setProposal(value); setError(null)
  }, [tenantId, proposalId])
  useEffect(() => {
    let active = true
    const recipientRequest = canPrepare ? proposalApi.listRecipients(tenantId) : Promise.resolve([])
    void Promise.all([proposalApi.get(tenantId, proposalId), recipientRequest])
      .then(([record, choices]) => {
        if (active) { setProposal(record); setRecipients(choices) }
      })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, proposalId, canPrepare])
  async function act(action: () => Promise<Proposal>) {
    setBusy(true); setError(null)
    try { await action(); await load() }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { proposal, recipients, error, busy, act }
}

type ProposalContentProps = ProposalContext & {
  proposal: Proposal
  recipients: ProposalRecipient[]
  error: string | null
  busy: boolean
  act: ProposalAction
}

function ProposalContent(props: ProposalContentProps) {
  const { proposal, canPrepare } = props
  return <section className="proposal-page" aria-labelledby="proposal-title">
    <Link className="text-action back-link"
      to={canPrepare ? `/briefs/${proposal.briefId}` : '/home'}>
      ← {canPrepare ? 'Back to Brief' : 'Back to work'}
    </Link>
    <ProposalHero proposal={proposal} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {canPrepare ? <AgencyProposalContent {...props} /> : <ClientProposalContent {...props} />}
  </section>
}

function AgencyProposalContent(props: ProposalContentProps) {
  const { proposal, busy, tenantId, token, act } = props
  const draft = proposal.status === masterDataCodes.lifecycleStatuses.draft
  return <>
    {draft ? <ProposalEditor key={`${proposal.id}-${proposal.version}`}
      proposal={proposal} busy={busy} onSave={(input: ProposalUpdateInput) => act(() =>
        proposalApi.update(tenantId, proposal, input, token))} /> :
      <ProposalSummary proposal={proposal} clientView={false} tenantId={tenantId} />}
    <ProposalPreview proposal={proposal} busy={busy} />
    <ProposalAgencyActions tenantId={tenantId} proposal={proposal}
      recipients={props.recipients} busy={busy}
      onApprove={() => act(() => proposalApi.approve(tenantId, proposal, token))}
      onRender={() => act(() => proposalApi.render(tenantId, proposal, token))}
      onShare={recipientUserId => act(() => proposalApi.share(
        tenantId, proposal, recipientUserId, token))} />
  </>
}

function ClientProposalContent(props: ProposalContentProps) {
  const { proposal, busy, tenantId, token, act } = props
  return <>
    <ProposalSummary proposal={proposal} clientView tenantId={tenantId} />
    <ProposalClientDecision proposal={proposal} busy={busy}
      onSelect={optionId => act(() => proposalApi.selectOption(
        tenantId, proposal, optionId, token))}
      onDecline={() => act(() => proposalApi.decline(tenantId, proposal, token))} />
  </>
}

function ProposalHero({ proposal }: { proposal: Proposal }) {
  return <header className="proposal-hero proposal-record-hero"><div>
    <p className="eyebrow eyebrow-light">Media proposal</p>
    <h1 id="proposal-title">{proposal.title}</h1><p>{proposal.executiveSummary}</p></div>
    <div className="proposal-status-block"><span className="status-chip">{humanizeCode(proposal.status, true)}</span>
      <small>Valid until {formatDateTime(proposal.expiryAtUtc)}</small></div>
  </header>
}

function ProposalSummary({ proposal, clientView, tenantId }: {
  proposal: Proposal
  clientView: boolean
  tenantId: string
}) {
  return <section className={`proposal-section ${clientView ? 'proposal-client-summary' : 'proposal-summary-card'}`}>
    <p className="eyebrow">{clientView ? 'Campaign direction' : 'Executive summary'}</p>
    <h2>{proposal.executiveSummary}</h2><p>{proposal.terms}</p>
    {clientView && proposal.document && <a className="secondary-button" target="_blank" rel="noreferrer"
      href={proposalApi.documentUrl(tenantId, proposal.document.id)}>Open proposal PDF</a>}
  </section>
}

function ProposalPreview({ proposal, busy }: { proposal: Proposal; busy: boolean }) {
  return <section className="proposal-section" aria-labelledby="proposal-preview-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Proposal preview</p>
      <h2 id="proposal-preview-title">Client choices</h2>
      <p>Only approved plan totals and client-safe facts are shown.</p></div></div>
    <div className="proposal-options-grid">{proposal.options.map(option =>
      <ProposalOptionCard key={option.id} option={option}
        selected={proposal.decision?.optionId === option.id} decisionMode={false} busy={busy} />)}</div>
  </section>
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' })
    .format(new Date(value))
}
