import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { briefApi, type CreateBriefVersion } from '../api/brief-client'
import type { BriefClarification, SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { api, humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'

const CampaignModeField = 'campaignMode'

type IntakeRequest = {
  tenantId: string
  userId: string
  token: string
  sourceTitle: string
  sourceContent: string
  clarifications: BriefClarification[]
}

type IntakeResult =
  | { understanding: SuppliedBriefUnderstanding; planningVersionId: null }
  | { understanding: null; planningVersionId: string }

export function NewBriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected) return
    let active = true
    void api.getCurrentUser()
      .then(current => { if (active) setUser(current.user) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (error && !user) return <MessageState title="Brief setup could not be loaded" message={error} />
  if (!user || !session) return <LoadingState label="Preparing a new Brief" />
  return <BriefCreator tenantId={selected.tenantId} userId={user.id}
    token={session.antiforgeryToken} />
}

function BriefCreator({ tenantId, userId, token }: {
  tenantId: string
  userId: string
  token: string
}) {
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [source, setSource] = useState({ title: '', content: '' })
  const [understanding, setUnderstanding] = useState<SuppliedBriefUnderstanding | null>(null)

  async function run(title: string, content: string, clarifications: BriefClarification[]) {
    setBusy(true); setError(null)
    try {
      const result = await runBriefIntake({
        tenantId, userId, token, sourceTitle: title,
        sourceContent: content, clarifications,
      })
      if (result.understanding) setUnderstanding(result.understanding)
      else navigate(`/planning/${result.planningVersionId}`)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  function submitSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const next = { title: field(values, 'sourceTitle'), content: field(values, 'sourceContent') }
    setSource(next)
    void run(next.title, next.content, [])
  }

  function submitClarifications(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const answers = understanding!.questions.map(question => ({
      fieldPath: question.fieldPath,
      value: field(values, question.fieldPath),
    }))
    void run(source.title, source.content, answers)
  }

  return <section aria-labelledby="new-brief-title" className="brief-intake-page">
    <header className="page-heading"><p className="eyebrow">New campaign</p>
      <h1 id="new-brief-title">Paste or type the Brief</h1>
      <p>Advertified will identify the client, audience, geography, timing and media needed. You will only be asked about details that are unclear.</p></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {understanding
      ? <ClarificationForm understanding={understanding} busy={busy}
          onSubmit={submitClarifications} onEdit={() => setUnderstanding(null)} />
      : <SourceForm busy={busy} onSubmit={submitSource} />}
  </section>
}

async function runBriefIntake(request: IntakeRequest): Promise<IntakeResult> {
  const understanding = await briefApi.understand(request.tenantId, {
    sourceTitle: request.sourceTitle,
    sourceContent: request.sourceContent,
    clarifications: request.clarifications,
  }, request.token)
  if (understanding.requiresHumanClarification) {
    return { understanding, planningVersionId: null }
  }
  if (!understanding.clientName || !understanding.campaignMode) {
    throw new Error('The client and campaign media choice must be clear before planning starts.')
  }
  const brief = await briefApi.create(request.tenantId, {
    clientId: null,
    clientName: understanding.clientName,
    title: understanding.title,
    ownerUserId: request.userId,
    sourceLocator: `supplied:web:${crypto.randomUUID()}`,
    sourceTitle: request.sourceTitle,
    sourceContent: request.sourceContent,
    sourceType: masterDataCodes.briefSourceTypes.suppliedText,
  }, request.token)
  const draft = await briefApi.createVersion(request.tenantId, brief.id,
    draftPayload(brief.id, understanding), request.token)
  const approved = await briefApi.confirm(request.tenantId, draft, request.token)
  const humanResolvedMode = request.clarifications.some(
    item => item.fieldPath === CampaignModeField)
  await planningApi.selectCampaignMode(
    request.tenantId, approved.id, understanding.campaignMode, request.token, {
      source: humanResolvedMode
        ? masterDataCodes.campaignModeDecisionSources.humanClarification
        : masterDataCodes.campaignModeDecisionSources.agent,
      confidence: understanding.campaignModeConfidence,
      reason: understanding.campaignModeRationale,
    })
  return { understanding: null, planningVersionId: approved.id }
}

function SourceForm({ busy, onSubmit }: {
  busy: boolean
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return <form className="brief-source-form detail-card" onSubmit={onSubmit}>
    <label className="field-group">Campaign or Brief name
      <input name="sourceTitle" required maxLength={300} placeholder="For example: Spring furniture campaign" />
    </label>
    <label className="field-group">Original Brief
      <textarea name="sourceContent" required rows={13}
        placeholder="Paste the email, WhatsApp message, tender extract or client Brief here." />
    </label>
    <button className="primary-button" type="submit" disabled={busy}>
      {busy ? 'Understanding the Brief…' : 'Create campaign from Brief'}
    </button>
  </form>
}

function ClarificationForm({ understanding, busy, onSubmit, onEdit }: {
  understanding: SuppliedBriefUnderstanding
  busy: boolean
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onEdit: () => void
}) {
  return <form className="brief-clarification-form detail-card" onSubmit={onSubmit}>
    <div className="brief-clarification-heading"><div>
      <p className="eyebrow">A few details are unclear</p>
      <h2>Answer only what could not be confirmed</h2>
      <p>The rest of the Brief has already been structured for planning.</p>
    </div><button className="text-action" type="button" onClick={onEdit}>Edit original Brief</button></div>
    <div className="brief-question-grid">{understanding.questions.map(question =>
      <label className="field-group" key={question.fieldPath}>{question.question}
        {question.options.length > 0
          ? <select name={question.fieldPath} required defaultValue="">
              <option value="" disabled>Choose one</option>
              {question.options.map(option => <option key={option} value={option}>
                {campaignModeLabel(option)}
              </option>)}
            </select>
          : <input name={question.fieldPath} required maxLength={4000} />}
      </label>)}</div>
    <button className="primary-button" type="submit" disabled={busy}>
      {busy ? 'Applying the answers…' : 'Continue to planning'}
    </button>
  </form>
}

function draftPayload(briefId: string, result: SuppliedBriefUnderstanding): CreateBriefVersion {
  const draft = result.draft
  return {
    briefId, baseVersionId: null, businessProblem: draft.businessProblem,
    objective: draft.objective, audiences: draft.audiences,
    geographies: draft.geographies, timing: draft.timing,
    budgetMinor: draft.budgetMinor, budgetUnknown: draft.budgetUnknown,
    currency: draft.currency, vatStatus: draft.vatStatus, feesMinor: draft.feesMinor,
    constraints: draft.constraints, measurement: draft.measurement, facts: draft.facts,
    unknowns: draft.unknowns, assumptions: draft.assumptions,
    conflicts: draft.conflicts, evidenceItemIds: [],
  }
}

function campaignModeLabel(value: string) {
  return value === masterDataCodes.campaignModes.oohOnly
    ? 'Out-of-home only' : 'Full campaign'
}

function field(values: FormData, name: string): string {
  const result = String(values.get(name) ?? '').trim()
  if (!result) throw new Error('Complete the requested information before continuing.')
  return result
}
