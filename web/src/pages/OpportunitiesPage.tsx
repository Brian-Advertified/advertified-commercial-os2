import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { ClientAccount, Opportunity } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

export function OpportunitiesPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <OpportunityList key={selected.tenantId} tenantId={selected.tenantId} />
}

function OpportunityList({ tenantId }: { tenantId: string }) {
  const model = useOpportunityIndex(tenantId)
  if (model.error && !model.items) {
    return <MessageState title="Opportunities could not be loaded" message={model.error} />
  }
  if (!model.items) return <LoadingState label="Loading opportunities" />
  return <OpportunityIndexContent {...model} items={model.items} />
}

function useOpportunityIndex(tenantId: string) {
  const { session } = useSession()
  const navigate = useNavigate()
  const [items, setItems] = useState<Opportunity[] | null>(null)
  const [clients, setClients] = useState<ClientAccount[]>([])
  const [ownerId, setOwnerId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)

  useEffect(() => {
    let active = true
    void loadIndex(tenantId).then(([opportunities, accounts, currentOwner]) => {
      if (!active) return
      setItems(opportunities); setClients(accounts); setOwnerId(currentOwner)
    }).catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId])

  async function create(values: FormData) {
    if (!session || !ownerId) return
    setCreating(true); setError(null)
    try {
      const created = await opportunityApi.create(tenantId, {
        clientId: String(values.get('clientId')),
        title: String(values.get('title')),
        sourceType: 'DISCOVERY', sourceRef: 'workspace-entry', ownerUserId: ownerId,
        problemSummary: optional(values.get('problemSummary')),
        objectiveSummary: optional(values.get('objectiveSummary')),
      }, session.antiforgeryToken)
      navigate(`/opportunities/${created.id}`)
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setCreating(false)
    }
  }
  return { items, clients, error, creating, create }
}

async function loadIndex(tenantId: string): Promise<[Opportunity[], ClientAccount[], string]> {
  const [opportunities, accounts, current] = await Promise.all([
    opportunityApi.list(tenantId), opportunityApi.listClients(tenantId), api.getCurrentUser(),
  ])
  return [opportunities, accounts, current.user.id]
}

function OpportunityIndexContent({ items, clients, error, creating, create }: {
  items: Opportunity[]; clients: ClientAccount[]; error: string | null; creating: boolean
  create: (values: FormData) => Promise<void>
}) {
  return (
    <section aria-labelledby="opportunities-title">
      <header className="page-heading page-heading-split">
        <div><p className="eyebrow">Evidence-led qualification</p><h1 id="opportunities-title">Opportunities</h1>
          <p>Turn real supplied sources into an approved strategy without skipping human review.</p></div>
        <span className="status-chip">{items.length} visible</span>
      </header>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <div className="opportunity-layout">
        <OpportunityCards items={items} />
        <CreateOpportunityForm clients={clients} creating={creating} create={create} />
      </div>
    </section>
  )
}

function OpportunityCards({ items }: { items: Opportunity[] }) {
  return <div className="record-stack" aria-label="Opportunity list">
    {items.length === 0 && <article className="detail-card"><h2>No opportunities yet</h2><p>Create the first qualification record.</p></article>}
    {items.map((item) => (
      <Link className="record-card" to={`/opportunities/${item.id}`} key={item.id}>
        <div><span className="status-chip">{label(item.stage)}</span><h2>{item.title}</h2></div>
        <p>{item.objectiveSummary ?? 'Objective not supplied'}</p>
        <span className="record-arrow" aria-hidden="true">→</span>
      </Link>
    ))}
  </div>
}

function CreateOpportunityForm({ clients, creating, create }: {
  clients: ClientAccount[]; creating: boolean; create: (values: FormData) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); void create(new FormData(event.currentTarget))
  }
  return <form className="detail-card opportunity-form" onSubmit={submit}>
    <p className="eyebrow">New discovery</p><h2>Create opportunity</h2>
    <Field label="Title" name="title" required />
    <label className="field-group">Client account<select name="clientId" required defaultValue="">
      <option value="" disabled>Choose a client</option>
      {clients.map((client) => <option value={client.id} key={client.id}>{client.tradingName}</option>)}
    </select></label>
    <Field label="Problem summary" name="problemSummary" />
    <Field label="Objective summary" name="objectiveSummary" />
    <button className="primary-button" disabled={creating || clients.length === 0}>
      {creating ? 'Creating…' : 'Create opportunity'}
    </button>
    {clients.length === 0 && <small>Create or obtain access to a client account first.</small>}
  </form>
}

function Field({ label: text, name, required = false }: { label: string; name: string; required?: boolean }) {
  return <label className="field-group">{text}<input name={name} required={required} /></label>
}

function optional(value: FormDataEntryValue | null): string | null {
  const normalized = String(value ?? '').trim()
  return normalized || null
}

function label(code: string): string { return code.toLowerCase().replaceAll('_', ' ') }
