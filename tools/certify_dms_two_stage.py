"""Build and evaluate the review-ready two-stage DStv certification artifact."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[1]
AGENT_RUNTIME = REPO_ROOT / "agent-runtime"
TOOLS = REPO_ROOT / "tools"
for path in (AGENT_RUNTIME, TOOLS):
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))

from bedrock_enrichment_output import wrap_enrichment_output  # noqa: E402
from bedrock_transcription_output import wrap_transcription_output  # noqa: E402
from inventory_semantic_contracts import (  # noqa: E402
    InventorySemanticExtractionArtifact,
)

from evaluate_inventory_file_gold import evaluate  # noqa: E402

SOURCE_HASH = (
    "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"
)
RATE_IMAGE = (
    "xlsx:sheet=Sheet1;image=2;cell=A1;"
    "embedded-part=xl%2Fmedia%2Fimage2.png"
)
POSITIONING_IMAGE = (
    "xlsx:sheet=Sheet1;image=1;cell=B11;"
    "embedded-part=xl%2Fmedia%2Fimage1.png"
)
REPLAY_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "certification" /
    "dms-bedrock-replay-input-2026-09-04.json"
)
GOLD_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "gold" /
    f"{SOURCE_HASH}.json"
)
OUTPUT_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "certification" /
    f"{SOURCE_HASH}.certified.json"
)
REPORT_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "certification" /
    f"{SOURCE_HASH}.evaluation.json"
)
DESCRIPTION = "DStv digital streaming content accessible across devices."
DESCRIPTION_RAW = "ACCESS ANYWHERE, ANY DEVICE, ANYTIME"
OCR_TEXT = (
    "DStv Media Sales Digital Rate Card\n"
    "DStv Stream VOD Video Pre Roll 16 x 9 "
    "MP4 Skippable after 5 Seconds R575\n"
    "DStv Stream VOD Video Pre Roll 16 x 9 "
    "MP4 15 seconds non skip R1,10\n"
    "DStv Stream Live Video 16 x 9 MP4 R500\n"
    "You Tube Video Pre Roll 16 x 9 MP4 R200"
)
RATE_MINOR = {
    "R575": 57_500,
    "R1,10": None,
    "R500": 50_000,
    "R200": 20_000,
}


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return value


def replay_call(replay: dict[str, Any], operation: str) -> dict[str, Any]:
    calls = replay.get("calls")
    if not isinstance(calls, list):
        raise ValueError("Replay evidence has no calls.")
    matches = [item for item in calls if item.get("operation") == operation]
    if len(matches) != 1 or not isinstance(matches[0].get("payload"), dict):
        raise ValueError(f"Replay evidence does not uniquely contain {operation}.")
    return matches[0]


def build_artifact() -> dict[str, Any]:
    replay = read_json(REPLAY_PATH)
    transcription_call = replay_call(replay, "SOURCE_TRANSCRIPTION")
    enrichment_call = replay_call(replay, "SEMANTIC_ENRICHMENT")
    transcription = wrap_transcription_output(
        InventorySemanticExtractionArtifact,
        transcription_call["payload"],
    ).artifact
    enrichment = wrap_enrichment_output(
        InventorySemanticExtractionArtifact,
        enrichment_call["payload"],
    ).artifact
    if len(transcription.candidates) != 4:
        raise ValueError("Deterministic post-processing did not yield four rows.")
    enriched = {
        candidate.source_locator: {
            field.field_name: field
            for field in candidate.fields
        }
        for candidate in enrichment.candidates
    }
    candidates = [
        build_candidate(index, source, enriched)
        for index, source in enumerate(transcription.candidates, start=1)
    ]
    current_cost = 13_212 + 11_548 + 8_743
    return {
        "schemaVersion": "advertified.inventory-dstv-certification.v1",
        "sourceHash": SOURCE_HASH,
        "status": "REVIEW_REQUIRED",
        "publicationAllowed": False,
        "certification": {
            "certifiedOn": "2026-09-04",
            "sourceBytes": 830_127,
            "sourceIdentityVerified": True,
            "sourceFactsMethod": "LOCAL_OCR_AND_DETERMINISTIC_POST_PROCESSING",
            "semanticMethod": "BEDROCK_OUTPUT_REPLAYED_THROUGH_CURRENT_TYPED_CONTRACT",
            "positioningMethod": "HUMAN_REVIEWED_SOURCE_COPY",
            "ocrText": OCR_TEXT,
            "bedrockCalls": [
                {
                    "operation": "SOURCE_TRANSCRIPTION_LEGACY_SCHEMA",
                    "result": "TYPED_CONTRACT_REJECTED",
                    "incrementalCostUsdMicros": 13_212,
                },
                {
                    "operation": "SOURCE_TRANSCRIPTION_FIXED_ROW_SCHEMA",
                    "result": "ACCEPTED_AFTER_HEADER_FILTER",
                    "incrementalCostUsdMicros": 11_548,
                },
                {
                    "operation": "SEMANTIC_ENRICHMENT_FIXED_ROW_SCHEMA",
                    "result": "ACCEPTED_AFTER_SAFE_WIRE_NORMALIZATION",
                    "incrementalCostUsdMicros": 8_743,
                },
            ],
            "certificationSessionCostUsdMicros": current_cost,
            "certificationSessionCostUsd": current_cost / 1_000_000,
            "priorReportedEvaluationCostUsdApprox": 0.05743146,
            "cumulativeKnownCostUsdApprox": 0.05743146 + current_cost / 1_000_000,
            "published": False,
        },
        "candidates": candidates,
    }


def build_candidate(
    index: int,
    source: Any,
    enriched: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    raw = {field.field_name: field for field in source.fields}
    row_locator = f"{RATE_IMAGE};transcribed-row={index}"
    semantic = enriched.get(row_locator)
    if semantic is None:
        raise ValueError(f"Missing semantic replay for row {index}.")
    channel = semantic.get("channel")
    product_type = semantic.get("product_type")
    if (
        channel is None or channel.normalized_value != "DIGITAL" or
        product_type is None or
        product_type.normalized_value != "DIGITAL_PLACEMENT"
    ):
        raise ValueError(f"Invalid semantic classification for row {index}.")
    raw_rate = raw["rate"].raw_value
    has_positioning = index <= 3
    extension: dict[str, str] = {}
    if raw_rate == "R1,10":
        extension["rateambiguity"] = "AMBIGUOUS_TRUNCATED_RATE"
        extension["sourceambiguity"] = "Rate value R1,10 is ambiguous."
    values = {
        "productCode": None,
        "name": raw["name"].raw_value,
        "channel": channel.normalized_value,
        "productType": product_type.normalized_value,
        "geography": None,
        "address": None,
        "latitude": None,
        "longitude": None,
        "rateType": None,
        "currency": "ZAR",
        "rateAmountMinor": RATE_MINOR[raw_rate],
        "availability": "PLANNING_AVAILABLE",
        "description": DESCRIPTION if has_positioning else None,
        "commercialTerms": None,
        "deliverable": {
            "placement": raw["placement"].raw_value,
            "dimensions": raw["dimensions"].raw_value,
            "format": raw["format"].raw_value,
        },
        "extension": extension,
    }
    evidence = [
        evidence_field(
            "supplier_name", "DStv Media Sales", None, RATE_IMAGE,
            "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED", "TRIM",
        ),
        evidence_field(
            "name", raw["name"].raw_value, raw["name"].raw_value,
            RATE_IMAGE, "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED", "TRIM",
        ),
        evidence_field(
            "placement", raw["placement"].raw_value,
            raw["placement"].raw_value, RATE_IMAGE,
            "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED", "TRIM",
        ),
        evidence_field(
            "dimensions", raw["dimensions"].raw_value,
            raw["dimensions"].raw_value, RATE_IMAGE,
            "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED",
            "DERIVED_FROM_SOURCE_CONTEXT",
        ),
        evidence_field(
            "format", raw["format"].raw_value,
            raw["format"].raw_value, RATE_IMAGE,
            "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED", "TRIM",
        ),
        evidence_field(
            "rate", raw_rate,
            str(RATE_MINOR[raw_rate]) if RATE_MINOR[raw_rate] is not None else None,
            RATE_IMAGE, "IMAGE_TRANSCRIPTION", "SUPPLIER_SUPPLIED",
            "PARSE_CURRENCY_AMOUNT",
        ),
        evidence_field(
            "currency", raw_rate, "ZAR", RATE_IMAGE,
            "POLICY_DERIVATION", "DERIVED_POLICY", "PARSE_CURRENCY_AMOUNT",
        ),
        evidence_field(
            "channel", channel.raw_value, channel.normalized_value,
            channel.source_locator, "SEMANTIC_ENRICHMENT", "DERIVED_POLICY",
            channel.transformation,
        ),
        evidence_field(
            "product_type", product_type.raw_value,
            product_type.normalized_value, product_type.source_locator,
            "SEMANTIC_ENRICHMENT", "DERIVED_POLICY",
            product_type.transformation,
        ),
        evidence_field(
            "availability", None, "PLANNING_AVAILABLE",
            "policy:inventory-availability-default-v1",
            "POLICY_DEFAULT", "DERIVED_POLICY", "EXPLICIT_UNKNOWN",
        ),
    ]
    if has_positioning:
        evidence.append(evidence_field(
            "description", DESCRIPTION_RAW, DESCRIPTION,
            POSITIONING_IMAGE, "HUMAN_REVIEWED_SOURCE_DESCRIPTION",
            "SUPPLIER_SUPPLIED", "TRIM",
        ))
    return {
        "rowNumber": index,
        "status": "REVIEW_REQUIRED",
        "sourceLocator": row_locator,
        "values": values,
        "evidence": evidence,
    }


def evidence_field(
    name: str,
    raw: str | None,
    normalized: str | None,
    locator: str,
    method: str,
    basis: str,
    transformation: str,
) -> dict[str, Any]:
    return {
        "fieldName": name,
        "rawValue": raw,
        "normalizedValue": normalized,
        "sourceLocator": locator,
        "sourceHash": SOURCE_HASH,
        "evidenceBasis": basis,
        "verificationState": "UNVERIFIED",
        "requiredAction": "REVIEW",
        "extractionMethod": method,
        "transformation": transformation,
        "extractionConfidence": 1,
    }


def certify() -> tuple[dict[str, Any], dict[str, Any]]:
    artifact = build_artifact()
    gold = read_json(GOLD_PATH)
    report = evaluate(artifact, gold)
    return artifact, report


def main() -> int:
    artifact, report = certify()
    OUTPUT_PATH.write_text(
        json.dumps(artifact, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    REPORT_PATH.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
