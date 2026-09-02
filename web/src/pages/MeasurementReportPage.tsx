import { useCallback } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { campaignApi } from '../api/campaign-client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { measurementReportReviewerRoles } from '../campaign/campaign-roles'
import { MeasurementReportCard } from '../campaign/MeasurementReportCard'
import { useResourceRecord } from '../campaign/useResourceRecord'
import { LoadingState, MessageState } from '../components/PageState'

export function MeasurementReportPage() {
  const route = z.guid().safeParse(useParams().reportId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/tasks" replace />
  return <MeasurementReportRecord tenantId={selected.tenantId} reportId={route.data}
    token={session.antiforgeryToken}
    canReview={measurementReportReviewerRoles.has(selected.roleCode)} />
}

function MeasurementReportRecord({ tenantId, reportId, token, canReview }: {
  tenantId: string
  reportId: string
  token: string
  canReview: boolean
}) {
  const loader = useCallback(
    () => campaignApi.getMeasurementReport(tenantId, reportId),
    [tenantId, reportId],
  )
  const model = useResourceRecord(loader)
  if (model.error && !model.record) {
    return <MessageState title="Measurement report could not be opened" message={model.error} />
  }
  if (!model.record) return <LoadingState label="Loading measurement report" />
  return <><CampaignFlowBinding tenantId={tenantId} campaignId={model.record.campaignId} />
  <section className="review-resource-page" aria-labelledby="report-review-title">
    <Link className="text-action back-link" to="/tasks">← Back to assigned tasks</Link>
    <header className="page-heading"><p className="eyebrow">Client measurement report</p>
      <h1 id="report-review-title">Review the sourced interpretation</h1>
      <p>Canonical metric values remain unchanged; the report may only interpret approved facts and retained limitations.</p></header>
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <MeasurementReportCard tenantId={tenantId} token={token} report={model.record}
      busy={model.busy} canReview={canReview} run={model.run} />
  </section></>
}
