"""Inventory AI budget and ledger regression tests."""

from __future__ import annotations

import importlib
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from inventory_ai_cost_ledger import load_policy, validate_budget


def test_budget_policy_accounts_for_historical_uncertainty() -> None:
    policy = load_policy()

    report = validate_budget((), policy)

    assert report["passed"]
    assert report["totalBudgetUsdMicros"] == 5_000_000
    assert report["confirmedHistoricalUsageUsdMicros"] == 90_935
    assert report["uncertainHistoricalUsageReserveUsdMicros"] == 97_187
    assert report["remainingBudgetUsdMicros"] == 4_811_878


def test_cost_and_bedrock_certification_commands_import() -> None:
    assert importlib.import_module("report_inventory_ai_cost")
    assert importlib.import_module("certify_inventory_corpus_bedrock")
