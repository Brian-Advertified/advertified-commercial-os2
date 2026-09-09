import type { Proposal, ProposalOption } from '../api/proposal-schemas'
import { formatDate, formatMoney } from '../presentation/format'
import { mediaVisual } from '../planning/media-visuals'

export function ProposalChoiceComparison({ proposal }: { proposal: Proposal }) {
  if (proposal.options.length < 2) return null
  return <section className="proposal-choice-comparison" id="proposal-comparison" aria-labelledby="proposal-comparison-title">
    <header><div><p className="eyebrow">Decision view</p>
      <h2 id="proposal-comparison-title">Compare the client choices side by side</h2>
      <p>Advertified keeps the differences visible so the client can choose a route for its commercial trade-offs, not because one option simply looks more polished.</p></div></header>
    <div className="proposal-comparison-scroll"><table>
      <thead><tr><th scope="col">Decision factor</th>{proposal.options.map(option =>
        <th scope="col" key={option.id}>{option.label}{proposal.decision?.optionId === option.id && <span>Selected</span>}</th>)}</tr></thead>
      <tbody>
        <ComparisonRow label="Investment" options={proposal.options}
          render={option => formatMoney(option.budgetMinor, option.currency)} />
        <ComparisonRow label="Campaign outcome" options={proposal.options}
          render={option => option.outcome} />
        <ComparisonRow label="Media roles" options={proposal.options}
          render={option => option.channels.map(channel => mediaVisual(channel).label).join(', ')} />
        <ComparisonRow label="Suppliers" options={proposal.options}
          render={option => supplierSummary(option)} />
        <ComparisonRow label="Media included" options={proposal.options}
          render={option => `${option.inventory.length || option.inventoryNames.length} placement${(option.inventory.length || option.inventoryNames.length) === 1 ? '' : 's'}`} />
        <ComparisonRow label="Geographies" options={proposal.options}
          render={option => geographySummary(option)} />
        <ComparisonRow label="Running window" options={proposal.options}
          render={option => runningWindow(option)} />
      </tbody>
    </table></div>
    <footer>Lower cost is not automatically better, and a larger media list is not automatically stronger. Choose the route whose retained outcome and trade-offs best match the approved Brief.</footer>
  </section>
}

function ComparisonRow({ label, options, render }: {
  label: string
  options: ProposalOption[]
  render: (option: ProposalOption) => string
}) {
  return <tr><th scope="row">{label}</th>{options.map(option => <td key={option.id}>{render(option)}</td>)}</tr>
}

function supplierSummary(option: ProposalOption) {
  const names = [...new Set(option.inventory.map(item => item.supplierName).filter(Boolean))] as string[]
  if (names.length === 0) return 'Not exposed on this retained option'
  return names.length <= 3 ? names.join(', ') : `${names.slice(0, 3).join(', ')} +${names.length - 3} more`
}

function geographySummary(option: ProposalOption) {
  const values = [...new Set(option.inventory.map(item => item.geography).filter(Boolean))]
  if (values.length === 0) return 'Not supplied'
  return values.length <= 3 ? values.join(', ') : `${values.slice(0, 3).join(', ')} +${values.length - 3} more`
}

function runningWindow(option: ProposalOption) {
  if (option.runningPeriods.length === 0) return 'Not supplied'
  const starts = option.runningPeriods.map(item => item.start).sort()
  const ends = option.runningPeriods.map(item => item.end).sort()
  return `${formatDate(starts[0])} – ${formatDate(ends.at(-1)!)} `
}
