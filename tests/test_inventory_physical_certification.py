"""Independent physical-certification regression tests."""

from __future__ import annotations

import json
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1] / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from inventory_physical_anchor_discovery import discover_anchors
from inventory_physical_certification import certify_file
from inventory_physical_facts import load_source

HASH = "a" * 64


def source_map(tmp_path: Path) -> Path:
    path = tmp_path / "source.json"
    path.write_text(json.dumps({
        "sourceHash": HASH,
        "relativePath": "supplier_inventory.xlsx",
        "format": "XLSX",
        "assets": [],
        "fragments": [],
        "tables": [{
            "locator": "xlsx:sheet=1:Inventory",
            "rows": [
                [
                    {"value": "name"},
                    {"value": "base_price"},
                    {"value": "currency"},
                ],
                [
                    {"value": "Bedford screen"},
                    {"value": "15000"},
                    {"value": "ZAR"},
                ],
            ],
        }],
    }), encoding="utf-8")
    return path


def candidate(*, name: str = "Bedford screen", raw_rate: str = "ZAR 15000"):
    return {
        "id": "candidate-1",
        "rowNumber": 1,
        "status": "REVIEW_REQUIRED",
        "sourceLocator": "xlsx:sheet=1:Inventory;row=2",
        "canonicalValues": {
            "name": name,
            "channel": "OOH",
            "productType": "OOH_SITE",
            "geography": "Bedfordview",
            "currency": "ZAR",
            "rateAmountMinor": 1_500_000,
            "rateType": "MONTH_RATE",
        },
        "evidence": [
            {
                "fieldName": "supplier",
                "rawValue": "Supplier",
                "normalizedValue": "Supplier",
                "sourceHash": HASH,
                "evidenceBasis": "DERIVED_POLICY",
            },
            {
                "fieldName": "name",
                "rawValue": "Bedford screen",
                "normalizedValue": name,
                "sourceHash": HASH,
                "evidenceBasis": "SUPPLIER_SUPPLIED",
            },
            {
                "fieldName": "rate",
                "rawValue": raw_rate,
                "normalizedValue": raw_rate,
                "sourceHash": HASH,
                "evidenceBasis": "SUPPLIER_SUPPLIED",
            },
        ],
        "validation": [],
    }


def import_view(candidates):
    return {
        "sourceHash": HASH,
        "status": "REVIEW_REQUIRED",
        "failureCode": None,
        "candidates": candidates,
        "extractionAttempts": [
            {"attemptNumber": 1, "status": "COMPLETED"}
        ],
    }


def source():
    return {
        "sourceHash": HASH,
        "importId": "00000000-0000-0000-0000-000000000001",
    }


def test_workbook_anchor_is_independently_discovered(tmp_path: Path) -> None:
    physical = load_source(source_map(tmp_path))

    anchors = discover_anchors(physical)

    assert len(anchors) == 1
    assert anchors[0].identity == "Bedford screen"
    assert anchors[0].raw_rate == "ZAR 15000"


def test_source_supported_workbook_candidate_passes(tmp_path: Path) -> None:
    result = certify_file(
        source_map(tmp_path), import_view([candidate()]), source()
    )

    assert result.passed
    assert result.matched_anchor_count == 1
    assert result.unsupported_candidate_count == 0


def test_invented_rate_fails_physical_certification(tmp_path: Path) -> None:
    result = certify_file(
        source_map(tmp_path),
        import_view([candidate(raw_rate="ZAR 99999")]),
        source(),
    )

    assert not result.passed
    assert "PHYSICAL_ANCHORS_UNCOVERED" in result.failures
    assert any(
        failure.startswith("RATE_NOT_SOURCE_SUPPORTED")
        for failure in result.failures
    )


def test_core_empty_candidate_is_not_certified(tmp_path: Path) -> None:
    empty = candidate(name="")
    empty["canonicalValues"] = {"availability": "AVAILABLE"}

    result = certify_file(
        source_map(tmp_path), import_view([empty]), source()
    )

    assert not result.passed
    assert "CORE_EMPTY_CANDIDATES_PRESENT" in result.failures
