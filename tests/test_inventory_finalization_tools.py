"""Focused checks for certified inventory finalization tooling."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))


def load(name: str):
    path = TOOLS / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


class FakeApi:
    def __init__(self) -> None:
        self.calls: list[tuple[str, str, dict]] = []

    def request(self, method: str, path: str, body: dict | None = None):
        self.calls.append((method, path, body or {}))
        return {"id": "candidate-1", "version": 2, "status": "REVIEW_REQUIRED"}


def test_openapi_review_and_publish_operations_are_discoverable() -> None:
    module = load("inventory_review_http")
    client = module.InventoryReviewHttp(FakeApi(), "tenant-1")
    summary = client.contract_summary()
    assert "candidate" in summary["reviewPath"].lower()
    assert "publish" in summary["publishPath"].lower()


def test_safe_completion_never_invents_missing_rate() -> None:
    module = load("finalize_and_publish_inventory_corpus")
    values = {
        "name": "A grounded site",
        "channel": "OOH",
        "productType": "OOH_PLACEMENT",
        "rateAmountMinor": None,
        "availability": "PLANNING_AVAILABLE",
        "extension": {},
    }
    candidate = {
        "rowNumber": 1,
        "sourceLocator": "pdf:page=1",
        "evidence": [],
    }
    changes, unresolved = module.apply_safe_completion(
        values,
        candidate,
        "a" * 64,
        "Grounded OOH inventory.pdf",
        {"fragments": [], "tables": []},
    )
    assert values["rateAmountMinor"] is None
    assert "RATE_NOT_SUPPLIED_OR_QUOTE_REQUIRED" in unresolved
    assert values["availability"] == "AVAILABLE"
    assert "productCode" in changes


def test_source_supported_rate_type_derivation() -> None:
    module = load("finalize_and_publish_inventory_corpus")
    assert module.derive_rate_type(
        {"channel": "RADIO", "deliverable": {"daypart": "06:00-09:00"}},
        "Station rates.pdf",
        "TIME BAND NET RATES 30 second spot",
    ) == "SPOT_RATE"
    assert module.derive_rate_type(
        {"channel": "DOOH", "deliverable": {}},
        "OOH inventory.pdf",
        "Monthly rate per screen",
    ) == "MONTH_RATE"
    assert module.derive_rate_type(
        {"channel": "DIGITAL", "deliverable": {}},
        "Digital inventory.pdf",
        "R3 000 per post per platform",
    ) == "FLAT_RATE"


def test_unknown_buying_basis_remains_unknown() -> None:
    module = load("finalize_and_publish_inventory_corpus")
    assert module.derive_rate_type(
        {"channel": "DIGITAL", "deliverable": {}},
        "Digital inventory.pdf",
        "Visible price R575",
    ) is None
