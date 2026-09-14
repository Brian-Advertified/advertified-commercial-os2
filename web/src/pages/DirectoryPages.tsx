import { useEffect, useMemo, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { directoryApi } from '../api/directory-client'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProductSummary } from '../api/inventory-schemas'
import type { Agency, ClientAccount } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import { channelLabel } from '../presentation/media-labels'

const agencyDirectoryRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

const partnerDirectoryRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

const supplyDirectoryRoles = new Set<string>([
  ...agencyDirectoryRoles,
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.supplierUser,
])

const influencerDirectoryRoles = new Set<string>([
  ...agencyDirectoryRoles,
  masterDataCodes.roles.influencerRep,
])

export function AdvertisersPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!agencyDirectoryRoles.has(selected.roleCode)) return <Unavailable label="Advertisers" />
  return <FoundationDirectoryPage tenantId={selected.tenantId} kind="advertisers" />
}

export function AgencyPartnersPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!partnerDirectoryRoles.has(selected.roleCode)) return <Unavailable label="Agency Partners" />
  return <FoundationDirectoryPage tenantId={selected.tenantId} kind="agencies" />
}

export function SuppliersPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!supplyDirectoryRoles.has(selected.roleCode)) return <Unavailable label="Suppliers" />
  return <SupplyDirectoryPage tenantId={selected.tenantId} influencerOnly={false} />
}

export function InfluencersPage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!influencerDirectoryRoles.has(selected.roleCode)) return <Unavailable label="Influencers & Creators" />
  return <SupplyDirectoryPage tenantId={selected.tenantId} influencerOnly />
}

function FoundationDirectoryPage({ tenantId, kind }: {
  tenantId: string
  kind: 'advertisers' | 'agencies'
}) {
  const [items, setItems] = useState<Array<ClientAccount | Agency> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  useEffect(() => {
    let active = true
    const request = kind === 'advertisers'
      ? directoryApi.listAdvertisers(tenantId)
      : directoryApi.listAgencies(tenantId)
    void request.then(value => { if (active) setItems(value) })
      .catch(() => { if (active) setError('The directory could not be loaded.') })
    return () => { active = false }
  }, [tenantId, kind])
  if (error && !items) return <MessageState title="Directory could not be loaded" message={error} />
  if (!items) return <LoadingState label="Loading directory" />
  const normalized = query.trim().toLowerCase()
  const visible = items.filter(item => !normalized ||
    item.tradingName.toLowerCase().includes(normalized) || item.legalName.toLowerCase().includes(normalized))
  const advertisers = kind === 'advertisers'
  return <section className="connected-directory-page">
    <DirectoryHeader eyebrow={advertisers ? 'Commercial relationships' : 'Partner network'}
      title={advertisers ? 'Advertisers' : 'Agency partners'}
      copy={advertisers
        ? 'Client accounts currently retained in this workspace. Campaign activity remains governed by each client’s Brief and approvals.'
        : 'Agency organisations retained in this workspace. Access and responsibilities remain governed by role and membership.'}
      count={items.length} />
    <DirectorySearch value={query} setValue={setQuery} placeholder={advertisers ? 'Search advertisers…' : 'Search agency partners…'} />
    <div className="connected-directory-grid">{visible.map(item => <article key={item.id} className="connected-directory-card">
      <div className="connected-directory-avatar">{item.tradingName.charAt(0).toUpperCase()}</div>
      <div><span>{advertisers ? 'Advertiser' : 'Agency partner'}</span><h2>{item.tradingName}</h2>
        <p>{item.legalName}</p></div>
      <dl><div><dt>Status</dt><dd>{humanizeCode(item.statusCode, true)}</dd></div>
        <div><dt>Reference</dt><dd>{item.externalReference}</dd></div>
        {'industry' in item && <div><dt>Industry</dt><dd>{item.industry ?? 'Not supplied'}</dd></div>}
        <div><dt>Website</dt><dd>{item.website ?? 'Not supplied'}</dd></div></dl>
    </article>)}</div>
    {visible.length === 0 && <DirectoryEmpty query={query} />}
  </section>
}

