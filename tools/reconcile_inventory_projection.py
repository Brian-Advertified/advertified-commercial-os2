"""Reconcile independent source entries with canonical projection candidates."""

from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path
from typing import Any

if __package__:
    from .inventory_projection_reconciliation_values import (
        MONEY_FIELD, SITE_CANDIDATE, SITE_REFERENCE, candidate_units,
        file_sha256, money_values, normalized, rate_variant_candidates,
        reference_unit, scalar_values, source_page, source_site, tokens,
    )
else:
    from inventory_projection_reconciliation_values import (
        MONEY_FIELD, SITE_CANDIDATE, SITE_REFERENCE, candidate_units,
        file_sha256, money_values, normalized, rate_variant_candidates,
        reference_unit, scalar_values, source_page, source_site, tokens,
    )

SKIP_FIELDS = {
    "ambiguity", "catalogue", "column_context", "row_context", "raw_rate",
    "source_bbox", "source_cell", "source_context", "source_line",
    "duration_multiplier_table",
}
UNKNOWN = {"", "not stated", "not applicable", "none", "null", "tbc", "unknown"}


def candidate_view(candidate: dict[str, Any]) -> dict[str, Any]:
    values = candidate.get("values", {})
    evidence = candidate.get("evidence", [])
    flat = scalar_values(values) + scalar_values(evidence)
    rate_evidence = [
        item for item in evidence
        if MONEY_FIELD.search(str(item.get("fieldName") or ""))
    ]
    amounts = money_values(rate_evidence)
    minor = values.get("rateAmountMinor")
    if isinstance(minor, int):
        amounts.add(f"{minor / 100:.2f}")
    return {
        "candidate": candidate,
        "page": source_page(candidate.get("sourceLocator", "")),
        "site": source_site(candidate.get("sourceLocator", ""), SITE_CANDIDATE),
        "text": " ".join(flat),
        "tokens": tokens(" ".join(flat)),
        "money": amounts,
    }




def reference_view(reference: dict[str, Any]) -> dict[str, Any]:
    expected = reference.get("expected_fields", {})
    text = reference.get("identity", "") + " " + " ".join(scalar_values(expected))
    rate_fields = {
        key: value for key, value in expected.items()
        if MONEY_FIELD.search(str(key))
    }
    return {
        "reference": reference,
        "page": source_page(reference.get("locator", "")),
        "site": source_site(reference.get("locator", ""), SITE_REFERENCE),
        "tokens": tokens(text),
        "identity_tokens": tokens(reference.get("identity", "")),
        "money": money_values(rate_fields),
    }


def match_score(reference: dict[str, Any], candidate: dict[str, Any]) -> float:
    identity = reference["identity_tokens"]
    overlap = len(identity & candidate["tokens"]) / max(1, min(len(identity), 8))
    money = bool(reference["money"] & candidate["money"])
    page = reference["page"] is None or candidate["page"] is None or reference["page"] == candidate["page"]
    if not page:
        return 0.0
    if reference["site"] is not None and reference["site"] == candidate["site"]:
        return 4.0 + overlap
    # A repeated price on the same page is accounting evidence, not identity.
    if money and overlap > 0:
        return 2.0 + overlap
    return overlap if overlap >= 0.5 else 0.0


def match_entries(
    references: list[dict[str, Any]], candidates: list[dict[str, Any]]
) -> list[tuple[int, int, float]]:
    ref_views = [reference_view(item) for item in references]
    candidate_views = [candidate_view(item) for item in candidates]
    token_index: dict[str, set[int]] = defaultdict(set)
    site_index: dict[int, set[int]] = defaultdict(set)
    for index, candidate in enumerate(candidate_views):
        for token in candidate["tokens"]:
            token_index[token].add(index)
        if candidate["site"] is not None:
            site_index[candidate["site"]].add(index)
    edges = []
    for ref_index, reference in enumerate(ref_views):
        possible: set[int] = set()
        for token in reference["identity_tokens"]:
            possible.update(token_index.get(token, ()))
        if reference["site"] is not None:
            possible.update(site_index.get(reference["site"], ()))
        for candidate_index in possible:
            candidate = candidate_views[candidate_index]
            score = match_score(reference, candidate)
            if score >= 0.5:
                edges.append((score, ref_index, candidate_index))
    used_references: set[int] = set()
    used_candidates: set[int] = set()
    matches = []
    for score, ref_index, candidate_index in sorted(edges, reverse=True):
        if ref_index in used_references or candidate_index in used_candidates:
            continue
        used_references.add(ref_index)
        used_candidates.add(candidate_index)
        matches.append((ref_index, candidate_index, score))
    return matches


def field_items(reference: dict[str, Any]) -> list[tuple[str, str]]:
    result = [("identity", str(reference.get("identity", "")))]
    for key, value in reference.get("expected_fields", {}).items():
        if key in SKIP_FIELDS:
            continue
        if key == "source_fields" and isinstance(value, dict):
            result.extend((str(child_key), str(child)) for child_key, child in value.items())
        elif isinstance(value, list) and key == "source_rates":
            result.extend((key, str(child)) for child in value)
        elif not isinstance(value, (dict, list)):
            result.append((key, str(value)))
    return [item for item in result if normalized(item[1]) not in UNKNOWN]


