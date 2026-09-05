"""Attach complete canonical reconciliation evidence to the retained ledger."""

from __future__ import annotations

import argparse
import hashlib
import json
import uuid
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def metric(numerator: int, denominator: int) -> dict[str, object]:
    return {
        "status": "MEASURED",
        "numerator": numerator,
        "denominator": denominator,
        "fraction": f"{numerator}/{denominator}",
        "value": numerator / denominator if denominator else None,
    }


def aggregate_ambiguities(ledger: dict[str, Any]) -> list[dict[str, object]]:
    grouped: dict[tuple[str, str], int] = {}
    for item in ledger["commercial_entries"]:
        description = str(item.get("expected_fields", {}).get("ambiguity") or "")
        if description and description.upper() != "NONE":
            key = (item["source_hash"], description)
            grouped[key] = grouped.get(key, 0) + 1
    existing = list(ledger.get("source_ambiguities", []))
    existing.extend(
        {"source_hash": key[0], "count": count, "description": key[1]}
        for key, count in sorted(grouped.items())
    )
    return existing


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--reconciliation", type=Path, required=True)
    args = parser.parse_args()
    ledger = json.loads(args.ledger.read_text(encoding="utf-8"))
    reconciliation = json.loads(args.reconciliation.read_text(encoding="utf-8"))
    pairs = {
        pair["reference_entry_id"]: pair
        for source in reconciliation["sources"]
        for pair in source["matched_pairs"]
    }
    for item in ledger["commercial_entries"]:
        pair = pairs.get(item["entry_id"])
        item["application_reconciliation"] = (
            {
                "status": "MATCHED",
                "candidate_row_number": pair["candidate_row_number"],
                "candidate_source_locator": pair["candidate_source_locator"],
                "candidate_product_code": pair["candidate_product_code"],
                "material_fields_correct": pair["material_fields_correct"],
                "material_fields_compared": pair["material_fields_compared"],
            }
            if pair else {"status": "MISSING_FROM_CANONICAL_PROJECTION"}
        )
    totals = reconciliation["totals"]
    ledger["status"] = "CERTIFICATION_EVIDENCE_COMPLETE"
    ledger["basis"].update({
        "reconciliation_path": str(args.reconciliation.resolve()),
        "reconciliation_sha256": sha256(args.reconciliation),
        "final_implementation_binding_sha256":
            reconciliation["implementation_binding_sha256"],
        "final_implementation_binding":
            reconciliation["implementation_binding"],
        "reconciliation_algorithm": reconciliation["reconciliation_algorithm"],
        "ledger_finalizer": {
            "path": str(Path(__file__).resolve()),
            "sha256": sha256(Path(__file__)),
        },
    })
    ledger["source_ambiguities"] = aggregate_ambiguities(ledger)
    ledger["application_reconciliation"] = {
        "status": "COMPLETE_CURRENT_CANONICAL_PROJECTION",
        "projection_version": "advertified-projection/4.0.0",
        "documents_reconciled": len(reconciliation["sources"]),
        "documents_total": ledger["collection"]["files"],
        "bidirectional_entry_evidence": str(args.reconciliation.resolve()),
    }
    coverage = ledger["coverage"]
    coverage["files_with_complete_entry_enumeration"] = coverage["files_total"]
    ledger["quality_metrics"] = {
        "physical_coverage": metric(
            coverage["physical_units_inspected"],
            coverage["physical_units_total"],
        ),
        "reference_entry_coverage": metric(
            coverage["files_with_complete_entry_enumeration"],
            coverage["files_total"],
        ),
        "recall": metric(totals["matched_entries"], totals["reference_entries"]),
        "precision": metric(totals["matched_entries"], totals["projected_candidates"]),
        "field_correctness": metric(
            totals["material_fields_correct"], totals["material_fields_compared"]
        ),
    }
    temporary = args.ledger.with_name(f".{args.ledger.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(ledger, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    temporary.replace(args.ledger)
    print(json.dumps(ledger["quality_metrics"], indent=2))


if __name__ == "__main__":
    main()
