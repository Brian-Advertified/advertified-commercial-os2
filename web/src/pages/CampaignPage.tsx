import { useEffect, useRef, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { BriefVersionFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { BookingReadinessSection } from '../campaign/BookingReadinessSection'
import { CampaignDeliveryRail } from '../campaign/CampaignDeliveryRail'
import { CampaignFundingSummary } from '../campaign/CampaignFundingSummary'
import { CampaignHeader } from '../campaign/CampaignHeader'
import { CampaignCommercialProof } from '../campaign/CampaignCommercialProof'
import {
  campaignBookingConfirmerRoles,
  campaignDeliveryOperatorRoles,
  campaignViewerRoles,
  creativeApproverRoles,
  creativeBrandReviewerRoles,
  creativeRequesterRoles,
  creativeUploaderRoles,
  deliveryProofReviewerRoles,
  measurementReportGeneratorRoles,
  measurementReportReviewerRoles,
  performanceEvidenceReviewerRoles,
  performanceEvidenceSubmitterRoles,
} from '../campaign/campaign-roles'
import { CreativeSection } from '../campaign/CreativeSection'
import {
  campaignDeliveryStageId,
  campaignDeliveryTabFromHash,
  currentCampaignDeliveryTab,
  type CampaignDeliveryTab,
} from '../campaign/campaign-delivery-stages'
import { DeliveryProofSection, LiveDeliverySection } from '../campaign/DeliverySection'
import { MeasurementSection } from '../campaign/MeasurementSection'
import type { CampaignActionRunner } from '../campaign/campaign-types'
import {
  useCampaignWorkspace,
  type CampaignWorkspaceModel,
} from '../campaign/useCampaignWorkspace'
import { LoadingState, MessageState } from '../components/PageState'

export function CampaignPage() {
  const route = z.guid().safeParse(useParams().campaignId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/campaigns" replace />
  if (!campaignViewerRoles.has(selected.roleCode)) {
    return <MessageState title="Campaign delivery is not available"
      message="This workspace role cannot view this campaign delivery workspace." />
  }
  return <CampaignRecord tenantId={selected.tenantId} campaignId={route.data}
    token={session.antiforgeryToken} roleCode={selected.roleCode} />
}

function CampaignRecord({ tenantId, campaignId, token, roleCode }: {
  tenantId: string
  campaignId: string
  token: string
  roleCode: string
}) {
  const needsReviewers = performanceEvidenceSubmitterRoles.has(roleCode) ||
    measurementReportGeneratorRoles.has(roleCode)
  const state = useCampaignWorkspace(tenantId, campaignId, needsReviewers)
  if (state.error && !state.model) {
    return <MessageState title="Campaign could not be opened" message={state.error} />
  }
  if (!state.model) return <LoadingState label="Loading campaign delivery" />
  return <><BriefVersionFlowBinding tenantId={tenantId}
      briefVersionId={state.model.campaign.briefVersionId} />
    <CampaignWorkspace tenantId={tenantId} token={token} roleCode={roleCode}
      model={state.model} busy={state.busy} error={state.error} run={state.run} /></>
}

type WorkspaceProps = {
  tenantId: string
  token: string
  roleCode: string
  model: CampaignWorkspaceModel
  busy: boolean
  error: string | null
  run: CampaignActionRunner
}

function CampaignWorkspace(props: WorkspaceProps) {
  const currentTab = currentCampaignDeliveryTab(props.model.campaign)
  const [activeTab, setActiveTab] = useState<CampaignDeliveryTab>(() =>
    campaignDeliveryTabFromHash(window.location.hash) ?? currentTab)
  const previousCurrentTab = useRef(currentTab)
  useEffect(() => {
    if (previousCurrentTab.current === currentTab) return
    previousCurrentTab.current = currentTab
    setActiveTab(currentTab)
    window.history.replaceState(null, '', `#${campaignDeliveryStageId(currentTab)}`)
  }, [currentTab])
  useEffect(() => {
    const selectHashTab = () => {
      const hashTab = campaignDeliveryTabFromHash(window.location.hash)
      if (hashTab) setActiveTab(hashTab)
    }
    window.addEventListener('hashchange', selectHashTab)
    return () => window.removeEventListener('hashchange', selectHashTab)
  }, [])

  const presentation = campaignStagePresentation(activeTab)
  return <section className="campaign-workspace-page connected-campaign-workspace" aria-labelledby="campaign-title">
    <Link className="text-action connected-campaign-back" to="/campaigns">← Back to Campaigns</Link>
    <header className="connected-stage-heading connected-campaign-stage-heading"><div><p className="eyebrow">{presentation.eyebrow}</p>
      <h1>{presentation.title}</h1><p>{presentation.copy}</p></div>
      <div className="connected-handwritten-note" aria-hidden="true">{presentation.note[0]}<br />{presentation.note[1]}<span /></div></header>
    <CampaignHeader campaign={props.model.campaign} />
    <CampaignDeliveryRail campaign={props.model.campaign} activeTab={activeTab}
      onSelect={setActiveTab} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <div className="campaign-tab-panel">
      <CampaignTabContent {...props} activeTab={activeTab} />
    </div>
    <details className="connected-campaign-proof"><summary>View commercial lineage and readiness evidence</summary>
      <CampaignCommercialProof model={props.model} /></details>
  </section>
}

function campaignStagePresentation(tab: CampaignDeliveryTab) {
  if (tab === 'bookings' || tab === 'funding') return {
    eyebrow: 'Campaign readiness', title: 'Approvals & Booking',
    copy: 'Track approvals, confirm selected inventory with suppliers, and prepare the campaign for production.',
    note: ['From approval', 'to impact.'] as const,
  }
  if (tab === 'creativeStage') return {
    eyebrow: 'Campaign readiness', title: 'Creative & launch readiness',
    copy: 'Bring booked media, creative requirements and approvals together before launch.',
    note: ['Ready means', 'ready.'] as const,
  }
  if (tab === 'live') return {
    eyebrow: 'Launch campaign', title: 'Launch campaign',
    copy: 'Monitor the governed launch state, booked media, active markets and delivery actions across the campaign.',
    note: ['From strategy', 'to street impact.'] as const,
  }
  if (tab === 'proof') return {
    eyebrow: 'Delivery accountability', title: 'Delivery proof',
    copy: 'Review evidence linked to the exact booked media line and keep gaps visible until they are resolved.',
    note: ['Proof over', 'promises.'] as const,
  }
  return {
    eyebrow: 'Campaign insights', title: 'Campaign reporting',
    copy: 'Turn reviewed performance evidence into campaign findings, limitations and approved learnings.',
    note: ['Turn insights', 'into opportunity.'] as const,
  }
}

function CampaignTabContent(props: WorkspaceProps & { activeTab: CampaignDeliveryTab }) {
  const { campaign, bookings, reviewers } = props.model
  const common = {
    tenantId: props.tenantId,
    token: props.token,
    campaign,
    busy: props.busy,
    run: props.run,
  }
  if (props.activeTab === 'funding') return <CampaignFundingSummary campaign={campaign} />
  if (props.activeTab === 'bookings') return <BookingReadinessSection {...common}
    bookings={bookings} canConfirm={campaignBookingConfirmerRoles.has(props.roleCode)} />
  if (props.activeTab === 'creativeStage') return <CreativeSection {...common} bookings={bookings}
    canRequest={creativeRequesterRoles.has(props.roleCode)}
    canUpload={creativeUploaderRoles.has(props.roleCode)}
    canBrandReview={creativeBrandReviewerRoles.has(props.roleCode)}
    canApprove={creativeApproverRoles.has(props.roleCode)} />
  if (props.activeTab === 'live') return <LiveDeliverySection {...common} bookings={bookings}
    canOperate={campaignDeliveryOperatorRoles.has(props.roleCode)} />
  if (props.activeTab === 'proof') return <DeliveryProofSection {...common} bookings={bookings}
    canReviewProof={deliveryProofReviewerRoles.has(props.roleCode)} />
  return <MeasurementSection {...common} reviewers={reviewers}
    canSubmitEvidence={performanceEvidenceSubmitterRoles.has(props.roleCode)}
    canReviewEvidence={performanceEvidenceReviewerRoles.has(props.roleCode)}
    canGenerateReport={measurementReportGeneratorRoles.has(props.roleCode)}
    canReviewReport={measurementReportReviewerRoles.has(props.roleCode)} />
}
