import type { InventoryBenchmark } from '../api/inventory-schemas'

export function InventoryBenchmarkPanel({ benchmark }: { benchmark: InventoryBenchmark }) {
  const difference = benchmark.differenceFromMedianPercent
  return <article className="detail-card inventory-benchmark" aria-labelledby="market-comparison-title">
    <div className="benchmark-heading"><div><p className="eyebrow">Market comparison</p>
      <h2 id="market-comparison-title">How this placement compares</h2>
      <p>{comparisonArea(benchmark.geographyBasis)} · {benchmark.cohortSize} comparable site{benchmark.cohortSize === 1 ? '' : 's'}</p></div>
      <div className="benchmark-position"><span>Market position</span>
        <strong>{humanize(benchmark.position)}</strong>
        <small>{Math.round(benchmark.confidence * 100)}% comparison confidence</small></div></div>
    <div className="benchmark-summary-grid">
      <Metric label="This rate" value={money(benchmark.rateAmountMinor, benchmark.currency)} />
      <Metric label="Local median" value={benchmark.medianMinor === null ? 'Not enough data' : money(benchmark.medianMinor, benchmark.currency)} />
      <Metric label="Vs median" value={difference === null ? 'Not enough data' : signedPercent(difference)} />
      <Metric label="Price percentile" value={benchmark.percentile === null ? 'Not enough data' : `${benchmark.percentile}%`} />
    </div>
    {benchmark.comparables.length > 0 && <details className="benchmark-comparables">
      <summary>View {benchmark.comparables.length} comparable site{benchmark.comparables.length === 1 ? '' : 's'}</summary>
      <div className="benchmark-comparable-list">{benchmark.comparables.map(site =>
        <div key={site.productVersionId} className="benchmark-comparable-row">
          <div><strong>{site.name}</strong><span>{site.geography}</span></div>
          <div><strong>{money(site.rateAmountMinor, site.currency)}</strong>
            <span>{site.distanceKilometres === null ? 'Local area match' : `${site.distanceKilometres.toFixed(1)} km away`}</span></div>
        </div>)}</div>
    </details>}
  </article>
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>
}

function comparisonArea(value: string) {
  if (value.startsWith('RADIUS_')) return `Compared within ${value.slice(7).replace('_KM', ' km').replace('_', '.')}`
  if (value.startsWith('GEOGRAPHY:')) return `Compared in ${value.slice(10)}`
  return humanize(value)
}

function humanize(value: string) {
  return value.toLowerCase().replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase())
}

function signedPercent(value: number) {
  if (value === 0) return 'At local median'
  return `${Math.abs(value)}% ${value < 0 ? 'below' : 'above'} median`
}

function money(amountMinor: number, currency: string) {
  return new Intl.NumberFormat('en-ZA', { style: 'currency', currency, maximumFractionDigits: 0 })
    .format(amountMinor / 100)
}
