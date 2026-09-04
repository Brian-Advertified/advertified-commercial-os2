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
from inventory_physical_certification_support import candidate_fact, match_inventory
from inventory_physical_facts import load_source, product_codes

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


def presentation_source_map(tmp_path: Path) -> Path:
    path = tmp_path / "presentation-source.json"
    path.write_text(json.dumps({
        "sourceHash": HASH,
        "relativePath": "Reveel - ZA - Publisher Media Kit.pptx",
        "format": "PPTX",
        "assets": [],
        "fragments": [
            {
                "locator": "pptx:slide=2;shape=4",
                "ordinal": 2,
                "kind": "IMAGE_OCR",
                "confidence": 0.0,
                "text": "Rv034 The Maslow Dagieos Sandton",
            },
            {
                "locator": "pptx:slide=5;shape=2",
                "ordinal": 5,
                "kind": "TEXT",
                "confidence": None,
                "text": "SANDTON\nRivonia Road",
            },
            {
                "locator": "pptx:slide=7;shape=2",
                "ordinal": 7,
                "kind": "TEXT",
                "confidence": None,
                "text": "SANDTON\nRivonia Road",
            },
            {
                "locator": "pptx:slide=13;shape=2",
                "ordinal": 13,
                "kind": "TEXT",
                "confidence": None,
                "text": "CAPE TOWN MARKET DIGITAL NETWORK\nLifestyle Markets",
            },
            {
                "locator": "pptx:slide=19;shape=1",
                "ordinal": 19,
                "kind": "TEXT",
                "confidence": None,
                "text": "JOHANNESBURG\nR24 / N1 2 Interchange",
            },
        ],
        "tables": [],
    }), encoding="utf-8")
    return path


def presentation_candidate(row: int, slide: int) -> dict:
    value = candidate(name="SANDTON - Rivonia Road", raw_rate="")
    value["id"] = f"candidate-{row}"
    value["rowNumber"] = row
    value["sourceLocator"] = f"pptx:slide={slide};shape=2"
    value["canonicalValues"].update({
        "channel": "DIGITAL",
        "productType": "DIGITAL_PLACEMENT",
        "geography": "SANDTON",
        "currency": None,
        "rateAmountMinor": None,
        "rateType": None,
    })
    value["evidence"] = [
        {
            "fieldName": "supplier_name",
            "rawValue": "Reveel ZA",
            "normalizedValue": "Reveel ZA",
            "sourceHash": HASH,
            "evidenceBasis": "DERIVED_POLICY",
        },
        {
            "fieldName": "name",
            "rawValue": "SANDTON\nRivonia Road",
            "normalizedValue": "SANDTON - Rivonia Road",
            "sourceHash": HASH,
            "evidenceBasis": "SUPPLIER_SUPPLIED",
        },
    ]
    return value


def test_low_confidence_image_ocr_code_is_not_a_physical_anchor(
    tmp_path: Path,
) -> None:
    anchors = discover_anchors(load_source(presentation_source_map(tmp_path)))

    assert not any(anchor.product_code == "RV034" for anchor in anchors)
    assert not any(
        "Rv034" in (anchor.identity or "") for anchor in anchors
    )


def test_two_line_brand_slogan_is_not_a_location_anchor(
    tmp_path: Path,
) -> None:
    anchors = discover_anchors(load_source(presentation_source_map(tmp_path)))

    assert not any(
        "architecture" in (anchor.identity or "").lower()
        for anchor in anchors
    )


def test_route_number_is_a_location_not_a_rate(tmp_path: Path) -> None:
    anchors = discover_anchors(load_source(presentation_source_map(tmp_path)))

    route = [
        anchor for anchor in anchors
        if "R24 / N1 2 Interchange" in (anchor.identity or "")
    ]
    assert len(route) == 1
    assert route[0].anchor_type == "SITE_LOCATION"
    assert route[0].raw_rate is None
    assert not any(anchor.raw_rate == "R24" for anchor in anchors)


def test_distinct_location_occurrences_cannot_share_one_candidate(
    tmp_path: Path,
) -> None:
    physical = load_source(presentation_source_map(tmp_path))
    anchors = tuple(
        anchor for anchor in discover_anchors(physical)
        if anchor.identity == "SANDTON | Rivonia Road"
    )
    candidates = (candidate_fact(presentation_candidate(1, 5)),)

    matches, unmatched, _ = match_inventory(physical, anchors, candidates)

    assert len(matches) == 1
    assert len(unmatched) == 1


def test_demographic_percentages_are_not_product_codes() -> None:
    assert product_codes("MALE 62% FEMALE 38% SEM CLUSTER 4") == ()


def test_route_name_with_location_suffix_is_not_a_rate(
    tmp_path: Path,
) -> None:
    path = tmp_path / "route-location-source.json"
    path.write_text(json.dumps({
        "sourceHash": HASH,
        "relativePath": "RSD Rate Cards - Gauteng - 2025.pptx",
        "format": "PPTX",
        "assets": [],
        "fragments": [{
            "locator": "pptx:slide=10;shape=2",
            "ordinal": 10,
            "kind": "TEXT",
            "confidence": None,
            "text": "R24 BEDFORDVIEW",
        }],
        "tables": [],
    }), encoding="utf-8")

    anchors = discover_anchors(load_source(path))

    assert not any(anchor.raw_rate == "R24" for anchor in anchors)


def test_supplier_name_evidence_is_accepted_as_supplier_reconstruction(
    tmp_path: Path,
) -> None:
    physical_path = tmp_path / "supplier-name-source.json"
    physical_path.write_text(json.dumps({
        "sourceHash": HASH,
        "relativePath": "supplier_inventory.xlsx",
        "format": "XLSX",
        "assets": [],
        "fragments": [],
        "tables": [{
            "locator": "xlsx:sheet=1:Inventory",
            "rows": [
                [{"value": "name"}, {"value": "base_price"}, {"value": "currency"}],
                [{"value": "Bedford screen"}, {"value": "15000"}, {"value": "ZAR"}],
            ],
        }],
    }), encoding="utf-8")
    item = candidate()
    item["evidence"][0]["fieldName"] = "supplier_name"

    result = certify_file(
        physical_path,
        import_view([item]),
        source(),
    )

    assert result.passed
