import { useCallback } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { campaignApi } from '../api/campaign-client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { deliveryProofReviewerRoles } from '../campaign/campaign-roles'
import { DeliveryProofCard } from '../campaign/DeliveryProofCard'
import { useResourceRecord } from '../campaign/useResourceRecord'
import { LoadingState, MessageState } from '../components/PageState'

export function DeliveryProofPage() {
  const route = z.guid().safeParse(useParams().proofId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/tasks" replace />
  return <DeliveryProofRecord tenantId={selected.tenantId} proofId={route.data}
    token={session.antiforgeryToken}
    canReview={deliveryProofReviewerRoles.has(selected.roleCode)} />
}

function DeliveryProofRecord({ tenantId, proofId, token, canReview }: {
  tenantId: string
  proofId: string
  token: string
  canReview: boolean
}) {
  const loader = useCallback(
    () => campaignApi.getDeliveryProof(tenantId, proofId),
    [tenantId, proofId],
  )
  const model = useResourceRecord(loader)
  if (model.error && !model.record) {
    return <MessageState title="Delivery proof could not be opened" message={model.error} />
  }
  if (!model.record) return <LoadingState label="Loading delivery proof" />
  return <><CampaignFlowBinding tenantId={tenantId} campaignId={model.record.campaignId} />
  <section className="review-resource-page" aria-labelledby="proof-review-title">
    <Link className="text-action back-link" to="/tasks">← Back to assigned tasks</Link>
    <header className="page-heading"><p className="eyebrow">Delivery evidence</p>
      <h1 id="proof-review-title">Review the exact supplier proof</h1>
      <p>The decision applies only to this immutable evidence record and booked media line.</p></header>
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <DeliveryProofCard tenantId={tenantId} token={token} proof={model.record}
      busy={model.busy} canReview={canReview} run={model.run} />
  </section></>
}
