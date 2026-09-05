"""Evaluate a confidential inventory extraction corpus without storing source documents."""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

REQUIRED_FORMATS = {"pdf", "spreadsheet", "presentation", "scan", "image"}
REVIEW_MODES = {"review-ready", "human-review-only"}
CRITICAL_FIELDS = {
    "supplier",
    "inventory_identity",
    "media_type",
    "location",
    "format_specification",
    "price",
    "currency",
    "vat_basis",
    "validity_date",
    "availability_exception",
}
THRESHOLDS = {
    "critical_field_precision": 0.99,
    "critical_field_recall": 0.95,
    "unsupported_critical_rate": 0.0,
    "unsupported_field_rate": 0.005,
    "row_column_association": 0.98,
    "numeric_currency_date_accuracy": 0.995,
    "table_cell_accuracy": 0.97,
    "ocr_confidence": 0.90,
    "critical_numeric_ocr_confidence": 0.95,
}
EXACT_TYPES = {"numeric", "currency", "date"}


@dataclass(frozen=True)
class Cell:
    document_id: str
    record_key: str
    field: str
    row: int
    column: int
    value: str
    evidence_pointer: str | None
    value_type: str
    extraction_method: str
    confidence: float | None

    @property
    def identity(self) -> tuple[str, str, str]:
        return self.document_id, self.record_key, self.field


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--holdout", action="store_true")
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    validate_manifest(manifest)
    documents = [
        item for item in manifest["documents"]
        if not args.holdout or item["partition"] == "holdout"
    ]
    report = evaluate(documents)
    report["dataset_version"] = manifest["datasetVersion"]
    report["partition"] = "holdout" if args.holdout else "all"
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report["passed"] else 1


def validate_manifest(manifest: dict[str, Any]) -> None:
    version = manifest.get("datasetVersion")
    documents = manifest.get("documents")
    if not isinstance(version, str) or not version.strip():
        raise ValueError("A versioned gold dataset is required.")
    if not isinstance(documents, list) or not documents:
        raise ValueError("The evaluation manifest must contain selected documents.")
    validate_document_set(documents)
    validate_release_modes(documents)
    validate_gold_data(documents)


def validate_document_set(documents: list[dict[str, Any]]) -> None:
    identifiers = [item.get("id") for item in documents]
    if len(set(identifiers)) != len(identifiers):
        raise ValueError("Corpus document identifiers must be unique.")
    formats = {str(item.get("format", "")).lower() for item in documents}
    if not REQUIRED_FORMATS.issubset(formats):
        raise ValueError("The corpus must cover PDF, spreadsheet, presentation, scan and image.")
    holdout = [item for item in documents if item.get("partition") == "holdout"]
    if len(holdout) < math.ceil(len(documents) * 0.20):
        raise ValueError("At least 20% of the corpus must remain in the holdout partition.")
    if any(not item.get("provenance") for item in documents):
        raise ValueError("Every confidential document requires provenance.")
    if any(item.get("partition") not in {"train", "holdout"} for item in documents):
        raise ValueError("Every document must belong to the train or holdout partition.")


def validate_release_modes(documents: list[dict[str, Any]]) -> None:
    if any(item.get("reviewMode") not in REVIEW_MODES for item in documents):
        raise ValueError(
            "Every document must be review-ready or explicitly human-review-only."
        )
    for format_name in REQUIRED_FORMATS:
        modes = {
            item["reviewMode"] for item in documents
            if str(item["format"]).lower() == format_name
        }
        if len(modes) != 1:
            raise ValueError("Each format must have one governed release mode.")


def validate_gold_data(documents: list[dict[str, Any]]) -> None:
    if any(not isinstance(item.get("goldCells"), list) or not item["goldCells"]
           for item in documents):
        raise ValueError("Every corpus document requires non-empty gold cells.")
    if any(item["reviewMode"] == "review-ready" and not any(
            str(cell.get("field")) in CRITICAL_FIELDS for cell in item["goldCells"])
           for item in documents):
        raise ValueError("Every review-ready document requires critical-field gold data.")
    if any(not isinstance(item.get("observedCells"), list) for item in documents):
        raise ValueError("Every corpus document requires observed extraction cells.")


def evaluate(documents: list[dict[str, Any]]) -> dict[str, Any]:
    review_ready = [
        item for item in documents if item["reviewMode"] == "review-ready"
    ]
    if not review_ready:
        raise ValueError("At least one review-ready document is required.")
    result = evaluate_metrics(review_ready)
    result["review_ready_document_count"] = len(review_ready)
    result["human_review_only_formats"] = sorted({
        str(item["format"]).lower() for item in documents
        if item["reviewMode"] == "human-review-only"
    })
    formats = {
        format_name: evaluate_metrics([
            item for item in review_ready
            if str(item["format"]).lower() == format_name
        ])
        for format_name in sorted({
            str(item["format"]).lower() for item in review_ready
        })
    }
    result["formats"] = formats
    format_failures = [
        f"{format_name}:{failure}"
        for format_name, report in formats.items()
        for failure in report["failures"]
    ]
    result["failures"] = [*result["failures"], *format_failures]
    result["passed"] = not result["failures"]
    result["document_count"] = len(documents)
    return result


