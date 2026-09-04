"""Evaluate one observed inventory import against human-authored file gold."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("observed", type=Path)
    parser.add_argument("gold", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    observed = read_json(args.observed)
    gold = read_json(args.gold)
    report = evaluate(observed, gold)
    payload = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(payload, encoding="utf-8")
    else:
        print(payload, end="")
    return 0 if report["passed"] else 1


def evaluate(
    observed: dict[str, Any],
    gold: dict[str, Any],
) -> dict[str, Any]:
    failures: list[str] = []
    candidates = observed.get("candidates")
    if not isinstance(candidates, list):
        candidates = []
        failures.append("observed_candidates_missing")
    expected_hash = str(gold.get("documentId", ""))
    if observed.get("sourceHash") != expected_hash:
        failures.append("source_hash_mismatch")
    indexed = index_candidates(candidates, failures)
    compare_gold_cells(indexed, gold, failures)
    validate_semantics(indexed, gold, failures)
    validate_safety(observed, indexed, gold, failures)
    return {
        "schemaVersion": "advertified.inventory-file-evaluation.v1",
        "datasetVersion": gold.get("datasetVersion"),
        "documentId": expected_hash,
        "relativePath": gold.get("relativePath"),
        "passed": not failures,
        "verdict": "PASS" if not failures else "FAIL",
        "failureCount": len(failures),
        "failures": failures,
        "observedCandidateCount": len(candidates),
        "expectedCandidateCount": gold.get(
            "safetyExpectations", {}
        ).get("requiredCandidateCount"),
    }


def index_candidates(
    candidates: list[dict[str, Any]],
    failures: list[str],
) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for candidate in candidates:
        row = candidate.get("rowNumber")
        if not isinstance(row, int) or row < 1:
            failures.append("candidate_row_number_invalid")
            continue
        key = f"row-{row}"
        if key in result:
            failures.append(f"duplicate_candidate:{key}")
            continue
        result[key] = candidate
    return result


def compare_gold_cells(
    candidates: dict[str, dict[str, Any]],
    gold: dict[str, Any],
    failures: list[str],
) -> None:
    for cell in gold.get("goldCells", []):
        record = str(cell.get("recordKey", ""))
        field = str(cell.get("field", ""))
        expected = cell.get("value")
        candidate = candidates.get(record)
        if candidate is None:
            failures.append(f"missing_candidate:{record}")
            continue
        actual = candidate_value(candidate, field)
        if actual != expected:
            failures.append(
                f"field_mismatch:{record}:{field}:"
                f"expected={expected!r}:actual={actual!r}"
            )


def candidate_value(
    candidate: dict[str, Any],
    field: str,
) -> Any:
    values = candidate.get("values") or {}
    deliverable = values.get("deliverable") or {}
    mappings = {
        "inventory_identity": (
            values.get("productCode") or values.get("name")
        ),
        "media_type": values.get("channel"),
        "product_type": values.get("productType"),
        "placement": deliverable.get("placement"),
        "dimensions": deliverable.get("dimensions"),
        "format_specification": deliverable.get("format"),
        "currency": values.get("currency"),
        "description": values.get("description"),
    }
    if field == "supplier":
        return raw_evidence(candidate, "supplier_name")
    if field == "price":
        return raw_evidence(candidate, "rate")
    return mappings.get(field, values.get(field))


def validate_semantics(
    candidates: dict[str, dict[str, Any]],
    gold: dict[str, Any],
    failures: list[str],
) -> None:
    expected = gold.get("semanticExpectations") or {}
    channel = expected.get("requiredChannelCode")
    product_type = expected.get("requiredProductTypeCode")
    for key, candidate in candidates.items():
        values = candidate.get("values") or {}
        if channel and values.get("channel") != channel:
            failures.append(f"semantic_channel_mismatch:{key}")
        if product_type and values.get("productType") != product_type:
            failures.append(f"semantic_product_type_mismatch:{key}")
    evidence_locator = expected.get("descriptionEvidence")
    for key in expected.get("descriptionRequiredForRecordKeys", []):
        candidate = candidates.get(key)
        if candidate is None:
            continue
        values = candidate.get("values") or {}
        if not has_text(values.get("description")):
            failures.append(f"description_missing:{key}")
            continue
        if evidence_locator and not any(
            item.get("fieldName") == "description" and
            item.get("sourceLocator") == evidence_locator
            for item in candidate.get("evidence") or []
        ):
            failures.append(f"description_evidence_mismatch:{key}")
    for key in expected.get("descriptionForbiddenForRecordKeys", []):
        candidate = candidates.get(key)
        if candidate is not None and has_text(
                (candidate.get("values") or {}).get("description")):
            failures.append(f"description_forbidden:{key}")


def validate_safety(
    observed: dict[str, Any],
    candidates: dict[str, dict[str, Any]],
    gold: dict[str, Any],
    failures: list[str],
) -> None:
    safety = gold.get("safetyExpectations") or {}
    required_count = safety.get("requiredCandidateCount")
    if isinstance(required_count, int) and len(candidates) != required_count:
        failures.append("candidate_count_mismatch")
    for key in safety.get("requiredAmbiguityNoteRecordKeys", []):
        candidate = candidates.get(key)
        extension = (candidate or {}).get("values", {}).get("extension") or {}
        if not (
            extension.get("rateambiguity") == "AMBIGUOUS_TRUNCATED_RATE" or
            has_text(extension.get("sourceambiguity"))
        ):
            failures.append(f"ambiguity_note_missing:{key}")
    for key in safety.get("expectedNullNormalizedRateRecordKeys", []):
        candidate = candidates.get(key)
        values = (candidate or {}).get("values") or {}
        rate_evidence = field_evidence(candidate or {}, "rate")
        if values.get("rateAmountMinor") is not None or any(
                item.get("normalizedValue") is not None
                for item in rate_evidence):
            failures.append(f"ambiguous_rate_was_normalized:{key}")
    for key, expected in (
            safety.get("expectedRateAmountMinor") or {}).items():
        candidate = candidates.get(key)
        actual = (candidate or {}).get("values", {}).get(
            "rateAmountMinor"
        )
        if actual != expected:
            failures.append(
                f"rate_amount_mismatch:{key}:"
                f"expected={expected!r}:actual={actual!r}"
            )
    for field in safety.get("expectedUnknownFields", []):
        for key, candidate in candidates.items():
            if unknown_value(candidate, field) is not None:
                failures.append(f"expected_unknown_populated:{key}:{field}")
    serialized = json.dumps(observed, sort_keys=True)
    for value in safety.get("prohibitedInventedValues", []):
        if str(value) in serialized:
            failures.append(f"prohibited_value_present:{value}")
    if safety.get("uniqueFieldsPerCandidate"):
        for key, candidate in candidates.items():
            names = [
                str(item.get("fieldName"))
                for item in candidate.get("evidence") or []
            ]
            if len(names) != len(set(names)):
                failures.append(f"duplicate_evidence_field:{key}")
    if safety.get("publicationAllowed") is False:
        if observed.get("status") == "PUBLISHED" or any(
            candidate.get("status") == "PUBLISHED"
            for candidate in candidates.values()
        ):
            failures.append("publication_occurred")


def unknown_value(
    candidate: dict[str, Any],
    field: str,
) -> Any:
    values = candidate.get("values") or {}
    commercial = values.get("commercialTerms") or {}
    mappings = {
        "rate_type": values.get("rateType"),
        "rate_valid_from": commercial.get("rateValidFrom"),
        "rate_valid_to": commercial.get("rateValidTo"),
    }
    return mappings.get(field, values.get(field))


def field_evidence(
    candidate: dict[str, Any],
    field: str,
) -> list[dict[str, Any]]:
    return [
        item
        for item in candidate.get("evidence") or []
        if item.get("fieldName") == field
    ]


def raw_evidence(
    candidate: dict[str, Any],
    field: str,
) -> Any:
    evidence = field_evidence(candidate, field)
    return evidence[0].get("rawValue") if evidence else None


def has_text(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("Expected a JSON object.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
