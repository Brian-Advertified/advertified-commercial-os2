"""Focused acceptance tests for one-file physical-source certification."""

from __future__ import annotations

import importlib.util
import json
import sys
from copy import deepcopy
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
DOCUMENT_ID = (
    "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"
)
GOLD_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "gold" /
    f"{DOCUMENT_ID}.json"
)


def load_evaluator():
    path = REPO_ROOT / "tools" / "evaluate_inventory_file_gold.py"
    spec = importlib.util.spec_from_file_location(
        "evaluate_inventory_file_gold", path
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def load_certifier():
    load_evaluator()
    path = REPO_ROOT / "tools" / "certify_dms_two_stage.py"
    spec = importlib.util.spec_from_file_location(
        "certify_dms_two_stage", path
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def load_local_repair():
    tools_path = str(REPO_ROOT / "tools")
    if tools_path not in sys.path:
        sys.path.insert(0, tools_path)
    load_evaluator()
    path = REPO_ROOT / "tools" / "repair_dms_local.py"
    spec = importlib.util.spec_from_file_location(
        "repair_dms_local", path
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_local_repair_projects_api_candidates_for_file_gold() -> None:
    repair = load_local_repair()
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))
    expected = valid_observed(gold)
    api_view = {
        "sourceHash": DOCUMENT_ID,
        "status": "REVIEW_REQUIRED",
        "candidates": [
            {
                "rowNumber": item["rowNumber"],
                "status": item["status"],
                "sourceLocator": item["sourceLocator"],
                "canonicalValues": item["values"],
                "evidence": item["evidence"],
            }
            for item in expected["candidates"]
        ],
    }

    observed = repair.to_observed(api_view)
    report = repair.evaluate(observed, gold)

    assert report["verdict"] == "PASS"
    repair.validate_no_bedrock_cost({
        "liveExecutionEnabled": False,
        "existingCommittedCostUsdMicros": 0,
    })


def test_replayed_dms_certification_passes_file_gold() -> None:
    certifier = load_certifier()

    artifact, report = certifier.certify()

    assert report["verdict"] == "PASS"
    assert report["failureCount"] == 0
    assert report["observedCandidateCount"] == 4
    assert artifact["status"] == "REVIEW_REQUIRED"
    assert artifact["publicationAllowed"] is False
    assert artifact["certification"]["certificationSessionCostUsdMicros"] == 33_503


def test_two_stage_dms_result_passes_file_gold() -> None:
    evaluator = load_evaluator()
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))

    report = evaluator.evaluate(valid_observed(gold), gold)

    assert report["verdict"] == "PASS"
    assert report["failureCount"] == 0
    assert report["observedCandidateCount"] == 4


def test_retained_current_dms_artifact_passes_file_gold() -> None:
    evaluator = load_evaluator()
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))
    observed_path = (
        REPO_ROOT / "artifacts" / "inventory-corpus" / "observed" /
        f"{DOCUMENT_ID}.json"
    )
    observed = json.loads(observed_path.read_text(encoding="utf-8"))

    report = evaluator.evaluate(observed, gold)

    assert report["verdict"] == "PASS", report["failures"]
    assert report["observedCandidateCount"] == 4
    assert report["failures"] == []


def test_file_gold_rejects_invented_rate_basis_and_dates() -> None:
    evaluator = load_evaluator()
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))
    observed = valid_observed(gold)
    unsafe = deepcopy(observed)
    unsafe["candidates"][0]["values"]["rateType"] = "FLAT_RATE"
    unsafe["candidates"][0]["values"]["commercialTerms"] = {
        "rateValidFrom": "2024-01-01",
        "rateValidTo": "2024-12-31",
    }

    report = evaluator.evaluate(unsafe, gold)

    assert report["verdict"] == "FAIL"
    assert "expected_unknown_populated:row-1:rate_type" in report["failures"]
    assert "expected_unknown_populated:row-1:rate_valid_from" in report["failures"]
    assert "prohibited_value_present:FLAT_RATE" in report["failures"]
    assert "prohibited_value_present:2024-01-01" in report["failures"]


def test_file_gold_rejects_normalized_ambiguous_rate() -> None:
    evaluator = load_evaluator()
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))
    observed = valid_observed(gold)
    ambiguous = observed["candidates"][1]
    ambiguous["values"]["rateAmountMinor"] = 110
    next(item for item in ambiguous["evidence"]
         if item["fieldName"] == "rate")["normalizedValue"] = "110"

    report = evaluator.evaluate(observed, gold)

    assert report["verdict"] == "FAIL"
    assert "ambiguous_rate_was_normalized:row-2" in report["failures"]
    assert any(item.startswith("rate_amount_mismatch:row-2")
               for item in report["failures"])


def valid_observed(gold: dict) -> dict:
    rate_image = gold["sourceEvidence"]["rateTableImage"]
    positioning = gold["sourceEvidence"]["positioningImage"]
    rows = [
        ("DStv Stream VOD", "Video Pre Roll",
         "MP4 Skippable after 5 Seconds", "R575", 57_500, True),
        ("DStv Stream VOD", "Video Pre Roll",
         "MP4 15 seconds non skip", "R1,10", None, True),
        ("DStv Stream Live", "Video",
         "MP4", "R500", 50_000, True),
        ("You Tube", "Video Pre Roll",
         "MP4", "R200", 20_000, False),
    ]
    return {
        "sourceHash": DOCUMENT_ID,
        "status": "REVIEW_REQUIRED",
        "candidates": [
            candidate(
                index,
                *row,
                rate_image=rate_image,
                positioning=positioning,
            )
            for index, row in enumerate(rows, start=1)
        ],
    }


def candidate(
    row: int,
    name: str,
    placement: str,
    format_name: str,
    raw_rate: str,
    amount_minor: int | None,
    has_description: bool,
    *,
    rate_image: str,
    positioning: str,
) -> dict:
    values = {
        "name": name,
        "channel": "DIGITAL",
        "productType": "DIGITAL_PLACEMENT",
        "currency": "ZAR",
        "rateType": None,
        "rateAmountMinor": amount_minor,
        "commercialTerms": None,
        "description": (
            "DStv digital streaming content accessible across devices."
            if has_description else None
        ),
        "deliverable": {
            "placement": placement,
            "dimensions": "16 x 9",
            "format": format_name,
        },
        "extension": (
            {"rateambiguity": "AMBIGUOUS_TRUNCATED_RATE"}
            if row == 2 else {}
        ),
    }
    evidence = [
        field("supplier_name", "DStv Media Sales", None, rate_image),
        field("rate", raw_rate,
              str(amount_minor) if amount_minor is not None else None,
              rate_image),
        field("currency", raw_rate, "ZAR", rate_image),
        field("channel", "Streaming", "DIGITAL", rate_image),
        field("product_type", "Streaming", "DIGITAL_PLACEMENT", rate_image),
    ]
    if has_description:
        evidence.append(field(
            "description",
            "ACCESS ANYWHERE, ANY DEVICE, ANYTIME",
            values["description"],
            positioning,
        ))
    return {
        "rowNumber": row,
        "status": "REVIEW_REQUIRED",
        "sourceLocator": f"{rate_image};transcribed-row={row}",
        "values": values,
        "evidence": evidence,
    }


def field(
    name: str,
    raw: str,
    normalized: str | None,
    locator: str,
) -> dict:
    return {
        "fieldName": name,
        "rawValue": raw,
        "normalizedValue": normalized,
        "sourceLocator": locator,
    }