function SupplyDirectoryPage({ tenantId, influencerOnly }: { tenantId: string; influencerOnly: boolean }) {
  const [products, setProducts] = useState<InventoryProductSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  useEffect(() => {
    let active = true
    void inventoryApi.search(tenantId, {
      channel: influencerOnly ? masterDataCodes.channels.influencer : undefined,
      pageSize: 100,
    }).then(value => { if (active) setProducts(value.items) })
      .catch(() => { if (active) setError('The supply directory could not be loaded.') })
    return () => { active = false }
  }, [tenantId, influencerOnly])
  const grouped = useMemo(() => groupSupply(products ?? []), [products])
  if (error && !products) return <MessageState title="Directory could not be loaded" message={error} />
  if (!products) return <LoadingState label="Loading supply directory" />
  const normalized = query.trim().toLowerCase()
  const visible = grouped.filter(item => !normalized || item.name.toLowerCase().includes(normalized) ||
    item.channels.some(channel => channelLabel(channel).toLowerCase().includes(normalized)))
  return <section className="connected-directory-page">
    <DirectoryHeader eyebrow={influencerOnly ? 'Creator marketplace' : 'Media network'}
      title={influencerOnly ? 'Influencers & creators' : 'Suppliers'}
      copy={influencerOnly
        ? 'Creator and influencer supply retained through the governed inventory model. Profiles shown here are commercial supply records, not social claims.'
        : 'Media owners and suppliers represented by current governed inventory in this workspace.'}
      count={grouped.length} />
    <DirectorySearch value={query} setValue={setQuery}
      placeholder={influencerOnly ? 'Search creators or channels…' : 'Search suppliers or channels…'} />
    <div className="connected-directory-grid">{visible.map(item => <article key={item.id} className="connected-directory-card">
      <div className="connected-directory-avatar connected-directory-avatar--supply"><Icon name={influencerOnly ? 'users' : 'inventory'} /></div>
      <div><span>{influencerOnly ? 'Creator / representative' : 'Media supplier'}</span><h2>{item.name}</h2>
        <p>{item.products} governed product{item.products === 1 ? '' : 's'}</p></div>
      <div className="connected-directory-tags">{item.channels.map(channel => <span key={channel}>{channelLabel(channel)}</span>)}</div>
      <dl><div><dt>Markets</dt><dd>{item.geographies.slice(0, 3).join(' · ') || 'Not established'}</dd></div>
        <div><dt>Verified products</dt><dd>{item.verified}</dd></div></dl>
      <Link className="text-action" to={`/inventory?supplier=${encodeURIComponent(item.name)}`}>Open governed inventory →</Link>
    </article>)}</div>
    {visible.length === 0 && <DirectoryEmpty query={query} />}
  </section>
}

function groupSupply(products: InventoryProductSummary[]) {
  const groups = new Map<string, { id: string; name: string; products: number; verified: number; channels: string[]; geographies: string[] }>()
  products.forEach(product => {
    const current = groups.get(product.supplierId) ?? { id: product.supplierId, name: product.supplierName,
      products: 0, verified: 0, channels: [], geographies: [] }
    current.products += 1
    if (product.verification === masterDataCodes.verificationLevels.humanVerified ||
        product.verification === masterDataCodes.verificationLevels.sourceVerified) current.verified += 1
    if (!current.channels.includes(product.channel)) current.channels.push(product.channel)
    if (product.geography && !current.geographies.includes(product.geography)) current.geographies.push(product.geography)
    groups.set(product.supplierId, current)
  })
  return [...groups.values()].sort((a, b) => a.name.localeCompare(b.name))
}

function DirectoryHeader({ eyebrow, title, copy, count }: { eyebrow: string; title: string; copy: string; count: number }) {
  return <header className="connected-stage-heading connected-directory-heading"><div><p className="eyebrow">{eyebrow}</p>
    <h1>{title}</h1><p>{copy}</p></div><div className="connected-directory-count"><strong>{count}</strong><span>shown records</span></div></header>
}

function DirectorySearch({ value, setValue, placeholder }: { value: string; setValue: (value: string) => void; placeholder: string }) {
  return <label className="connected-directory-search"><Icon name="search" /><input type="search" value={value}
    onChange={event => setValue(event.target.value)} placeholder={placeholder} /></label>
}

function DirectoryEmpty({ query }: { query: string }) {
  return <article className="connected-directory-empty"><Icon name="search" /><h2>No matching records</h2>
    <p>{query ? 'Change the search to see other retained records.' : 'No governed records are available in this workspace yet.'}</p></article>
}

function Unavailable({ label }: { label: string }) {
  return <MessageState title={`${label} is not available`} message="This workspace role cannot view this directory." />
}
