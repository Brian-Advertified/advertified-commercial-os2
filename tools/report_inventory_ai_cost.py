"""Generate the auditable US$5 inventory-AI cost report."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path

from inventory_ai_cost_ledger import (
    REPO_ROOT,
    load_policy,
    read_inventory_semantic_runs,
    validate_budget,
)

OUTPUT_ROOT = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "ai-cost"
)


def main() -> int:
    policy = load_policy()
    entries = read_inventory_semantic_runs()
    report = validate_budget(entries, policy)
    report["schemaVersion"] = "advertified.inventory-ai-cost-report.v1"
    report["generatedAtUtc"] = datetime.now(UTC).isoformat()
    report["currency"] = "USD"
    report["purposes"] = policy.get("purposes") or []
    report["hardRules"] = policy.get("hardRules") or []
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    json_path = OUTPUT_ROOT / "inventory-ai-cost-report.json"
    markdown_path = OUTPUT_ROOT / "INVENTORY_AI_COST.md"
    json_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    markdown_path.write_text(render_markdown(report), encoding="utf-8")
    print(json.dumps({
        "passed": report["passed"],
        "totalBudgetUsd": usd(report["totalBudgetUsdMicros"]),
        "confirmedHistoricalUsageUsd": usd(
            report["confirmedHistoricalUsageUsdMicros"]
        ),
        "uncertainHistoricalReserveUsd": usd(
            report["uncertainHistoricalUsageReserveUsdMicros"]
        ),
        "newActualUsageUsd": usd(report["newActualUsageUsdMicros"]),
        "newCommittedUsageUsd": usd(
            report["newCommittedUsageUsdMicros"]
        ),
        "budgetAccountedTotalUsd": usd(
            report["budgetAccountedTotalUsdMicros"]
        ),
        "remainingBudgetUsd": usd(report["remainingBudgetUsdMicros"]),
        "entryCount": report["entryCount"],
        "failures": report["failures"],
        "report": str(json_path.relative_to(REPO_ROOT)),
    }, indent=2))
    return 0 if report["passed"] else 2


def render_markdown(report: dict) -> str:
    rows = [
        "# Inventory AI cost ledger",
        "",
        f"Generated: `{report['generatedAtUtc']}`",
        "",
        "| Measure | USD |",
        "|---|---:|",
        f"| Hard ceiling | {usd(report['totalBudgetUsdMicros']):.6f} |",
        f"| Confirmed historical usage | {usd(report['confirmedHistoricalUsageUsdMicros']):.6f} |",
        f"| Historical uncertainty reserve | {usd(report['uncertainHistoricalUsageReserveUsdMicros']):.6f} |",
        f"| New actual usage | {usd(report['newActualUsageUsdMicros']):.6f} |",
        f"| New committed usage | {usd(report['newCommittedUsageUsdMicros']):.6f} |",
        f"| Budget-accounted total | {usd(report['budgetAccountedTotalUsdMicros']):.6f} |",
        f"| Remaining | {usd(report['remainingBudgetUsdMicros']):.6f} |",
        "",
        "## Why the budget was used",
        "",
        "| Purpose | Amount/maximum | Reason |",
        "|---|---:|---|",
    ]
    for item in report.get("purposes") or []:
        amount = item.get("amountUsdMicros")
        if amount is None:
            amount = item.get("maximumAmountUsdMicros")
        reason = str(item.get("reason", "")).replace("|", "\\|")
        rows.append(
            f"| {item.get('code', '')} | {usd(int(amount or 0)):.6f}"
            f" | {reason} |"
        )
    rows.extend([
        "",
        "## Provider ledger",
        "",
        "| Entry | Source | Purpose | Model | Actual | Committed | Status |",
        "|---|---|---|---|---:|---:|---|",
    ])
    for item in report.get("entries") or []:
        rows.append(
            f"| {item.get('id', '')} | {item.get('sourceHash') or ''}"
            f" | {item.get('purpose', '')} | {item.get('modelId') or ''}"
            f" | {usd(int(item.get('actualCostUsdMicros') or 0)):.6f}"
            f" | {usd(int(item.get('committedCostUsdMicros') or 0)):.6f}"
            f" | {item.get('status') or ''} |"
        )
    rows.append("")
    return "\n".join(rows)


def usd(micros: int) -> float:
    return micros / 1_000_000


if __name__ == "__main__":
    raise SystemExit(main())
