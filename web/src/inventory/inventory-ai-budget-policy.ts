import { inventoryAiBudgetState } from '../generated/inventory-ai-budget-state'
import { masterDataCodes } from '../generated/master-data-codes'

export const INVENTORY_AI_TOTAL_BUDGET_USD_MICROS =
  inventoryAiBudgetState.totalUsdMicros
export const INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS =
  inventoryAiBudgetState.confirmedHistoricalUsdMicros
export const INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS =
  inventoryAiBudgetState.uncertainReserveUsdMicros
export const INVENTORY_AI_ACCOUNTED_HISTORICAL_USD_MICROS =
  INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS +
  INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS
export const INVENTORY_AI_SEMANTIC_ALLOCATION_USD_MICROS = 4_311_878
export const INVENTORY_AI_CANARY_RESERVE_USD_MICROS = 500_000
export const INVENTORY_AI_PER_CALL_MAXIMUM_USD_MICROS = 60_000

export type InventoryAiBudgetView = {
  totalUsdMicros: number
  confirmedHistoricalUsdMicros: number
  uncertainReserveUsdMicros: number
  inventoryCommittedUsdMicros: number
  canaryCommittedUsdMicros: number
  accountedUsedUsdMicros: number
  remainingUsdMicros: number
  usedPercentage: number
}

export function inventoryAiBudget(
  activeInventoryCommittedUsdMicros: number,
): InventoryAiBudgetView {
  const inventoryCommitted = Math.max(
    inventoryAiBudgetState.inventoryCommittedUsdMicros,
    Math.max(0, Math.trunc(activeInventoryCommittedUsdMicros)),
  )
  const canaryCommitted = inventoryAiBudgetState.canaryCommittedUsdMicros
  const accountedUsed = Math.min(
    INVENTORY_AI_TOTAL_BUDGET_USD_MICROS,
    INVENTORY_AI_ACCOUNTED_HISTORICAL_USD_MICROS +
      inventoryCommitted +
      canaryCommitted,
  )
  return {
    totalUsdMicros: INVENTORY_AI_TOTAL_BUDGET_USD_MICROS,
    confirmedHistoricalUsdMicros:
      INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS,
    uncertainReserveUsdMicros: INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS,
    inventoryCommittedUsdMicros: inventoryCommitted,
    canaryCommittedUsdMicros: canaryCommitted,
    accountedUsedUsdMicros: accountedUsed,
    remainingUsdMicros:
      INVENTORY_AI_TOTAL_BUDGET_USD_MICROS - accountedUsed,
    usedPercentage:
      (accountedUsed / INVENTORY_AI_TOTAL_BUDGET_USD_MICROS) * 100,
  }
}

export function inventoryAiUsd(usdMicros: number): string {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency: masterDataCodes.currencies.usd,
    minimumFractionDigits: 4,
    maximumFractionDigits: 6,
  }).format(usdMicros / 1_000_000)
}

export const inventoryAiBudgetReasons = inventoryAiBudgetState.purposes
