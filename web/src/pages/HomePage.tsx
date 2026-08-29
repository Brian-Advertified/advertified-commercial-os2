import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import type { Tenant, Workspace } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'

type Counts = { clientAccounts: number | null; agencies: number | null; contacts: number | null }

export function HomePage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <HomeData key={selected.tenantId} workspace={selected} />
}

function HomeData({ workspace }: { workspace: Workspace }) {
  const [tenant, setTenant] = useState<Tenant | null>(null)
  const [counts, setCounts] = useState<Counts | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void Promise.all([
      api.getTenant(workspace.tenantId),
      api.getFoundationCounts(workspace.tenantId),
    ]).then(([tenantResult, countResult]) => {
      if (active) { setTenant(tenantResult); setCounts(countResult) }
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    })
    return () => { active = false }
  }, [workspace.tenantId])

  if (error) return <MessageState title="Your workspace could not be opened" message={error} />
  if (!tenant || !counts) return <LoadingState label={`Preparing ${workspace.name}`} />
  return <HomeContent tenant={tenant} counts={counts} />
}

function HomeContent({ tenant, counts }: { tenant: Tenant; counts: Counts }) {
  const cards = [
    { label: 'Client accounts', value: counts.clientAccounts },
    { label: 'Agencies', value: counts.agencies },
    { label: 'Contacts', value: counts.contacts },
  ]
  return (
    <section aria-labelledby="home-title">
      <header className="page-heading page-heading-split">
        <div>
          <p className="eyebrow">Foundation overview</p>
          <h1 id="home-title">Good to see you in {tenant.tradingName}.</h1>
          <p>Here is the commercial foundation currently available to your role.</p>
        </div>
        <span className="status-chip">Active workspace</span>
      </header>
      <div className="summary-grid" aria-label="Foundation record counts">
        {cards.map((card) => (
          <article className="summary-card" key={card.label}>
            <p>{card.label}</p><strong>{card.value ?? 'Restricted'}</strong>
            <small>{card.value === null ? 'Not available to your role' : 'Visible records'}</small>
          </article>
        ))}
      </div>
      <div className="content-grid">
        <article className="detail-card">
          <p className="eyebrow">Workspace details</p><h2>{tenant.legalName}</h2>
          <dl className="detail-list">
            <div><dt>Currency</dt><dd>{tenant.currencyCode}</dd></div>
            <div><dt>Time zone</dt><dd>{tenant.timeZone}</dd></div>
            <div><dt>VAT status</dt><dd>{tenant.vatStatusCode}</dd></div>
          </dl>
        </article>
        <article className="next-action-card">
          <p className="eyebrow eyebrow-light">Next available action</p>
          <h2>Keep your profile current</h2>
          <p>Accurate contact details help your team recognise who is acting in this workspace.</p>
          <Link className="light-action" to="/profile">Review profile <span aria-hidden="true">→</span></Link>
        </article>
      </div>
    </section>
  )
}
