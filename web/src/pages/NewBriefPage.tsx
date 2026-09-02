import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import type { CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { BriefIntakeGuide } from '../brief/BriefIntakeGuide'
import { BriefClarificationForm } from '../brief-intake/BriefClarificationForm'
import { BriefSourceForm } from '../brief-intake/BriefSourceForm'
import { BriefUnderstandingReview } from '../brief-intake/BriefUnderstandingReview'
import { useBriefIntake } from '../brief-intake/useBriefIntake'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'

export function NewBriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const user = useCurrentUser(Boolean(selected))
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (user.error && !user.value) {
    return <MessageState title="Brief setup could not be loaded" message={user.error} />
  }
  if (!user.value || !session) return <LoadingState label="Preparing a new Brief" />
  return <BriefCreator tenantId={selected.tenantId} userId={user.value.id}
    token={session.antiforgeryToken} />
}

function BriefCreator({ tenantId, userId, token }: {
  tenantId: string
  userId: string
  token: string
}) {
  const model = useBriefIntake({ tenantId, userId, token })
  return <><CampaignModeBinding mode={model.understanding?.campaignMode ?? null} />
  <section aria-labelledby="new-brief-title" className="brief-intake-page">
    <BriefIntakeHeading />
    <div className="brief-integrity-strip" role="note" aria-label="Brief source integrity">
      <Icon name="shield" />
      <strong>Original wording preserved</strong>
      <span>The supplied request remains the source of truth while Advertified structures it for planning.</span>
    </div>
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    {!model.understanding
      ? <div className="brief-source-workbench">
          <BriefSourceForm busy={model.busy} source={model.source}
            onSubmit={model.submitSource} />
          <BriefIntakeGuide understanding={null} busy={model.busy} />
        </div>
      : model.understanding.requiresHumanClarification
        ? <BriefClarificationForm understanding={model.understanding} busy={model.busy}
            onSubmit={model.submitClarifications} onEdit={model.editSource} />
        : <BriefUnderstandingReview understanding={model.understanding} busy={model.busy}
            onApprove={model.approveReview} onEdit={model.editSource}
            onCorrectMode={model.correctMode}
            spatialRequirements={model.spatialRequirements}
            onSpatialRequirementsChange={model.setSpatialRequirements} />}
  </section></>
}

function BriefIntakeHeading() {
  return <header className="page-heading brief-intake-heading"><div>
    <p className="eyebrow">New campaign</p>
    <h1 id="new-brief-title">Start with the Brief, not a form</h1>
    <p>Paste the client request in its original wording. Advertified structures the campaign, identifies the media scope and asks only about material details that remain unclear.</p>
  </div><span className="brief-heading-state">Source-first intake</span></header>
}

function useCurrentUser(enabled: boolean) {
  const [value, setValue] = useState<CurrentUser | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (!enabled) return
    let active = true
    void api.getCurrentUser()
      .then(result => { if (active) setValue(result.user) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [enabled])
  return { value, error }
}
