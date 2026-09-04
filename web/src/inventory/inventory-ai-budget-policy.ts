export const INVENTORY_AI_TOTAL_BUDGET_USD_MICROS = 5_000_000
export const INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS = 90_935
export const INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS = 97_187
export const INVENTORY_AI_ACCOUNTED_HISTORICAL_USD_MICROS =
  INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS +
  INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS
export const INVENTORY_AI_MAXIMUM_NEW_USAGE_USD_MICROS =
  INVENTORY_AI_TOTAL_BUDGET_USD_MICROS -
  INVENTORY_AI_ACCOUNTED_HISTORICAL_USD_MICROS
export const INVENTORY_AI_PER_CALL_MAXIMUM_USD_MICROS = 60_000

export type InventoryAiBudgetView = {
  totalUsdMicros: number
  confirmedHistoricalUsdMicros: number
  uncertainReserveUsdMicros: number
  activeCommittedUsdMicros: number
  accountedUsedUsdMicros: number
  remainingUsdMicros: number
  usedPercentage: number
}

export function inventoryAiBudget(
  activeCommittedUsdMicros: number,
): InventoryAiBudgetView {
  const committed = Math.max(0, Math.trunc(activeCommittedUsdMicros))
  const accountedUsed = Math.min(
    INVENTORY_AI_TOTAL_BUDGET_USD_MICROS,
    INVENTORY_AI_ACCOUNTED_HISTORICAL_USD_MICROS + committed,
  )
  return {
    totalUsdMicros: INVENTORY_AI_TOTAL_BUDGET_USD_MICROS,
    confirmedHistoricalUsdMicros:
      INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS,
    uncertainReserveUsdMicros: INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS,
    activeCommittedUsdMicros: committed,
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
    currency: 'USD',
    minimumFractionDigits: 4,
    maximumFractionDigits: 6,
  }).format(usdMicros / 1_000_000)
}

export const inventoryAiBudgetReasons = [
  {
    code: 'HISTORICAL_CERTIFICATION_USAGE',
    label: 'Earlier extraction certification',
    amountUsdMicros: INVENTORY_AI_CONFIRMED_HISTORICAL_USD_MICROS,
    explanation:
      'Confirmed provider usage retained from the workbook and extraction evaluations.',
  },
  {
    code: 'FAILED_CALL_USAGE_RESERVE',
    label: 'Conservative failed-call reserve',
    amountUsdMicros: INVENTORY_AI_UNCERTAIN_RESERVE_USD_MICROS,
    explanation:
      'Reserved so failed calls with incomplete usage evidence cannot cause the US$5 ceiling to be exceeded.',
  },
  {
    code: 'CORPUS_SEMANTIC_CLASSIFICATION',
    label: 'Corpus classification and descriptions',
    amountUsdMicros: INVENTORY_AI_MAXIMUM_NEW_USAGE_USD_MICROS,
    explanation:
      'Maximum available for classifying and describing physically certified inventory rows. Bedrock cannot alter source facts.',
  },
] as const
