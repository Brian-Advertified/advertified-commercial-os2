"""Check the US$10 owner budget without resetting existing provider commitments."""
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
from inventory_ai_cost_ledger import REPO_ROOT, query_json
from load_local_audience_bootstrap import inspect_container

ROOT = REPO_ROOT / "artifacts" / "production-readiness" / "preview"
BASELINE = ROOT / "bedrock-cost-baseline.json"
REPORT = ROOT / "bedrock-cost-report.json"
BUDGET_USD_MICROS = 10_000_000  # Total authorised by the owner on 2026-09-13.


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
    # A relative report cannot override lifetime commitments or an unapplied budget migration.
    if guard() != 0:
        return 2
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


def guard() -> int:
    """Read the same lifetime commitments as the atomic API dispatch guard; never reset them."""
    inspect_container()
    value = query_json("""
        SELECT jsonb_build_object(
            'committedUsdMicros', governance.read_ai_monthly_budget(CURRENT_DATE),
            'receiptAwareCommittedUsdMicros', COALESCE(sum(
                CASE
                    WHEN status_code = 'COMPLETED' AND actual_cost_usd_micros IS NOT NULL
                        THEN actual_cost_usd_micros
                    ELSE maximum_cost_usd_micros
                END
            ), 0),
            'ledgerMaximumUsdMicros', COALESCE(sum(maximum_cost_usd_micros), 0),
            'recordedActualUsdMicros', COALESCE(sum(actual_cost_usd_micros), 0),
            'unreconciledCalls', count(*) FILTER (WHERE actual_cost_usd_micros IS NULL),
            'reservationCount', count(*),
            'ownerBudgetMigrationApplied', EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '202609130016_OwnerAiBudgetTenDollars'),
            'receiptReconciliationMigrationApplied', EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '202609130017_AiBudgetReceiptReconciliation'))
        FROM governance.ai_monthly_budget_ledger
        """)
    limit = BUDGET_USD_MICROS
    per_call = 60_000  # Current Haiku 4.5 preview maximum is US$0.06 for Audience Intelligence.
    committed = int(value['committedUsdMicros'])
    remaining = max(0, limit - committed)
    report = {
        'schemaVersion': 'advertified.owner-ai-budget.v1',
        'capturedAtUtc': datetime.now(UTC).isoformat(),
        'limitUsdMicros': limit, 'nextCallMaximumUsdMicros': per_call,
        'remainingUsdMicros': remaining, **value,
        'passed': value['ownerBudgetMigrationApplied'] is True
        and value['receiptReconciliationMigrationApplied'] is True
        and committed == int(value['receiptAwareCommittedUsdMicros'])
        and committed <= int(value['ledgerMaximumUsdMicros'])
        and remaining >= per_call,
    }
    ROOT.mkdir(parents=True, exist_ok=True)
    (ROOT / 'bedrock-owner-budget-report.json').write_text(
        json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
    return 0 if report['passed'] else 2


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("baseline", "verify", "guard"))
    parser.add_argument("--require-changes", action="store_true")
    args = parser.parse_args()
    if args.mode == 'guard':
        return guard()
    return snapshot() if args.mode == "baseline" else verify(args.require_changes)


if __name__ == "__main__":
    raise SystemExit(main())
