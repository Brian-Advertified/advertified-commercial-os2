"""Inventory AI budget and ledger regression tests."""

from __future__ import annotations

import importlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from inventory_ai_cost_ledger import validate_budget


def test_budget_policy_accounts_for_historical_uncertainty() -> None:
    policy = {
        "activeBudgetScope": "synthetic-evaluation",
        "totalBudgetUsdMicros": 10_000,
        "perCallMaximumUsdMicros": 2_000,
        "confirmedHistoricalUsageUsdMicros": 1_000,
        "uncertainHistoricalUsageReserveUsdMicros": 500,
    }

    report = validate_budget((), policy)

    assert report["passed"]
    assert report["totalBudgetUsdMicros"] == 10_000
    assert report["confirmedHistoricalUsageUsdMicros"] == 1_000
    assert report["uncertainHistoricalUsageReserveUsdMicros"] == 500
    assert report["remainingBudgetUsdMicros"] == 8_500


def test_read_only_cost_report_imports_without_private_evaluation_data() -> None:
    assert importlib.import_module("report_inventory_ai_cost")
