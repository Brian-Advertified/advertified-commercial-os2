"""Verify the live brief-to-proposal canary stayed inside its US$0.50 reserve."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path

from ai_cost_catalog import delta, read_all_ai_cost_rows, rows_from_payload
from inventory_ai_cost_ledger import REPO_ROOT, load_policy

ROOT = REPO_ROOT / "artifacts" / "inventory-corpus" / "ai-cost"
BASELINE = ROOT / "brief-proposal-canary-baseline.json"
OUTPUT = ROOT / "brief-proposal-canary-cost.json"


def main() -> int:
    policy = load_policy()
    baseline = json.loads(
        BASELINE.resolve(strict=True).read_text(encoding="utf-8")
    )
    changes = delta(
        rows_from_payload(baseline),
        read_all_ai_cost_rows(),
    )
    reserve = int(policy["briefProposalCanaryReserveUsdMicros"])
    accounted_history = int(policy["accountedHistoricalUsageUsdMicros"])
    inventory_report_path = ROOT / "inventory-ai-cost-report.json"
    inventory_report = json.loads(
        inventory_report_path.resolve(strict=True).read_text(encoding="utf-8")
    )
    inventory_new = int(
        inventory_report.get("newCommittedUsageUsdMicros") or 0
    )
    canary_committed = int(changes["committedDeltaUsdMicros"])
    total = accounted_history + inventory_new + canary_committed
    failures = []
    if canary_committed > reserve:
        failures.append("CANARY_RESERVE_EXCEEDED")
    if total > int(policy["totalBudgetUsdMicros"]):
        failures.append("US5_TOTAL_CEILING_EXCEEDED")
    if not changes["changeCount"]:
        failures.append("NO_LIVE_AI_COST_ENTRY_RECORDED_FOR_CANARY")
    report = {
        "schemaVersion": "advertified.brief-proposal-canary-cost.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "canaryReserveUsdMicros": reserve,
        "canaryActualUsdMicros": changes["actualDeltaUsdMicros"],
        "canaryCommittedUsdMicros": canary_committed,
        "inventoryCommittedUsdMicros": inventory_new,
        "historicalAccountedUsdMicros": accounted_history,
        "totalAccountedUsdMicros": total,
        "remainingUsdMicros": max(
            0, int(policy["totalBudgetUsdMicros"]) - total
        ),
        "passed": not failures,
        "failures": failures,
        "changes": changes["changes"],
    }
    OUTPUT.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
