import type { ProposalOption } from '../api/proposal-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { mediaVisual } from '../planning/media-visuals'
import { formatDate, formatMoney } from '../presentation/format'

export function ProposalOptionCard({ option, selected, decisionMode, busy, onSelect }: {
  option: ProposalOption
  selected: boolean
  decisionMode: boolean
  busy: boolean
  onSelect?: () => Promise<void>
}) {
  return <article className={`proposal-option-card ${selected ? 'is-selected' : ''}`}>
    <header><div><span className="proposal-option-number">Option {option.displayOrder}</span>
      <h2>{option.label}</h2></div><strong>{formatMoney(option.budgetMinor, option.currency)}</strong></header>
    <p className="proposal-option-outcome">{option.outcome}</p>
    <div className="proposal-option-channels">{option.channels.map(channel =>
      <span key={channel}><MediaTypeIcon channel={channel} />{mediaVisual(channel).label}</span>)}</div>
    <div className="proposal-option-periods">{groupPeriods(option).map(item =>
      <div key={item.channel}><strong>{mediaVisual(item.channel).label}</strong>
        <span>{item.periods.join(' · ')}</span></div>)}</div>
    {option.inventoryNames.length > 0 && <details className="proposal-inventory-detail">
      <summary>View included media</summary>
      {option.inventory.length === 0
        ? <ul>{option.inventoryNames.map(name => <li key={name}>{name}</li>)}</ul>
        : <ul>{option.inventory.map(item => <li key={item.productVersionId}>
          <strong>{item.name}</strong> · {item.geography} · {formatMoney(item.clientPriceMinor, option.currency)}
          {item.deliverable && <small>Deliverable: {[item.deliverable.format,
            item.deliverable.buyingUnit, item.deliverable.dimensions,
            item.deliverable.placement].filter(Boolean).join(' · ')}</small>}
          {item.commercialTerms && item.commercialTerms.conditions.length > 0 &&
            <small>Conditions: {item.commercialTerms.conditions.join('; ')}</small>}
          {item.spatial && <small>Location: {[item.spatial.venue, item.spatial.road,
            item.spatial.route, item.spatial.trafficDirection].filter(Boolean).join(' · ')}</small>}
          {item.logoAssetId && <small>Rights-approved supplier logo selected.</small>}
          {item.uncertainties.map(value => <small key={value}>Unresolved: {value}</small>)}
        </li>)}</ul>}
    </details>}
    {selected && <p className="proposal-selected-mark">Selected by the client</p>}
    {decisionMode && !selected && onSelect && <button className="primary-button proposal-select-button"
      type="button" disabled={busy} onClick={() => void onSelect()}>Select this option</button>}
  </article>
}

function groupPeriods(option: ProposalOption) {
  const grouped = new Map<string, string[]>()
  for (const period of option.runningPeriods) {
    const values = grouped.get(period.channel) ?? []
    values.push(`${formatDate(period.start)} – ${formatDate(period.end)}`)
    grouped.set(period.channel, values)
  }
  return [...grouped].map(([channel, periods]) => ({ channel, periods }))
}
