import { useEffect, useState } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProductSummary } from '../api/inventory-schemas'
import type { CampaignBriefSummary } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { Icon, type IconName } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'

type SearchData = {
  briefs: CampaignBriefSummary[]
  campaigns: Campaign[]
  inventory: InventoryProductSummary[]
  incomplete: boolean
}

type SearchResult = {
  id: string
  category: string
  title: string
  detail: string
  updatedAtUtc: string
  to: string
  icon: IconName
}

export function SearchPage() {
  const { selected, loading } = useWorkspace()
  const [parameters] = useSearchParams()
  const query = parameters.get('q')?.trim() ?? ''
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />

  return <section className="approved-work-index" aria-labelledby="search-title">
    <header className="approved-work-index-header"><div>
      <h1 id="search-title">Search Advertified</h1>
      <p>Find work in the current workspace without crossing tenant boundaries.</p>
    </div></header>
    {query
      ? <SearchResults key={`${selected.tenantId}:${query}`}
          tenantId={selected.tenantId} query={query} />
      : <div className="approved-work-index-list"><article className="approved-work-index-empty">
          <strong>Enter a search term</strong>
          <p>Search campaigns, Briefs, inventory products and measurement reports.</p>
        </article></div>}
  </section>
}

function SearchResults({ tenantId, query }: { tenantId: string; query: string }) {
  const [data, setData] = useState<SearchData | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    void loadSearchData(tenantId, query)
      .then(value => { if (active) setData(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, query])

  if (error) return <MessageState title="Search could not be completed" message={error} />
  if (!data) return <LoadingState label={`Searching for ${query}`} />
  const results = buildResults(data, query)
  return <>
    {data.incomplete && <p className="inline-alert" role="status">
      Some work areas could not be searched with your current access.
    </p>}
    <p className="approved-search-summary" aria-live="polite">
      {results.length} result{results.length === 1 ? '' : 's'} for “{query}”
    </p>
    <div className="approved-work-index-list">
      {results.length === 0
        ? <article className="approved-work-index-empty"><strong>No matching records</strong>
            <p>Try a campaign, client, supplier, location or reference name.</p></article>
        : results.map(result => <SearchResultRow key={`${result.category}:${result.id}`}
            result={result} />)}
    </div>
  </>
}

function SearchResultRow({ result }: { result: SearchResult }) {
  return <Link className="approved-work-index-row" to={result.to}>
    <span><Icon name={result.icon} /></span>
    <div><strong>{result.title}</strong>
      <small>{result.category} · {result.detail}</small></div>
    <time>{formatDateTime(result.updatedAtUtc)}</time><Icon name="arrow" />
  </Link>
}

async function loadSearchData(tenantId: string, query: string): Promise<SearchData> {
  const [briefs, campaigns, inventory] = await Promise.allSettled([
    briefApi.list(tenantId),
    campaignApi.list(tenantId),
    inventoryApi.search(tenantId, { search: query }),
  ])
  const failures = [briefs, campaigns, inventory].filter(result => result.status === 'rejected')
  if (failures.length === 3) throw failures[0].reason
  return {
    briefs: briefs.status === 'fulfilled' ? briefs.value : [],
    campaigns: campaigns.status === 'fulfilled' ? campaigns.value : [],
    inventory: inventory.status === 'fulfilled' ? inventory.value.items : [],
    incomplete: failures.length > 0,
  }
}

function buildResults(data: SearchData, query: string): SearchResult[] {
  const normalized = query.toLowerCase()
  const results: SearchResult[] = []
  for (const brief of data.briefs) {
    if (!matches(normalized, brief.title, brief.clientName, brief.status)) continue
    results.push({ id: brief.id, category: 'Brief', title: brief.title,
      detail: `${brief.clientName} · ${humanizeCode(brief.status, true)}`,
      updatedAtUtc: brief.updatedAtUtc, to: `/briefs/${brief.id}`, icon: 'brief' })
  }
  for (const campaign of data.campaigns) {
    if (matches(normalized, campaign.title, campaign.status, campaign.fundingStatus)) {
      results.push({ id: campaign.id, category: 'Campaign', title: campaign.title,
        detail: humanizeCode(campaign.status, true), updatedAtUtc: campaign.updatedAtUtc,
        to: `/campaigns/${campaign.id}`, icon: 'plan' })
    }
    for (const report of campaign.measurementReports) {
      if (!matches(normalized, campaign.title, report.status,
        report.interpretation.executiveSummary, report.versionNumber)) continue
      results.push({ id: report.id, category: 'Measurement report',
        title: `${campaign.title} · Report ${report.versionNumber}`,
        detail: humanizeCode(report.status, true), updatedAtUtc: report.updatedAtUtc,
        to: `/measurement-reports/${report.id}`, icon: 'evidence' })
    }
  }
  for (const product of data.inventory) {
    if (!matches(normalized, product.name, product.productCode, product.supplierName,
      product.channel, product.productType, product.geography)) continue
    results.push({ id: product.id, category: 'Inventory', title: product.name,
      detail: `${product.supplierName} · ${product.geography}`,
      updatedAtUtc: product.updatedAtUtc, to: `/inventory/products/${product.id}`,
      icon: 'inventory' })
  }
  return results.sort((left, right) => right.updatedAtUtc.localeCompare(left.updatedAtUtc))
}

function matches(query: string, ...values: Array<string | number | null | undefined>) {
  return values.some(value => String(value ?? '').toLowerCase().includes(query))
}
