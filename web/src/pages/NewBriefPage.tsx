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
import { LoadingState, MessageState } from '../components/PageState'

export function NewBriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session) return <Navigate to="/sign-in" replace />
  return <BriefSession key={`${selected.membershipId}:${selected.version}:${session.antiforgeryToken}`}
    tenantId={selected.tenantId} token={session.antiforgeryToken} />
}

function BriefSession({ tenantId, token }: { tenantId: string; token: string }) {
  const user = useCurrentUser(true)
  if (user.error && !user.value) {
    return <MessageState title="Brief setup could not be loaded" message={user.error} />
  }
  if (!user.value) return <LoadingState label="Preparing a new Brief" />
  return <BriefCreator tenantId={tenantId} userId={user.value.id} token={token} />
}

function BriefCreator({ tenantId, userId, token }: {
  tenantId: string
  userId: string
  token: string
}) {
  const model = useBriefIntake({ tenantId, userId, token })
  return <><CampaignModeBinding mode={model.understanding?.campaignMode ?? null} />
  <section aria-labelledby="new-brief-title" className="brief-intake-page connected-campaign-page">
    {!model.understanding && <BriefIntakeHeading />}
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    {!model.understanding
      ? <div className="brief-source-workbench connected-brief-workbench">
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
            audienceResearch={model.audienceResearch} onAudienceResearchChange={model.setAudienceResearch}
            onSpatialRequirementsChange={model.setSpatialRequirements} />}
  </section></>
}

function BriefIntakeHeading() {
  return <header className="page-heading brief-intake-heading connected-stage-heading"><div>
    <p className="eyebrow">New campaign</p>
    <h1 id="new-brief-title">Start with your full campaign brief</h1>
    <p>Tell us about your campaign. You can paste the client's original Brief and add the structured details you already know.</p>
  </div><div className="connected-handwritten-note" aria-hidden="true">Turn ideas<br />into impact.<span /></div></header>
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