def field_is_correct(key: str, expected: str, candidate: dict[str, Any]) -> bool:
    view = candidate_view(candidate)
    if MONEY_FIELD.search(key):
        return bool(money_values(expected) & view["money"])
    expected_normalized = normalized(expected)
    candidate_normalized = normalized(view["text"])
    if expected_normalized and expected_normalized in candidate_normalized:
        return True
    expected_tokens = tokens(expected)
    return bool(expected_tokens) and len(expected_tokens & view["tokens"]) / len(expected_tokens) >= 0.8


def reconcile_pair(
    reference: dict[str, Any], candidate: dict[str, Any], score: float
) -> dict[str, Any]:
    fields = [
        {
            "field": key,
            "expected": expected,
            "correct": field_is_correct(key, expected, candidate),
        }
        for key, expected in field_items(reference)
    ]
    values = candidate.get("values", {})
    expected_locator = reference.get("expected_fields", {}).get("source_cell")
    actual_locators = [candidate.get("sourceLocator", "")] + [
        item.get("sourceLocator", "") for item in candidate.get("evidence", [])
    ]
    locator_values = scalar_values(expected_locator)
    locator_correct = bool(locator_values) and any(
        expected == actual or expected in actual
        for expected in locator_values for actual in actual_locators
    )
    return {
        "reference_entry_id": reference["entry_id"],
        "candidate_row_number": candidate.get("rowNumber"),
        "candidate_source_locator": candidate.get("sourceLocator"),
        "candidate_product_code": values.get("productCode"),
        "candidate_name": values.get("name"),
        "match_score": score,
        "material_fields_correct": sum(item["correct"] for item in fields),
        "material_fields_compared": len(fields),
        "evidence_locator_compared": bool(locator_values),
        "evidence_locator_correct": locator_correct,
        "fields": fields,
    }


def reconcile_source(
    source_hash: str,
    references: list[dict[str, Any]],
    candidates: list[dict[str, Any]],
) -> dict[str, Any]:
    matches = match_entries(references, candidates)
    matched_references = {item[0] for item in matches}
    matched_candidates = {item[1] for item in matches}
    pairs = [
        reconcile_pair(references[ref_index], candidates[candidate_index], score)
        for ref_index, candidate_index, score in matches
    ]
    correct = sum(item["material_fields_correct"] for item in pairs)
    field_total = sum(item["material_fields_compared"] for item in pairs)
    return {
        "source_hash": source_hash,
        "reference_entries": len(references),
        "projected_candidates": len(candidates),
        "matched_entries": len(matches),
        "false_negatives": len(references) - len(matches),
        "false_positives": len(candidates) - len(matches),
        "material_fields_correct": correct,
        "material_fields_compared": field_total,
        "matched_pairs": pairs,
        "unmatched_reference_ids": [
            item["entry_id"] for index, item in enumerate(references)
            if index not in matched_references
        ],
        "unmatched_candidates": [
            {
                "row_number": item.get("rowNumber"),
                "source_locator": item.get("sourceLocator"),
                "product_code": item.get("values", {}).get("productCode"),
                "name": item.get("values", {}).get("name"),
            }
            for index, item in enumerate(candidates)
            if index not in matched_candidates
        ],
    }


def reconcile_unit(
    unit: str,
    references: list[dict[str, Any]],
    candidates: list[dict[str, Any]],
) -> dict[str, Any]:
    unit_references = [item for item in references if reference_unit(item) == unit]
    unit_candidates = [child for item in candidates for child in candidate_units(item, unit)]
    if not unit_references:
        return {
            "status": "NOT_MEASURABLE_NO_EXPLICIT_REFERENCE_UNITS",
            "reference_units": 0,
            "candidate_units": len(unit_candidates),
        }
    result = reconcile_source(unit, unit_references, unit_candidates)
    return {
        "status": "MEASURED_WITH_STRUCTURAL_UNIT_CLASSIFICATION",
        "reference_units": result["reference_entries"],
        "candidate_units": result["projected_candidates"],
        "matched_units": result["matched_entries"],
        "false_negatives": result["false_negatives"],
        "false_positives": result["false_positives"],
        "recall": result["matched_entries"] / len(unit_references),
        "precision": result["matched_entries"] / len(unit_candidates)
            if unit_candidates else 0,
    }


