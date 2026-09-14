import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { api, humanMessage } from '../api/client'
import { proposalApi } from '../api/proposal-client'
import type { Proposal, ProposalApprover, ProposalRecipient, ProposalUpdateInput } from '../api/proposal-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { BriefVersionFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { ProposalAgencyActions } from '../proposal/ProposalAgencyActions'
import { ProposalClientDecision } from '../proposal/ProposalClientDecision'
import { ProposalCommercialProof } from '../proposal/ProposalCommercialProof'
import { ProposalCampaignDirection } from '../proposal/ProposalCampaignDirection'
import { ProposalChoiceComparison } from '../proposal/ProposalChoiceComparison'
import { ProposalEditor } from '../proposal/ProposalEditor'
import { clientMediaCopy } from '../presentation/media-labels'
import { ProposalBrandingPanel } from '../proposal/ProposalBrandingPanel'
import { formatDate, humanizeCode } from '../presentation/format'
import { ProposalOptionCard } from '../proposal/ProposalOptionCard'

const preparationRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

const inventoryImpactComparisonSchema = z.object({
  oldProductName: z.string().optional(),
  oldGeography: z.string().optional(),
  oldRateMinor: z.number().nullable().optional(),
  newProductFound: z.boolean().optional(),
  newProductName: z.string().nullable().optional(),
  newGeography: z.string().nullable().optional(),
  newRateMinor: z.number().nullable().optional(),
  rateChanged: z.boolean().optional(),
  geographyChanged: z.boolean().optional(),
}).passthrough()

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
  return <><BriefVersionFlowBinding tenantId={context.tenantId}
      briefVersionId={state.proposal.briefVersionId} />
    <ProposalContent {...context} {...state} proposal={state.proposal} /></>
}

function useProposalRecord({ tenantId, proposalId, canPrepare }: ProposalContext) {
  const [proposal, setProposal] = useState<Proposal | null>(null)
  const [recipients, setRecipients] = useState<ProposalRecipient[]>([])
  const [approvers, setApprovers] = useState<ProposalApprover[]>([])
  const [currentUserId, setCurrentUserId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const value = await proposalApi.get(tenantId, proposalId)
    setProposal(value); setError(null)
  }, [tenantId, proposalId])
  useEffect(() => {
    let active = true
    const recipientRequest = canPrepare ? proposalApi.listRecipients(tenantId) : Promise.resolve([])
    const approverRequest = canPrepare ? proposalApi.listApprovers(tenantId) : Promise.resolve([])
    const userRequest = canPrepare
      ? api.getCurrentUser().then(result => result.user.id)
      : Promise.resolve(null)
    void Promise.all([
      proposalApi.get(tenantId, proposalId), recipientRequest, approverRequest, userRequest,
    ]).then(([record, clientChoices, approvalChoices, userId]) => {
      if (active) {
        setProposal(record); setRecipients(clientChoices); setApprovers(approvalChoices)
        setCurrentUserId(userId)
      }
    })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, proposalId, canPrepare])
  async function act(action: () => Promise<Proposal>) {
    setBusy(true); setError(null)
    try { await action(); await load() }
    catch (failure) {
      const message = humanMessage(failure)
      try { setProposal(await proposalApi.get(tenantId, proposalId)) }
      catch { /* Keep the command failure visible if recovery loading also fails. */ }
      setError(message)
    }
    finally { setBusy(false) }
  }
  return { proposal, recipients, approvers, currentUserId, error, busy, act }
}

type ProposalContentProps = ProposalContext & {
  proposal: Proposal
  recipients: ProposalRecipient[]
  approvers: ProposalApprover[]
  currentUserId: string | null
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
    <ProposalNavigation canPrepare={canPrepare} hasFunding={Boolean(proposal.decision?.optionId)}
      choiceCount={proposal.options.length} />
    <ProposalCommercialProof proposal={proposal} />
    <ProposalCampaignDirection proposal={proposal} />
    <ProposalChoiceComparison proposal={proposal} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <ProposalInventoryUpdateNotice proposal={proposal} />
    {canPrepare ? <AgencyProposalContent {...props} /> : <ClientProposalContent {...props} />}
    <ProposalFundingNextStep proposal={proposal} canPrepare={canPrepare} />
  </section>
}

