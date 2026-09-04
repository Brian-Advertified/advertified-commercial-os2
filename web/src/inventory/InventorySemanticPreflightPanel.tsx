import { useState } from 'react'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { masterDataCodes } from '../generated/master-data-codes'
import type {
  InventorySemanticPreflight,
} from '../api/inventory-semantic-preflight-schemas'
import { humanizeCode } from '../presentation/format'

export function InventorySemanticPreflightPanel({
  tenantId,
}: {
  tenantId: string
}) {
  const [result, setResult] =
    useState<InventorySemanticPreflight | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function preflight() {
    setBusy(true)
    setError(null)
    try {
      setResult(
        await inventoryApi.semanticPreflight(tenantId))
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setBusy(false)
    }
  }

  return <section className="inventory-record-section">
    <p className="eyebrow">AI cost and safety gate</p>
    <h2>Inventory semantic preflight</h2>
    <p>Build the complete tenant extraction plan and calculate its
      worst-case Bedrock cost. This check does not call Bedrock.</p>
    <button className="secondary-button" type="button"
      disabled={busy} onClick={() => void preflight()}>
      {busy ? 'Calculating…' : 'Cost complete extraction batch'}
    </button>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {result && <PreflightResult result={result} />}
  </section>
}

function PreflightResult({
  result,
}: {
  result: InventorySemanticPreflight
}) {
  const blocked = result.sources.filter(
    source => source.blocker !== null)
  const packets = result.sources.reduce(
    (total, source) => total + source.packetCount, 0)
  const images = result.sources.reduce(
    (total, source) => total + source.imageCount, 0)
  return <div className="detail-card">
    <h3>{result.readyToActivate
      ? 'Batch is within the configured activation gates'
      : 'Batch remains blocked'}</h3>
    <dl>
      <div><dt>Sources</dt><dd>{result.sources.length}</dd></div>
      <div><dt>Packets</dt><dd>{packets}</dd></div>
      <div><dt>Images</dt><dd>{images}</dd></div>
      <div><dt>Worst-case total</dt>
        <dd>{usd(result.worstCaseTotalCostUsdMicros)}</dd></div>
      <div><dt>Approved ceiling</dt>
        <dd>{usd(result.certificationBudgetUsdMicros)}</dd></div>
      <div><dt>Blocked sources</dt><dd>{blocked.length}</dd></div>
      <div><dt>Live execution</dt>
        <dd>{result.liveExecutionEnabled ? 'Enabled' : 'Disabled'}</dd></div>
    </dl>
    {result.blockers.length > 0 &&
      <ul>{result.blockers.map(blocker =>
        <li key={blocker}>{humanizeCode(blocker, true)}</li>)}</ul>}
    <details>
      <summary>File-by-file preflight</summary>
      <ul>{result.sources.map(source =>
        <li key={source.importId}>
          <strong>{source.fileName}</strong>
          {' · '}{source.packetCount} packet(s)
          {' · '}{usd(source.newMaximumCostUsdMicros)}
          {source.blocker
            ? ` · ${humanizeCode(source.blocker, true)}`
            : ' · ready for governed reprojection'}
        </li>)}</ul>
    </details>
  </div>
}

function usd(micros: number) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: masterDataCodes.currencies.usd,
    minimumFractionDigits: 2,
    maximumFractionDigits: 6,
  }).format(micros / 1_000_000)
}