def evaluate_metrics(documents: list[dict[str, Any]]) -> dict[str, Any]:
    gold = cells(documents, "goldCells")
    observed = cells(documents, "observedCells")
    gold_by_identity = {item.identity: item for item in gold}
    observed_by_identity = {item.identity: item for item in observed}
    accepted_critical = [item for item in observed if item.field in CRITICAL_FIELDS]
    gold_critical = [item for item in gold if item.field in CRITICAL_FIELDS]
    correct_critical = sum(
        exact(item, gold_by_identity.get(item.identity)) for item in accepted_critical
    )
    recalled_critical = sum(
        exact(observed_by_identity.get(item.identity), item) for item in gold_critical
    )
    unsupported = [item for item in observed if not item.evidence_pointer]
    associated = [
        item for item in observed
        if item.identity in gold_by_identity
    ]
    exact_values = [item for item in gold if item.value_type in EXACT_TYPES]
    ocr = [item.confidence for item in observed
           if item.extraction_method == "OCR" and item.confidence is not None]
    critical_numeric_ocr = [
        item.confidence for item in observed
        if item.extraction_method == "OCR" and item.field in CRITICAL_FIELDS
        and item.value_type in EXACT_TYPES and item.confidence is not None
    ]
    requires_ocr = any(str(item.get("format", "")).lower() in {"scan", "image"}
                       for item in documents)
    requires_critical_numeric_ocr = any(
        item.field in CRITICAL_FIELDS and item.value_type in EXACT_TYPES
        for item in gold
    )
    metrics = {
        "critical_field_precision": ratio(correct_critical, len(accepted_critical)),
        "critical_field_recall": ratio(recalled_critical, len(gold_critical)),
        "unsupported_critical_rate": ratio(
            sum(item.field in CRITICAL_FIELDS for item in unsupported),
            len(accepted_critical),
        ),
        "unsupported_field_rate": ratio(len(unsupported), len(observed)),
        "row_column_association": ratio(
            sum(position_matches(item, gold_by_identity[item.identity])
                for item in associated),
            len(observed),
        ),
        "numeric_currency_date_accuracy": ratio(
            sum(exact(observed_by_identity.get(item.identity), item)
                for item in exact_values),
            len(exact_values),
        ),
        "table_cell_accuracy": ratio(
            sum(exact(observed_by_identity.get(item.identity), item) for item in gold),
            max(len(gold), len(observed)),
        ),
        "ocr_confidence": min(ocr, default=0.0 if requires_ocr else 1.0),
        "critical_numeric_ocr_confidence": min(
            critical_numeric_ocr,
            default=0.0 if requires_critical_numeric_ocr and requires_ocr else 1.0),
    }
    failures = [
        name for name, threshold in THRESHOLDS.items()
        if metrics[name] > threshold and name.startswith("unsupported_")
        or metrics[name] < threshold and not name.startswith("unsupported_")
    ]
    return {
        "document_count": len(documents),
        "gold_cell_count": len(gold),
        "observed_cell_count": len(observed),
        "metrics": metrics,
        "thresholds": THRESHOLDS,
        "failures": failures,
        "passed": not failures,
    }


def cells(documents: list[dict[str, Any]], key: str) -> list[Cell]:
    result: list[Cell] = []
    for document in documents:
        for raw in document.get(key, []):
            result.append(Cell(
                document_id=document["id"],
                record_key=str(raw["recordKey"]),
                field=str(raw["field"]),
                row=int(raw["row"]),
                column=int(raw["column"]),
                value=str(raw["value"]),
                evidence_pointer=raw.get("evidencePointer"),
                value_type=str(raw.get("valueType", "text")),
                extraction_method=str(raw.get("extractionMethod", "TABULAR")),
                confidence=(float(raw["confidence"])
                            if raw.get("confidence") is not None else None),
            ))
    identities = [item.identity for item in result]
    if len(identities) != len(set(identities)):
        raise ValueError(f"{key} contains duplicate field identities.")
    return result


def exact(left: Cell | None, right: Cell | None) -> bool:
    return left is not None and right is not None and left.value == right.value


def position_matches(left: Cell, right: Cell) -> bool:
    return left.row == right.row and left.column == right.column


def ratio(numerator: int, denominator: int) -> float:
    return 1.0 if denominator == 0 else numerator / denominator


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (KeyError, TypeError, ValueError, json.JSONDecodeError) as error:
        print(json.dumps({"passed": False, "error": str(error)}), file=sys.stderr)
        sys.exit(2)