function ProposalInventoryUpdateNotice({ proposal }: { proposal: Proposal }) {
  if (proposal.inventoryReviewStatus ===
      masterDataCodes.proposalInventoryReviewStatuses.current) return null
  const open = proposal.inventoryImpacts.filter(item =>
    item.status === masterDataCodes.proposalInventoryImpactStatuses.open)
  return <section className="connected-revision-review" role="alert"
    aria-labelledby="proposal-inventory-update-title">
    <header><div><p className="eyebrow">Revision review & reconfirmation</p>
      <h2 id="proposal-inventory-update-title">Supplier inventory changed after this proposal was created</h2>
      <p>The historical proposal remains unchanged. Review each affected line, update planning against current inventory, then create a replacement proposal version for reconfirmation.</p></div>
      <span>{open.length} open impact{open.length === 1 ? '' : 's'}</span></header>
    <div className="connected-revision-steps"><div className="is-complete"><b>1</b><span><strong>Change detected</strong><small>Supplier release updated</small></span></div>
      <div className="is-active"><b>2</b><span><strong>Review changes</strong><small>Compare old and current supply</small></span></div>
      <div><b>3</b><span><strong>Replan</strong><small>Confirm current media plan</small></span></div>
      <div><b>4</b><span><strong>Reconfirm proposal</strong><small>Create the new governed version</small></span></div></div>
    {proposal.inventoryImpacts.length > 0 && <div className="connected-revision-comparisons">
      {proposal.inventoryImpacts.map(impact => <ProposalImpactCard key={impact.id} impact={impact} />)}
    </div>}
    <footer><Link className="secondary-button" to={`/planning/${proposal.briefVersionId}`}>Review current media plan</Link>
      <Link className="secondary-button" to="/marketplace">Review current inventory</Link>
      <Link className="primary-button" to={`/briefs/${proposal.briefId}/proposals/new?replaces=${proposal.id}`}>
        Create reconfirmed proposal</Link></footer>
  </section>
}

function ProposalImpactCard({ impact }: { impact: Proposal['inventoryImpacts'][number] }) {
  const model = proposalImpactModel(impact)
  return <article className={model.resolved ? 'is-resolved' : ''}>
    <header><div><span>{model.impactType}</span><h3>{model.previousName}</h3></div><em>{model.status}</em></header>
    <div className="connected-revision-compare-grid">
      <RevisionSide title="Previous proposal" name={model.previousName}
        geography={model.previousGeography} note={model.previousNote} />
      <div className="connected-revision-arrow">→</div>
      <RevisionSide title="Current inventory" name={model.currentName}
        geography={model.currentGeography} note={model.currentNote} />
    </div>
    <footer>{model.summary}</footer>
  </article>
}

function proposalImpactModel(impact: Proposal['inventoryImpacts'][number]) {
  const comparison = readInventoryImpact(impact.comparisonJson)
  const rateChanged = Boolean(comparison?.rateChanged)
  return {
    resolved: impact.status === masterDataCodes.proposalInventoryImpactStatuses.resolved,
    impactType: humanizeCode(impact.impactType, true),
    status: humanizeCode(impact.status, true),
    previousName: comparison?.oldProductName ?? 'Affected inventory line',
    previousGeography: comparison?.oldGeography ?? 'Geography not retained in comparison',
    previousNote: rateChanged ? 'Previous commercial rate superseded' : 'Previous commercial terms retained in history',
    currentName: replacementName(comparison),
    currentGeography: comparison?.newGeography ?? 'Review current eligible alternatives',
    currentNote: rateChanged ? 'Current rate requires reconfirmation' : 'No rate change established',
    summary: impactSummary(comparison),
  }
}