def source_accounting(observed_payloads: list[dict[str, Any]]) -> dict[str, Any]:
    reports = [item.get("response", {}).get("sourceAccounting") for item in observed_payloads]
    if not reports or any(report is None for report in reports):
        return {"status": "NOT_OBSERVABLE_IN_PROJECTION_ARTIFACT"}
    entries = [entry for report in reports for entry in report.get("entries", [])]
    def counts(key: Any) -> dict[str, int]:
        result: dict[str, int] = defaultdict(int)
        for entry in entries:
            result[key(entry)] += 1
        return dict(sorted(result.items()))
    return {
        "status": "MEASURED",
        "commercial_source_elements": len(entries),
        "accounted": sum(report["summary"]["accountedCommercialElements"] for report in reports),
        "unaccounted": sum(report["summary"]["unaccountedCommercialElements"] for report in reports),
        "first_failure_stage": counts(lambda item: item.get("firstFailureStage") or "NONE"),
        "document_type": counts(lambda item: item.get("structureKind") or "UNKNOWN"),
        "structural_pattern": counts(lambda item:
            "MATRIX" if item.get("headerHierarchy") else
            "TABLE" if "table" in str(item.get("structureKind", "")).lower() else "BLOCK"),
        "commercial_field": counts(lambda item: item.get("commercialSignal") or "UNKNOWN"),
        "terminal_disposition": counts(lambda item: item.get("terminalDisposition") or "UNKNOWN"),
        "exception_groups": sum(len(report.get("exceptionGroups", [])) for report in reports),
    }


def load_observed(path: Path) -> tuple[
    dict[str, list[dict[str, Any]]], str, dict[str, Any], list[dict[str, Any]]
]:
    result = {}
    binding_hashes = set()
    binding: dict[str, Any] = {}
    payloads = []
    for file in path.glob("*.json"):
        payload = json.loads(file.read_text(encoding="utf-8"))
        payloads.append(payload)
        response = payload["response"]
        result[response["sourceHash"]] = response["projectedCandidates"]
        binding_hashes.add(payload["binding_sha256"])
        binding = payload["implementation_binding"]
    if len(binding_hashes) != 1:
        raise ValueError(f"Observed outputs have {len(binding_hashes)} implementation bindings.")
    return result, binding_hashes.pop(), binding, payloads


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ledger", type=Path, required=True)
    parser.add_argument("--observed", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    ledger = json.loads(args.ledger.read_text(encoding="utf-8"))
    observed, binding_hash, binding, observed_payloads = load_observed(args.observed)
    references: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for item in ledger["commercial_entries"]:
        references[item["source_hash"]].append(item)
    if set(references) != set(observed):
        raise ValueError("Reference and observed source-hash coverage differ.")
    sources = [
        reconcile_source(source_hash, references[source_hash], observed[source_hash])
        for source_hash in sorted(references)
    ]
    totals = {
        key: sum(item[key] for item in sources)
        for key in (
            "reference_entries", "projected_candidates", "matched_entries",
            "false_negatives", "false_positives", "material_fields_correct",
            "material_fields_compared",
        )
    }
    totals["recall"] = totals["matched_entries"] / totals["reference_entries"]
    totals["precision"] = totals["matched_entries"] / totals["projected_candidates"]
    totals["field_correctness"] = totals["material_fields_correct"] / totals["material_fields_compared"]
    all_references = [item for values in references.values() for item in values]
    all_candidates = [item for values in observed.values() for item in values]
    unit_metrics = {
        unit: reconcile_unit(unit, all_references, all_candidates)
        for unit in ("product", "rate_variant", "package", "component")
    }
    compared_fields = [field for source in sources
        for pair in source["matched_pairs"] for field in pair["fields"]]
    critical = [field for field in compared_fields if
        MONEY_FIELD.search(field["field"]) or field["field"] in {
            "currency", "vat", "validity", "rate_type", "charging_basis"}]
    other = [field for field in compared_fields if field not in critical]
    locator_pairs = [pair for source in sources for pair in source["matched_pairs"]
        if pair["evidence_locator_compared"]]
    payload = {
        "schema_version": "advertified.inventory-projection-reconciliation/1.0",
        "reconciliation_algorithm": {
            "path": str(Path(__file__).resolve()),
            "sha256": file_sha256(Path(__file__)),
        },
        "implementation_binding_sha256": binding_hash,
        "implementation_binding": binding,
        "metric_definitions": {
            "recall": "one-to-one matched independent entries / independently enumerated entries",
            "precision": "one-to-one matched canonical candidates / canonical candidates",
            "field_correctness": "correct known material fields / known material fields compared on matched entries",
            "unit_metrics": "references and candidates are reconciled within product, rate-variant, package and component units; inferred reference units are explicitly labelled",
            "evidence_locator_correctness": "explicit reference source-cell locators found in candidate or field-evidence locators",
        },
        "totals": totals,
        "material_field_metrics": {
            "critical_financial": {
                "correct": sum(item["correct"] for item in critical),
                "compared": len(critical),
                "correctness": sum(item["correct"] for item in critical) / len(critical)
                    if critical else None,
            },
            "other_material": {
                "correct": sum(item["correct"] for item in other),
                "compared": len(other),
                "correctness": sum(item["correct"] for item in other) / len(other)
                    if other else None,
            },
            "evidence_locator": {
                "correct": sum(item["evidence_locator_correct"] for item in locator_pairs),
                "compared": len(locator_pairs),
                "correctness": sum(item["evidence_locator_correct"] for item in locator_pairs) /
                    len(locator_pairs) if locator_pairs else None,
            },
        },
        "unit_metrics": unit_metrics,
        "loss_funnel": source_accounting(observed_payloads),
        "sources": sources,
    }
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(totals, indent=2))


if __name__ == "__main__":
    main()
