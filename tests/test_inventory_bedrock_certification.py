"""Offline safety tests for Bedrock inventory certification."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = str(ROOT / "tools")
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

from inventory_bedrock_response_validator import validate_response  # noqa: E402
import run_inventory_bedrock_certification as governed_runner  # noqa: E402
from prepare_inventory_bedrock_certification import (  # noqa: E402
    HISTORICAL_USAGE_RESERVE_USD_MICROS,
    PROGRAMME_LIMIT_USD_MICROS,
    cost_usd_micros,
)


def packet(*, ambiguous: bool = False, unavailable: bool = False) -> dict:
    row = {
        "physicalRowId": "row-1",
        "identityRaw": "DStv Stream VOD",
        "descriptionRaw": "Video pre roll 15 seconds",
        "geographyRaw": None,
        "rateRaw": "R1,10" if ambiguous else "R575",
        "rateAmountMinor": None if ambiguous else 57_500,
        "currency": "ZAR",
        "buyingBasisRaw": None,
        "availabilityRaw": "Not available" if unavailable else None,
        "ambiguityCodes": ["AMBIGUOUS_RATE"] if ambiguous else [],
        "evidence": [{
            "field": "rate",
            "raw": "R1,10" if ambiguous else "R575",
            "sourceLocator": "xlsx:A2",
        }],
    }
    return {
        "sourceHash": "a" * 64,
        "input": {"physicalRows": [row]},
    }


def response(*, availability: str = "AVAILABLE", rate_type=None, notes=None) -> dict:
    return {
        "sourceHash": "a" * 64,
        "products": [{
            "sourceRowIds": ["row-1"],
            "name": "DStv Stream VOD",
            "channel": "DIGITAL",
            "productType": "DIGITAL_PLACEMENT",
            "geography": None,
            "description": "DStv Stream VOD video pre roll 15 seconds",
            "rateSourceRowId": "row-1",
            "rateType": rate_type,
            "availability": availability,
            "validFrom": None,
            "validTo": None,
            "reviewNotes": notes or [],
        }],
        "nonProductFacts": [],
    }


def validate(request: dict, output: dict):
    return validate_response(
        request,
        output,
        {"DIGITAL"},
        {"DIGITAL_PLACEMENT"},
    )


def test_grounded_response_passes() -> None:
    result = validate(packet(), response())
    assert result.passed, result.failures
    assert result.covered_source_row_count == 1


def test_each_source_row_must_be_classified_once() -> None:
    output = response()
    output["nonProductFacts"] = [{
        "sourceRowId": "row-1",
        "reason": "duplicate",
        "attachToProductIndex": 0,
    }]
    result = validate(packet(), output)
    assert not result.passed
    assert "SOURCE_ROWS_CLASSIFIED_MORE_THAN_ONCE" in result.failures


def test_rate_type_cannot_be_invented_from_price() -> None:
    result = validate(packet(), response(rate_type="FLAT_RATE"))
    assert not result.passed
    assert "PRODUCT_1_RATE_TYPE_INFERRED_WITHOUT_BASIS" in result.failures


def test_ambiguous_rate_requires_review_note() -> None:
    result = validate(packet(ambiguous=True), response())
    assert not result.passed
    assert "PRODUCT_1_AMBIGUITY_NOTE_MISSING" in result.failures


def test_ambiguous_rate_with_note_passes() -> None:
    result = validate(
        packet(ambiguous=True),
        response(notes=["Ambiguous truncated rate requires human review."]),
    )
    assert result.passed, result.failures


def test_explicit_unavailable_must_be_preserved() -> None:
    result = validate(packet(unavailable=True), response())
    assert not result.passed
    assert "PRODUCT_1_UNAVAILABLE_SOURCE_NOT_PRESERVED" in result.failures


def test_default_availability_is_available() -> None:
    result = validate(packet(), response(availability="LIMITED"))
    assert not result.passed
    assert "PRODUCT_1_DEFAULT_AVAILABLE_POLICY_VIOLATED" in result.failures


def test_conservative_plan_must_fit_after_historical_reserve() -> None:
    projected = cost_usd_micros(1_000_000, 500_000)
    assert HISTORICAL_USAGE_RESERVE_USD_MICROS + projected < PROGRAMME_LIMIT_USD_MICROS
