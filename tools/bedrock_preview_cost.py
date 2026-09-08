"""Enforce the cumulative US$2 Bedrock preview budget from persisted cost rows."""
from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path

from ai_cost_catalog import (
    baseline_payload,
    delta,
    read_all_ai_cost_rows,
    rows_from_payload,
)
from inventory_ai_cost_ledger import REPO_ROOT

ROOT = REPO_ROOT / "artifacts" / "production-readiness" / "preview"
BASELINE = ROOT / "bedrock-cost-baseline.json"
REPORT = ROOT / "bedrock-cost-report.json"
BUDGET_USD_MICROS = 2_000_000


def snapshot() -> int:
    ROOT.mkdir(parents=True, exist_ok=True)
    payload = baseline_payload(read_all_ai_cost_rows())
    payload["capturedAtUtc"] = datetime.now(UTC).isoformat()
    payload["budgetUsdMicros"] = BUDGET_USD_MICROS
    BASELINE.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "mode": "baseline",
        "rowCount": payload["rowCount"],
        "budgetUsd": BUDGET_USD_MICROS / 1_000_000,
    }, indent=2))
    return 0


def verify(require_changes: bool) -> int:
    before = rows_from_payload(json.loads(BASELINE.read_text(encoding="utf-8")))
    changes = delta(before, read_all_ai_cost_rows())
    actual = int(changes["actualDeltaUsdMicros"])
    committed = int(changes["committedDeltaUsdMicros"])
    failures = []
    if actual > BUDGET_USD_MICROS or committed > BUDGET_USD_MICROS:
        failures.append("BEDROCK_PREVIEW_BUDGET_EXCEEDED")
    if require_changes and int(changes["changeCount"]) == 0:
        failures.append("NO_BEDROCK_PREVIEW_COST_ROWS")
    report = {
        "schemaVersion": "advertified.bedrock-preview-cost.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "budgetUsdMicros": BUDGET_USD_MICROS,
        "actualUsdMicros": actual,
        "committedUsdMicros": committed,
        "remainingUsdMicros": max(0, BUDGET_USD_MICROS - committed),
        "passed": not failures,
        "failures": failures,
        **changes,
    }
    ROOT.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "mode": "verify",
        "passed": report["passed"],
        "actualUsd": actual / 1_000_000,
        "committedUsd": committed / 1_000_000,
        "remainingUsd": report["remainingUsdMicros"] / 1_000_000,
        "changeCount": changes["changeCount"],
        "failures": failures,
    }, indent=2))
    return 0 if not failures else 2


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("baseline", "verify"))
    parser.add_argument("--require-changes", action="store_true")
    args = parser.parse_args()
    return snapshot() if args.mode == "baseline" else verify(args.require_changes)


if __name__ == "__main__":
    raise SystemExit(main())
