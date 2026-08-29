import { useEffect, useState } from 'react'
import { ApiFailure, humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryBenchmark } from '../api/inventory-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { InventoryBenchmarkPanel } from './InventoryBenchmarkPanel'

const benchmarkChannels = new Set<string>([
  masterDataCodes.channels.ooh,
  masterDataCodes.channels.dooh,
])

export function InventoryBenchmarkSection({ tenantId, productId, channel }: {
  tenantId: string
  productId: string
  channel: string
}) {
  const [benchmark, setBenchmark] = useState<InventoryBenchmark | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  useEffect(() => {
    if (!benchmarkChannels.has(channel)) return
    let active = true
    void inventoryApi.getBenchmark(tenantId, productId)
      .then(value => { if (active) { setBenchmark(value); setMessage(null) } })
      .catch((failure: unknown) => {
        if (!active) return
        setMessage(failure instanceof ApiFailure &&
          failure.code === 'INVENTORY_BENCHMARK_UNAVAILABLE'
          ? 'There is not enough current comparable OOH data to position this placement yet.'
          : humanMessage(failure))
      })
    return () => { active = false }
  }, [tenantId, productId, channel])
  if (!benchmarkChannels.has(channel)) return null
  if (benchmark) return <InventoryBenchmarkPanel benchmark={benchmark} />
  if (message) return <article className="detail-card inventory-benchmark-empty">
    <p className="eyebrow">Market comparison</p><h2>Comparison not available yet</h2><p>{message}</p>
  </article>
  return <article className="detail-card inventory-benchmark-empty" aria-busy="true">
    <p className="eyebrow">Market comparison</p><h2>Comparing nearby inventory…</h2>
  </article>
}
