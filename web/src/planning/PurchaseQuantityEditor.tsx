import { useState } from 'react'
import type { MediaAllocation, ShortlistCandidate } from '../api/planning-schemas'
import { masterDataCodes } from '../generated/master-data-codes'

const countRates: string[] = [masterDataCodes.rateTypes.cpm, masterDataCodes.rateTypes.cpc,
  masterDataCodes.rateTypes.cpl, masterDataCodes.rateTypes.cpa,
  masterDataCodes.rateTypes.spotRate, masterDataCodes.rateTypes.packageRate]

export function PurchaseQuantityEditor({ allocation, candidates, editable, onChange }: {
  allocation: MediaAllocation; candidates: ShortlistCandidate[]; editable: boolean
  onChange: (patch: Partial<MediaAllocation>) => void
}) {
  const [selected, setSelected] = useState('')
  const [quantity, setQuantity] = useState('')
  const purchases = allocation.purchases ?? []
  const available = candidates.filter(item => item.channel === allocation.channel && item.rateId &&
    countRates.includes(item.commercialReadiness?.rateType ?? '') && !purchases.some(purchase =>
      purchase.inventoryTenantId === item.inventoryTenantId && purchase.inventoryProductId === item.inventoryProductId))
  const candidate = available.find(item => item.id === selected)
  const count = Number(quantity)
  function add() {
    if (!candidate?.rateId || !candidate.commercialReadiness?.rateType || !Number.isSafeInteger(count) || count <= 0) return
    onChange({ purchases: [...purchases, { inventoryTenantId: candidate.inventoryTenantId,
      inventoryProductId: candidate.inventoryProductId, productVersionId: candidate.productVersionId,
      rateId: candidate.rateId, rateType: candidate.commercialReadiness.rateType, quantity: count }] })
    setSelected('')
    setQuantity('')
  }
  if (available.length === 0 && purchases.length === 0) return null
  return <fieldset><legend>Buying quantities</legend>
    <p>Enter committed impressions, clicks, leads, acquisitions, spots or supplier packages for the selected rate.</p>
    {purchases.map(purchase => <div key={`${purchase.inventoryTenantId}-${purchase.inventoryProductId}`}>
      <span>{candidates.find(item => item.productVersionId === purchase.productVersionId)?.name ?? 'Retained placement'}:
        {' '}{purchase.quantity.toLocaleString()} ({purchase.rateType})</span>
      {editable && <button type="button" className="text-action" onClick={() => onChange({
        purchases: purchases.filter(item => item !== purchase),
      })}>Remove quantity</button>}
    </div>)}
    {editable && available.length > 0 && <>
      <label>Placement<select value={selected} onChange={event => setSelected(event.target.value)}>
        <option value="">Choose a placement</option>
        {available.map(item => <option key={item.id} value={item.id}>{item.name} ({item.commercialReadiness?.rateType})</option>)}
      </select></label>
      <label>Committed quantity<input type="number" min="1" step="1" value={quantity}
        onChange={event => setQuantity(event.target.value)} /></label>
      <button type="button" className="secondary-button" disabled={!candidate || !Number.isSafeInteger(count) || count <= 0}
        onClick={add}>Add buying quantity</button>
    </>}
  </fieldset>
}