function RevisionSide({ title, name, geography, note }: {
  title: string; name: string; geography: string; note: string
}) {
  return <div><small>{title}</small><strong>{name}</strong><p>{geography}</p><span>{note}</span></div>
}

function replacementName(comparison: z.infer<typeof inventoryImpactComparisonSchema> | null) {
  if (!comparison?.newProductFound) return 'Replacement required'
  return comparison.newProductName ?? 'Replacement found'
}

function readInventoryImpact(json: string) {
  try {
    const parsed = inventoryImpactComparisonSchema.safeParse(JSON.parse(json))
    return parsed.success ? parsed.data : null
  } catch {
    return null
  }
}

function impactSummary(comparison: z.infer<typeof inventoryImpactComparisonSchema> | null) {
  if (!comparison) return 'Review the source-linked old and replacement inventory.'
  const changes = [
    comparison.rateChanged ? 'rate changed' : null,
    comparison.geographyChanged ? 'geography changed' : null,
  ].filter(Boolean)
  return changes.length > 0
    ? `Material differences: ${changes.join(', ')}.`
    : 'No automatic material difference was established; human review is still required.'
}

function AgencyProposalContent(props: ProposalContentProps) {
  const { proposal, busy, tenantId, token, act } = props
  const draft = proposal.status === masterDataCodes.lifecycleStatuses.draft &&
    proposal.inventoryReviewStatus ===
      masterDataCodes.proposalInventoryReviewStatuses.current
  return <>
    <div id="proposal-details">{draft ? <ProposalEditor key={`${proposal.id}-${proposal.version}`}
      proposal={proposal} busy={busy} onSave={(input: ProposalUpdateInput) => act(() =>
        proposalApi.update(tenantId, proposal, input, token))} /> :
      <ProposalSummary proposal={proposal} clientView={false} tenantId={tenantId} />}</div>
    <ProposalBrandingPanel tenantId={tenantId} proposal={proposal} token={token} busy={busy}
      onConfigure={input => act(() => proposalApi.configureBranding(
        tenantId, proposal, input, token))}
      onApproveUnbranded={reason => act(() => proposalApi.approveUnbranded(
        tenantId, proposal, reason, token))} />
    <ProposalPreview proposal={proposal} busy={busy} />
    <div id="proposal-action"><ProposalAgencyActions tenantId={tenantId} proposal={proposal}
      recipients={props.recipients} approvers={props.approvers}
      currentUserId={props.currentUserId} busy={busy}
      onSubmitForApproval={approverUserId => act(() => proposalApi.submitForApproval(
        tenantId, proposal, approverUserId, token))}
      onApprove={() => act(() => proposalApi.approve(tenantId, proposal, token))}
      onReject={reason => act(() => proposalApi.rejectApproval(tenantId, proposal, reason, token))}
      onRender={() => act(() => proposalApi.render(tenantId, proposal, token))}
      onShare={recipientUserId => act(() => proposalApi.share(
        tenantId, proposal, recipientUserId, token))}
      onRecordExternalDecision={(optionId, evidenceReference, reason) => act(() =>
        proposalApi.recordExternalDecision(tenantId, proposal, {
          optionId,
          declined: optionId === null,
          evidenceReference,
          reason: reason || null,
        }, token))} /></div>
  </>
}

function ClientProposalContent(props: ProposalContentProps) {
  const { proposal, busy, tenantId, token, act } = props
  return <>
    <div id="proposal-details"><ProposalSummary proposal={proposal} clientView tenantId={tenantId} /></div>
    <div id="proposal-options"><ProposalClientDecision proposal={proposal} busy={busy}
      onSelect={optionId => act(() => proposalApi.selectOption(
        tenantId, proposal, optionId, token))}
      onDecline={() => act(() => proposalApi.decline(tenantId, proposal, token))} /></div>
  </>
}

