type InventoryBudgetSource = {
  fileName: string;
  packetCount: number;
  maximumCostUsdMicros: number;
  newMaximumCostUsdMicros: number;
  blocker?: string | null;
};

type InventoryBudgetPreflight = {
  existingCommittedCostUsdMicros: number;
  newMaximumCostUsdMicros: number;
  worstCaseTotalCostUsdMicros: number;
  liveExecutionEnabled: boolean;
  sources: InventoryBudgetSource[];
};

type InventoryBedrockBudgetSummaryProps = {
  preflight: InventoryBudgetPreflight;
};

const HARD_CORPUS_BUDGET_USD_MICROS = 5_000_000;
const CONFIRMED_HISTORICAL_USAGE_USD_MICROS = 90_935;
const HISTORICAL_UNCERTAINTY_RESERVE_USD_MICROS = 97_187;

function formatUsd(micros: number): string {
  return `US$${(micros / 1_000_000).toFixed(6)}`;
}

export function InventoryBedrockBudgetSummary({
  preflight,
}: InventoryBedrockBudgetSummaryProps) {
  const currentScopeExposure = Math.max(
    0,
    preflight.existingCommittedCostUsdMicros,
  );
  const plannedExposure = Math.max(
    0,
    preflight.newMaximumCostUsdMicros,
  );
  const reservedTotal =
    CONFIRMED_HISTORICAL_USAGE_USD_MICROS +
    HISTORICAL_UNCERTAINTY_RESERVE_USD_MICROS +
    currentScopeExposure +
    plannedExposure;
  const remaining = Math.max(
    0,
    HARD_CORPUS_BUDGET_USD_MICROS - reservedTotal,
  );

  return (
    <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5">
      <div>
        <p className="text-sm font-semibold text-slate-950">
          Bedrock corpus certification budget
        </p>
        <p className="mt-1 text-sm text-slate-600">
          Deterministic physical transcription costs nothing. Paid calls are
          limited to grounded classification and searchable descriptions after
          all physical-file checks pass.
        </p>
      </div>

      <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <BudgetValue label="Hard limit" value={formatUsd(HARD_CORPUS_BUDGET_USD_MICROS)} />
        <BudgetValue
          label="Confirmed used"
          value={formatUsd(CONFIRMED_HISTORICAL_USAGE_USD_MICROS)}
        />
        <BudgetValue
          label="Historical reserve"
          value={formatUsd(HISTORICAL_UNCERTAINTY_RESERVE_USD_MICROS)}
        />
        <BudgetValue
          label="Current + planned"
          value={formatUsd(currentScopeExposure + plannedExposure)}
        />
        <BudgetValue label="Remaining" value={formatUsd(remaining)} />
      </dl>

      <div className="overflow-hidden rounded-xl border border-slate-200">
        <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Source</th>
              <th className="px-4 py-3">Why paid AI is requested</th>
              <th className="px-4 py-3 text-right">Packets</th>
              <th className="px-4 py-3 text-right">Maximum</th>
              <th className="px-4 py-3">State</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {preflight.sources.map((source) => (
              <tr key={source.fileName}>
                <td className="px-4 py-3 font-medium text-slate-900">
                  {source.fileName}
                </td>
                <td className="px-4 py-3 text-slate-600">
                  Classify existing physical rows and generate grounded search
                  descriptions. Source prices, dates, availability and buying
                  bases cannot be changed.
                </td>
                <td className="px-4 py-3 text-right tabular-nums">
                  {source.packetCount}
                </td>
                <td className="px-4 py-3 text-right tabular-nums">
                  {formatUsd(source.newMaximumCostUsdMicros)}
                </td>
                <td className="px-4 py-3 text-slate-600">
                  {source.blocker ?? (preflight.liveExecutionEnabled ? "Ready" : "Locked")}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function BudgetValue({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-slate-50 px-4 py-3">
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
        {label}
      </dt>
      <dd className="mt-1 text-lg font-semibold tabular-nums text-slate-950">
        {value}
      </dd>
    </div>
  );
}
