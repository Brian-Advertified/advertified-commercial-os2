"""The preview preflight cannot reset old reservations or outrun its applied database policy."""
import importlib
import json
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.parametrize("committed,migrated,expected", [
    (4_330_000, True, 0),
    (9_950_000, True, 0),
    (9_950_001, True, 2),
    (4_330_000, False, 2),
])
def test_owner_budget_guard_preserves_prior_commitments(monkeypatch, tmp_path, committed, migrated, expected):
    monkeypatch.syspath_prepend(str(ROOT / "tools"))
    module = importlib.import_module("bedrock_preview_cost")
    monkeypatch.setattr(module, "ROOT", tmp_path)
    monkeypatch.setattr(module, "inspect_container", lambda: None)
    monkeypatch.setattr(module, "query_json", lambda sql: {
        "committedUsdMicros": committed,
        "ledgerMaximumUsdMicros": committed,
        "recordedActualUsdMicros": 0,
        "unreconciledCalls": 20,
        "reservationCount": 20,
        "ownerBudgetMigrationApplied": migrated,
    })
    assert module.guard() == expected
    report = json.loads((tmp_path / "bedrock-owner-budget-report.json").read_text(encoding="utf-8"))
    assert report["limitUsdMicros"] == 10_000_000
    assert report["committedUsdMicros"] == committed
    assert report["remainingUsdMicros"] == 10_000_000 - committed
    assert report["nextCallMaximumUsdMicros"] == 50_000
    assert report["passed"] == (expected == 0)
