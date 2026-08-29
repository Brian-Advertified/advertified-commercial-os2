import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { briefApi, type CreateBriefVersion } from '../api/brief-client'
import { api, humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { ClientAccount, CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

export function NewBriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const [clients, setClients] = useState<ClientAccount[] | null>(null)
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected) return
    let active = true
    void Promise.all([opportunityApi.listClients(selected.tenantId), api.getCurrentUser()])
      .then(([availableClients, current]) => {
        if (active) { setClients(availableClients); setUser(current.user) }
      }).catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (error && !clients) return <MessageState title="Brief setup could not be loaded" message={error} />
  if (!clients || !user || !session) return <LoadingState label="Preparing a new Brief" />
  return <BriefCreator tenantId={selected.tenantId} clients={clients} user={user}
    token={session.antiforgeryToken} />
}

function BriefCreator({ tenantId, clients, user, token }: {
  tenantId: string
  clients: ClientAccount[]
  user: CurrentUser
  token: string
}) {
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(null)
    try {
      const values = new FormData(event.currentTarget)
      const title = value(values, 'title')
      const sourceContent = value(values, 'sourceContent')
      const brief = await briefApi.create(tenantId, {
        clientId: value(values, 'clientId'), title, ownerUserId: user.id,
        sourceLocator: `supplied:web:${crypto.randomUUID()}`,
        sourceTitle: `${title} supplied source`, sourceContent,
      }, token)
      await briefApi.createVersion(tenantId, brief.id, draftPayload(brief.id, values), token)
      navigate(`/briefs/${brief.id}`)
    } catch (failure) {
      setError(humanMessage(failure)); setBusy(false)
    }
  }

  return <section aria-labelledby="new-brief-title">
    <header className="page-heading"><p className="eyebrow">Supplied client Brief</p>
      <h1 id="new-brief-title">Understand a new Brief</h1>
      <p>Keep the original words, label what is missing, and confirm one exact version.</p></header>
    {clients.length === 0 ? <MessageState title="No client is available"
      message="Ask a workspace administrator to assign a client before creating a Brief." />
      : <BriefForm clients={clients} busy={busy} error={error} submit={submit} />}
  </section>
}

function BriefForm({ clients, busy, error, submit }: {
  clients: ClientAccount[]
  busy: boolean
  error: string | null
  submit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return <form className="brief-form detail-card" onSubmit={submit}>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <label className="field-group">Client<select name="clientId" required>
      {clients.map((client) => <option value={client.id} key={client.id}>{client.tradingName}</option>)}
    </select></label>
    <label className="field-group">Brief title<input name="title" required maxLength={300} /></label>
    <label className="field-group field-wide">Original client wording
      <textarea name="sourceContent" required rows={7} /></label>
    <label className="field-group field-wide">Business problem
      <textarea name="businessProblem" required /></label>
    <label className="field-group field-wide">Objective<textarea name="objective" required /></label>
    <label className="field-group">Audience direction<input name="audiences" /></label>
    <label className="field-group">Geography<input name="geographies" /></label>
    <label className="field-group">Timing<input name="timing" required /></label>
    <label className="field-group">Budget in rand<input name="budget" type="number" min="0" step="0.01" /></label>
    <label className="field-group">Constraints<input name="constraints" /></label>
    <label className="field-group">How success will be measured<input name="measurement" /></label>
    <button className="primary-button" type="submit" disabled={busy}>
      {busy ? 'Understanding Brief…' : 'Understand this Brief'}
    </button>
  </form>
}

function draftPayload(briefId: string, values: FormData): CreateBriefVersion {
  const budget = value(values, 'budget', false)
  const audiences = list(values, 'audiences')
  const geographies = list(values, 'geographies')
  const unknowns = [
    ...(!budget ? [{ fieldPath: 'budget', question: 'What budget is available?', isBlocking: false }] : []),
    ...(audiences.length === 0 ? [{ fieldPath: 'audiences', question: 'Who should this reach?', isBlocking: false }] : []),
    ...(geographies.length === 0 ? [{ fieldPath: 'geographies', question: 'Where must this run?', isBlocking: false }] : []),
  ]
  return {
    briefId, baseVersionId: null, businessProblem: value(values, 'businessProblem'),
    objective: value(values, 'objective'), audiences, geographies,
    timing: value(values, 'timing'), budgetMinor: budget ? Math.round(Number(budget) * 100) : null,
    budgetUnknown: !budget, currency: budget ? opportunityCodes.currency.zar : null,
    vatStatus: null, feesMinor: null,
    constraints: list(values, 'constraints'), measurement: list(values, 'measurement'),
    facts: [], unknowns, assumptions: [], conflicts: [], evidenceItemIds: [],
  }
}

function value(values: FormData, name: string, required = true): string {
  const result = String(values.get(name) ?? '').trim()
  if (required && !result) throw new Error(`${name} is required`)
  return result
}

function list(values: FormData, name: string): string[] {
  const text = value(values, name, false)
  return text ? text.split(',').map((item) => item.trim()).filter(Boolean) : []
}
