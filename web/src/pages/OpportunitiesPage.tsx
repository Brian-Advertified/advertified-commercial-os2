import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { ClientAccount, Opportunity } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'

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
        sourceType: masterDataCodes.opportunitySourceTypes.discovery,
        sourceRef: 'workspace-entry', ownerUserId: ownerId,
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
    <section className="approved-opportunity-page" aria-labelledby="opportunities-title">
      <header className="approved-work-index-header">
        <div><p className="eyebrow">Evidence-led qualification</p>
          <h1 id="opportunities-title">Opportunities</h1>
          <p>Develop a real commercial opening from retained evidence before it becomes a campaign Brief.</p></div>
        <Link className="primary-button" to="/briefs/new">+ New supplied Brief</Link>
      </header>
      <dl className="approved-opportunity-metrics" aria-label="Opportunity context">
        <Metric label="Visible opportunities" value={String(items.length)} note="Current workspace" />
        <Metric label="Client accounts" value={String(clients.length)} note="Available to this workspace" />
        <Metric label="Qualification path" value="Evidence → Strategy" note="Before the Campaign Brief" />
      </dl>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <div className="approved-opportunity-layout">
        <OpportunityTable items={items} clients={clients} />
        <CreateOpportunityForm clients={clients} creating={creating} create={create} />
      </div>
    </section>
  )
}

function Metric({ label, value, note }: { label: string; value: string; note: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{note}</small></div>
}

function OpportunityTable({ items, clients }: { items: Opportunity[]; clients: ClientAccount[] }) {
  const clientNames = new Map(clients.map((client) => [client.id, client.tradingName]))
  return <section className="approved-panel approved-opportunity-list" aria-labelledby="opportunity-register-title">
    <header><div><h2 id="opportunity-register-title">Current opportunities</h2>
      <p>Evidence-led commercial work that has not yet become a campaign Brief.</p></div>
      <span className="status-chip">{items.length} visible</span></header>
    {items.length === 0 ? <div className="approved-work-index-empty">
      <strong>No opportunities yet</strong><p>Create one only when there is a real commercial opening to qualify.</p>
    </div> : <div className="operations-table-scroll"><table className="operations-table">
      <thead><tr><th>Opportunity</th><th>Client</th><th>Stage</th><th>Updated</th><th><span className="sr-only">Open</span></th></tr></thead>
      <tbody>{items.map((item) => <tr key={item.id}>
        <td><Link to={`/opportunities/${item.id}`}><strong>{item.title}</strong></Link>
          <small>{item.objectiveSummary ?? 'Objective not supplied'}</small></td>
        <td>{clientNames.get(item.clientId) ?? 'Client account unavailable'}</td>
        <td><span className="operations-state-label">{humanizeCode(item.stage)}</span></td>
        <td>{formatDateTime(item.updatedAtUtc)}</td>
        <td><Link className="operations-row-action" to={`/opportunities/${item.id}`} aria-label={`Open ${item.title}`}>→</Link></td>
      </tr>)}</tbody>
    </table></div>}
  </section>
}

function CreateOpportunityForm({ clients, creating, create }: {
  clients: ClientAccount[]; creating: boolean; create: (values: FormData) => Promise<void>
}) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); void create(new FormData(event.currentTarget))
  }
  return <aside className="approved-panel approved-opportunity-create">
    <header><div><h2>Create opportunity</h2>
      <p>Use this only when there is no supplied Brief yet and a commercial opening needs qualification.</p></div></header>
    <form className="approved-opportunity-form" onSubmit={submit}>
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
    <p className="approved-opportunity-note">Already have the client’s Brief? <Link to="/briefs/new">Paste or upload the source directly</Link>. A supplied Brief does not need this Opportunity form.</p>
  </aside>
}

function Field({ label, name, required = false }: { label: string; name: string; required?: boolean }) {
  return <label className="field-group">{label}<input name={name} required={required} /></label>
}

function optional(value: FormDataEntryValue | null): string | null {
  const normalized = String(value ?? '').trim()
  return normalized || null
}
