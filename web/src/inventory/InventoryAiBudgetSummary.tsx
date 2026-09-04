import {
  inventoryAiBudget,
  inventoryAiBudgetReasons,
  inventoryAiUsd,
} from './inventory-ai-budget-policy'

type InventoryAiBudgetSummaryProps = {
  activeCommittedUsdMicros: number
}

export function InventoryAiBudgetSummary({
  activeCommittedUsdMicros,
}: InventoryAiBudgetSummaryProps) {
  const budget = inventoryAiBudget(activeCommittedUsdMicros)

  return (
    <section aria-labelledby="inventory-ai-budget-title" className="inventory-ai-budget-summary">
      <div className="inventory-ai-budget-summary__heading">
        <div>
          <p className="eyebrow">Inventory AI budget</p>
          <h3 id="inventory-ai-budget-title">US$5 hard ceiling</h3>
        </div>
        <strong>{budget.usedPercentage.toFixed(1)}% accounted</strong>
      </div>

      <div
        aria-label={`${inventoryAiUsd(budget.accountedUsedUsdMicros)} of ${inventoryAiUsd(budget.totalUsdMicros)} accounted`}
        aria-valuemax={budget.totalUsdMicros}
        aria-valuemin={0}
        aria-valuenow={budget.accountedUsedUsdMicros}
        className="inventory-ai-budget-summary__progress"
        role="progressbar"
      >
        <span style={{ width: `${budget.usedPercentage}%` }} />
      </div>

      <dl className="inventory-ai-budget-summary__totals">
        <div>
          <dt>Ceiling</dt>
          <dd>{inventoryAiUsd(budget.totalUsdMicros)}</dd>
        </div>
        <div>
          <dt>Accounted usage</dt>
          <dd>{inventoryAiUsd(budget.accountedUsedUsdMicros)}</dd>
        </div>
        <div>
          <dt>Remaining</dt>
          <dd>{inventoryAiUsd(budget.remainingUsdMicros)}</dd>
        </div>
        <div>
          <dt>Corpus commitment</dt>
          <dd>{inventoryAiUsd(budget.inventoryCommittedUsdMicros)}</dd>
        </div>
        <div>
          <dt>Brief-to-proposal canary</dt>
          <dd>{inventoryAiUsd(budget.canaryCommittedUsdMicros)}</dd>
        </div>
      </dl>

      <details>
        <summary>Why this budget was used</summary>
        <dl className="inventory-ai-budget-summary__reasons">
          {inventoryAiBudgetReasons.map((item) => (
            <div key={item.code}>
              <dt>{item.label}</dt>
              <dd>
                <strong>{inventoryAiUsd(item.amountUsdMicros)}</strong>
                <span>{item.explanation}</span>
              </dd>
            </div>
          ))}
        </dl>
      </details>
    </section>
  )
}
