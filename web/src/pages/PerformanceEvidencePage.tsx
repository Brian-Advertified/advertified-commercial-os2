import { useCallback } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { campaignApi } from '../api/campaign-client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { performanceEvidenceReviewerRoles } from '../campaign/campaign-roles'
import { PerformanceEvidenceCard } from '../campaign/PerformanceEvidenceCard'
import { useResourceRecord } from '../campaign/useResourceRecord'
import { LoadingState, MessageState } from '../components/PageState'

export function PerformanceEvidencePage() {
  const route = z.guid().safeParse(useParams().evidenceId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/tasks" replace />
  return <PerformanceEvidenceRecord tenantId={selected.tenantId} evidenceId={route.data}
    token={session.antiforgeryToken}
    canReview={performanceEvidenceReviewerRoles.has(selected.roleCode)} />
}

function PerformanceEvidenceRecord({ tenantId, evidenceId, token, canReview }: {
  tenantId: string
  evidenceId: string
  token: string
  canReview: boolean
}) {
  const loader = useCallback(
    () => campaignApi.getPerformanceEvidence(tenantId, evidenceId),
    [tenantId, evidenceId],
  )
  const model = useResourceRecord(loader)
  if (model.error && !model.record) {
    return <MessageState title="Performance evidence could not be opened" message={model.error} />
  }
  if (!model.record) return <LoadingState label="Loading performance evidence" />
  return <section className="review-resource-page" aria-labelledby="evidence-review-title">
    <Link className="text-action back-link" to="/tasks">← Back to assigned tasks</Link>
    <header className="page-heading"><p className="eyebrow">Sourced performance facts</p>
      <h1 id="evidence-review-title">Review the measurement evidence</h1>
      <p>Approve only the exact facts supported by the retained source, method and visible limitations.</p></header>
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <PerformanceEvidenceCard tenantId={tenantId} token={token} evidence={model.record}
      busy={model.busy} canReview={canReview} run={model.run} />
  </section>
}
