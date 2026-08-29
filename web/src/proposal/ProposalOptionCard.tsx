import type { ProposalOption } from '../api/proposal-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { mediaVisual } from '../planning/media-visuals'

export function ProposalOptionCard({ option, selected, decisionMode, busy, onSelect }: {
  option: ProposalOption
  selected: boolean
  decisionMode: boolean
  busy: boolean
  onSelect?: () => Promise<void>
}) {
  return <article className={`proposal-option-card ${selected ? 'is-selected' : ''}`}>
    <header><div><span className="proposal-option-number">Option {option.displayOrder}</span>
      <h2>{option.label}</h2></div><strong>{money(option.budgetMinor, option.currency)}</strong></header>
    <p className="proposal-option-outcome">{option.outcome}</p>
    <div className="proposal-option-channels">{option.channels.map(channel =>
      <span key={channel}><MediaTypeIcon channel={channel} />{mediaVisual(channel).label}</span>)}</div>
    <div className="proposal-option-periods">{groupPeriods(option).map(item =>
      <div key={item.channel}><strong>{mediaVisual(item.channel).label}</strong>
        <span>{item.periods.join(' · ')}</span></div>)}</div>
    {option.inventoryNames.length > 0 && <details className="proposal-inventory-detail">
      <summary>View included media</summary>
      <ul>{option.inventoryNames.map(name => <li key={name}>{name}</li>)}</ul>
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
    values.push(`${date(period.start)} – ${date(period.end)}`)
    grouped.set(period.channel, values)
  }
  return [...grouped].map(([channel, periods]) => ({ channel, periods }))
}

function date(value: string) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' })
    .format(new Date(`${value}T00:00:00`))
}

function money(amountMinor: number, currency: string) {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency, maximumFractionDigits: 0 })
    .format(amountMinor / 100)
}
