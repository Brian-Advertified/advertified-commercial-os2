"""Inventory AI budget and ledger regression tests."""

from __future__ import annotations

import importlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from ai_cost_catalog import delta, normalize_row
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


def test_agent_usage_minor_cost_is_converted_to_usd_micros() -> None:
    row = normalize_row(
        "ai_usage_ledger",
        {
            "id": "usage-1",
            "model_code": "us.amazon.nova-pro-v1:0",
            "incremental_cost_minor": 1,
        },
        "incremental_cost_minor",
        None,
    )

    assert row.model_id == "us.amazon.nova-pro-v1:0"
    assert row.actual_usd_micros == 10_000


def test_zero_cost_new_rows_do_not_satisfy_preview_usage_gate() -> None:
    zero = normalize_row(
        "ai_usage_ledger",
        {"id": "fixture-1", "incremental_cost_minor": 0},
        "incremental_cost_minor",
        None,
    )

    assert delta((), (zero,))["changeCount"] == 0
