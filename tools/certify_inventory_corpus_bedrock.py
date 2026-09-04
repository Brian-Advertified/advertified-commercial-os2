"""Certify Bedrock enrichment against all physical inventory baselines."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_ai_cost_ledger import (
    load_policy,
    read_inventory_semantic_runs,
    validate_budget,
)
from inventory_corpus_api import InventoryApi
from inventory_physical_facts import load_source, normalize_compact

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"
ALLOWED_ROOT_CHANGES = {"channel", "productType", "description"}
ALLOWED_EXTENSION_PREFIXES = (
    "semantic", "search", "bedrock", "classification",
)


def main() -> int:
    args = parse_args()
    root = args.evidence.resolve(strict=True)
    physical_register = read_json(
        root / "physical-certification" / "corpus-physical-certification.json"
    )
    if (
        physical_register.get("verdict") != "PASS"
        or physical_register.get("passedSourceCount") != 43
    ):
        raise RuntimeError(
            "Bedrock certification is blocked until all 43 physical files pass."
        )

    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    preflight = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    )
    sources = preflight.get("sources") or []
    if len(sources) != 43:
        raise RuntimeError("The live semantic source set is not the 43-file corpus.")

    records = []
    for source in sorted(sources, key=lambda item: item["fileName"].lower()):
        source_hash = str(source["sourceHash"])
        baseline = read_json(
            root / "physical-certification" / "baseline"
            / f"{source_hash}.json"
        )
        physical = load_source(
            root / "semantic-v1" / f"{source_hash}.json"
        )
        current = client.read_complete_import(str(source["importId"]))
        records.append(certify_document(
            source,
            baseline,
            current,
            physical.searchable_text,
        ))

    policy = load_policy()
    ledger_entries = read_inventory_semantic_runs()
    cost = validate_budget(ledger_entries, policy)
    certified_hashes = {item["sourceHash"] for item in sources}
    ledger_failures = validate_ledger_content(
        ledger_entries,
        policy,
        certified_hashes,
    )
    failures = [
        f"{item['fileName']}:{failure}"
        for item in records
        for failure in item["failures"]
    ] + list(cost["failures"]) + ledger_failures

    output = root / "bedrock-certification"
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": "advertified.inventory-bedrock-certification.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "verdict": "PASS" if not failures else "FAIL",
        "sourceCount": len(records),
        "passedSourceCount": sum(item["passed"] for item in records),
        "failedSourceCount": sum(not item["passed"] for item in records),
        "candidateCount": sum(item["candidateCount"] for item in records),
        "changedPhysicalFieldCount": sum(
            item["changedPhysicalFieldCount"] for item in records
        ),
        "missingClassificationCount": sum(
            item["missingClassificationCount"] for item in records
        ),
        "missingDescriptionCount": sum(
            item["missingDescriptionCount"] for item in records
        ),
        "cost": cost,
        "failures": failures,
        "documents": records,
    }
    write_json(output / "corpus-bedrock-certification.json", report)
    (output / "CORPUS_BEDROCK_CERTIFICATION.md").write_text(
        render_markdown(report), encoding="utf-8"
    )
    print(json.dumps({
        "verdict": report["verdict"],
        "sourceCount": report["sourceCount"],
        "passedSourceCount": report["passedSourceCount"],
        "candidateCount": report["candidateCount"],
        "changedPhysicalFieldCount": report["changedPhysicalFieldCount"],
        "missingClassificationCount": report[
            "missingClassificationCount"
        ],
        "missingDescriptionCount": report["missingDescriptionCount"],
        "actualKnownTotalUsd": (
            cost["actualKnownTotalUsdMicros"] / 1_000_000
        ),
        "budgetAccountedTotalUsd": (
            cost["budgetAccountedTotalUsdMicros"] / 1_000_000
        ),
        "remainingBudgetUsd": cost["remainingBudgetUsdMicros"] / 1_000_000,
        "failureCount": len(failures),
    }, indent=2))
    return 0 if report["verdict"] == "PASS" else 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    return parser.parse_args()


def certify_document(
    source: dict[str, Any],
    baseline: dict[str, Any],
    current: dict[str, Any],
    physical_text: str,
) -> dict[str, Any]:
    failures: list[str] = []
    baseline_candidates = baseline.get("candidates") or []
    current_candidates = current.get("candidates") or []
    if not baseline.get("physicalCertificationPassed"):
        failures.append("PHYSICAL_BASELINE_NOT_CERTIFIED")
    if current.get("status") != "REVIEW_REQUIRED":
        failures.append("IMPORT_NOT_REVIEW_FENCED")
    if current.get("failureCode"):
        failures.append("IMPORT_FAILURE_PRESENT")
    if len(baseline_candidates) != len(current_candidates):
        failures.append("CANDIDATE_COUNT_CHANGED")

    baseline_by_key = candidate_index(baseline_candidates)
    current_by_key = candidate_index(current_candidates)
    if set(baseline_by_key) != set(current_by_key):
        failures.append("PHYSICAL_CANDIDATE_IDENTITY_SET_CHANGED")

    changed_physical = 0
    missing_classification = 0
    missing_description = 0
    for key in sorted(set(baseline_by_key).intersection(current_by_key)):
        before = baseline_by_key[key]
        after = current_by_key[key]
        changes = forbidden_changes(
            before.get("values") or {},
            values(after),
        )
        if changes:
            changed_physical += len(changes)
            failures.extend(
                f"PHYSICAL_FIELD_CHANGED:{key}:{field}"
                for field in changes
            )
        current_values = values(after)
        if not current_values.get("channel") or not current_values.get("productType"):
            missing_classification += 1
            failures.append(f"CLASSIFICATION_MISSING:{key}")
        description = str(current_values.get("description") or "").strip()
        if not description:
            missing_description += 1
            failures.append(f"DESCRIPTION_MISSING:{key}")
        elif not description_grounded(
            description,
            physical_text,
            str(current_values.get("name") or ""),
        ):
            failures.append(f"DESCRIPTION_NOT_GROUNDED:{key}")
        if str(after.get("status") or "") != "REVIEW_REQUIRED":
            failures.append(f"CANDIDATE_NOT_REVIEW_FENCED:{key}")
        if not source_evidence_preserved(
            before.get("evidence") or [],
            after.get("evidence") or [],
        ):
            failures.append(f"SOURCE_EVIDENCE_CHANGED:{key}")

    return {
        "sourceHash": source["sourceHash"],
        "fileName": source["fileName"],
        "importId": source["importId"],
        "candidateCount": len(current_candidates),
        "changedPhysicalFieldCount": changed_physical,
        "missingClassificationCount": missing_classification,
        "missingDescriptionCount": missing_description,
        "passed": not failures,
        "failures": list(dict.fromkeys(failures)),
    }


def candidate_index(candidates: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for item in candidates:
        item_values = values(item)
        evidence = item.get("evidence") or []
        raw_rate = next((
            str(entry.get("rawValue"))
            for entry in evidence
            if entry.get("fieldName") == "rate" and entry.get("rawValue")
        ), "")
        key = "|".join((
            str(item.get("rowNumber") or ""),
            normalize_compact(str(item.get("sourceLocator") or "")),
            normalize_compact(str(
                item_values.get("productCode")
                or item_values.get("name")
                or ""
            )),
            normalize_compact(raw_rate),
        ))
        if key in result:
            raise RuntimeError(f"Duplicate physical candidate key: {key}")
        result[key] = item
    return result


def forbidden_changes(before: dict[str, Any], after: dict[str, Any]) -> list[str]:
    before_clean = physical_values(before)
    after_clean = physical_values(after)
    keys = set(before_clean).union(after_clean)
    return [
        key for key in sorted(keys)
        if before_clean.get(key) != after_clean.get(key)
    ]


def physical_values(value: dict[str, Any]) -> dict[str, Any]:
    result = {
        key: item
        for key, item in value.items()
        if key not in ALLOWED_ROOT_CHANGES
    }
    extension = result.get("extension")
    if isinstance(extension, dict):
        result["extension"] = {
            key: item
            for key, item in extension.items()
            if not key.lower().startswith(ALLOWED_EXTENSION_PREFIXES)
        }
    return result


def description_grounded(
    description: str,
    physical_text: str,
    name: str,
) -> bool:
    source_tokens = tokens(physical_text + " " + name)
    description_tokens = tokens(description)
    descriptive = {
        value for value in description_tokens
        if value not in {
            "advertising", "placement", "inventory", "media", "available",
            "campaign", "audience", "product", "format", "platform",
        }
    }
    if descriptive and len(descriptive.intersection(source_tokens)) < min(3, len(descriptive)):
        return False
    source_numbers = set(re.findall(r"\d+(?:[.,]\d+)?", physical_text))
    description_numbers = set(re.findall(r"\d+(?:[.,]\d+)?", description))
    return description_numbers.issubset(source_numbers)


def source_evidence_preserved(
    before: list[dict[str, Any]],
    after: list[dict[str, Any]],
) -> bool:
    source_before = {
        evidence_signature(item)
        for item in before
        if item.get("evidenceBasis") != "DERIVED_POLICY"
    }
    source_after = {
        evidence_signature(item)
        for item in after
        if item.get("evidenceBasis") != "DERIVED_POLICY"
    }
    return source_before.issubset(source_after)


def evidence_signature(item: dict[str, Any]) -> tuple[str, ...]:
    return (
        str(item.get("fieldName") or ""),
        str(item.get("rawValue") or ""),
        str(item.get("normalizedValue") or ""),
        str(item.get("sourceLocator") or ""),
        str(item.get("sourceHash") or ""),
    )


def validate_ledger_content(
    entries,
    policy: dict[str, Any],
    certified_hashes: set[str],
) -> list[str]:
    scope = str(policy.get("activeBudgetScope") or "")
    failures: list[str] = []
    for item in entries:
        if item.budget_scope != scope:
            continue
        if item.source_hash and item.source_hash not in certified_hashes:
            failures.append(f"LEDGER_SOURCE_NOT_CERTIFIED:{item.entry_id}")
        request = json.dumps(item.request, sort_keys=True).lower()
        response = json.dumps(item.response, sort_keys=True).lower()
        if "base64" in request or "image_data" in request or "imagebytes" in request:
            failures.append(f"BEDROCK_REQUEST_CONTAINS_IMAGE:{item.entry_id}")
        if "source transcription" in request or "transcribe" in request:
            failures.append(f"BEDROCK_USED_FOR_TRANSCRIPTION:{item.entry_id}")
        if not response or response in {"null", "\"\""}:
            failures.append(f"BEDROCK_RESPONSE_MISSING:{item.entry_id}")
        if not item.source_hash:
            failures.append(f"LEDGER_SOURCE_HASH_MISSING:{item.entry_id}")
    return failures


def values(item: dict[str, Any]) -> dict[str, Any]:
    return (
        item.get("canonicalValues")
        or item.get("proposedValues")
        or item.get("values")
        or {}
    )


def tokens(value: str) -> set[str]:
    return {
        item.lower()
        for item in re.findall(r"[A-Za-z][A-Za-z0-9'-]{2,}", value)
    }


def render_markdown(report: dict[str, Any]) -> str:
    cost = report["cost"]
    rows = [
        "# Corpus Bedrock certification",
        "",
        f"Verdict: **{report['verdict']}**",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Sources | {report['sourceCount']} |",
        f"| Passed sources | {report['passedSourceCount']} |",
        f"| Candidates | {report['candidateCount']} |",
        f"| Changed physical fields | {report['changedPhysicalFieldCount']} |",
        f"| Missing classifications | {report['missingClassificationCount']} |",
        f"| Missing descriptions | {report['missingDescriptionCount']} |",
        f"| Budget-accounted total | ${cost['budgetAccountedTotalUsdMicros'] / 1_000_000:.6f} |",
        f"| Remaining budget | ${cost['remainingBudgetUsdMicros'] / 1_000_000:.6f} |",
        "",
        "| File | Candidates | Physical changes | Missing class | Missing description | Verdict |",
        "|---|---:|---:|---:|---:|---|",
    ]
    for item in report["documents"]:
        rows.append(
            f"| {escape(item['fileName'])} | {item['candidateCount']}"
            f" | {item['changedPhysicalFieldCount']}"
            f" | {item['missingClassificationCount']}"
            f" | {item['missingDescriptionCount']}"
            f" | {'PASS' if item['passed'] else 'FAIL'} |"
        )
    rows.append("")
    return "\n".join(rows)


def escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected an object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError,
        KeyError,
        ValueError,
        RuntimeError,
        json.JSONDecodeError,
        requests.RequestException,
    ) as error:
        print(json.dumps({"verdict": "ERROR", "error": str(error)}))
        raise SystemExit(3)