function ProposalHero({ proposal }: { proposal: Proposal }) {
  return <header className="proposal-hero proposal-record-hero"><div>
    <p className="eyebrow eyebrow-light">Media proposal</p>
    <h1 id="proposal-title">{clientMediaCopy(proposal.title)}</h1><p>{clientMediaCopy(proposal.executiveSummary)}</p></div>
    <dl className="proposal-record-metrics">
      <div><dt>Status</dt><dd><span className="status-chip">{humanizeCode(proposal.status, true)}</span></dd></div>
      <div><dt>Client choices</dt><dd>{proposal.options.length}</dd></div>
      <div><dt>Version</dt><dd>{proposal.versionNumber}</dd></div>
      <div><dt>Valid until</dt><dd>{formatDate(proposal.expiryAtUtc)}</dd></div>
    </dl>
  </header>
}

function ProposalNavigation({ canPrepare, hasFunding, choiceCount }: {
  canPrepare: boolean
  hasFunding: boolean
  choiceCount: number
}) {
  return <nav className="proposal-navigation" aria-label="Proposal sections">
    <a href="#proposal-details">Summary and wording</a>
    {canPrepare && <a href="#proposal-branding">Branding</a>}
    {choiceCount > 1 && <a href="#proposal-comparison">Compare choices</a>}
    <a href="#proposal-options">Client choices</a>
    {canPrepare && <a href="#proposal-action">Next action</a>}
    {hasFunding && <a href="#proposal-funding">Funding handoff</a>}
  </nav>
}

function ProposalSummary({ proposal, clientView, tenantId }: {
  proposal: Proposal
  clientView: boolean
  tenantId: string
}) {
  return <section className={`proposal-section ${clientView ? 'proposal-client-summary' : 'proposal-summary-card'}`}>
    <p className="eyebrow">{clientView ? 'Campaign direction' : 'Executive summary'}</p>
    <h2>{clientMediaCopy(proposal.executiveSummary)}</h2><p>{proposal.terms}</p>
    {clientView && proposal.document && <a className="secondary-button" target="_blank" rel="noreferrer"
      href={proposalApi.documentUrl(tenantId, proposal.document.id)}>Open proposal PDF</a>}
  </section>
}

function ProposalPreview({ proposal, busy }: { proposal: Proposal; busy: boolean }) {
  return <section className="proposal-section" id="proposal-options" aria-labelledby="proposal-preview-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Proposal preview</p>
      <h2 id="proposal-preview-title">Client choices</h2>
      <p>Only approved plan totals and client-safe facts are shown.</p></div></div>
    <div className="proposal-options-grid">{proposal.options.map(option =>
      <ProposalOptionCard key={option.id} option={option}
        selected={proposal.decision?.optionId === option.id} decisionMode={false} busy={busy} />)}</div>
  </section>
}

function ProposalFundingNextStep({ proposal, canPrepare }: {
  proposal: Proposal
  canPrepare: boolean
}) {
  const selectedId = proposal.decision?.optionId
  if (!selectedId) return null
  const option = proposal.options.find(item => item.id === selectedId)
  if (!option) return null
  const query = new URLSearchParams({
    proposalVersionId: proposal.id,
    proposalOptionId: option.id,
    amountMinor: String(option.budgetMinor),
    currency: option.currency,
  })
  return <article className="proposal-funding-next" id="proposal-funding"><div><p className="eyebrow eyebrow-light">Client decision recorded</p>
    <h2>{canPrepare ? 'Continue with purchase order and funding' : 'The selected option is ready for funding'}</h2>
    <p>{canPrepare
      ? 'Funding remains tied to this exact option and approved commercial version.'
      : 'The agency can now reconcile the purchase order and payment before campaign delivery begins.'}</p>
    {canPrepare && proposal.decision?.recordedForExternalParty && <small>
      Verified external reply from {proposal.decision.externalPartyEmail} · Evidence: {proposal.decision.evidenceReference}
    </small>}</div>
    {canPrepare
      ? <Link className="primary-button" to={`/funding?${query}`}>Open funding <span aria-hidden="true">→</span></Link>
      : <span className="status-chip status-positive">Selection retained</span>}
  </article>
}
