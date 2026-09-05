"""Report extraction quality without copying or modifying confidential sources."""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path
from typing import Any

try:
    from tools.evaluate_inventory_file_gold import evaluate as evaluate_file_gold
except ModuleNotFoundError:  # Direct execution from the tools directory.
    from evaluate_inventory_file_gold import evaluate as evaluate_file_gold

CORE_FIELDS = (
    "productCode", "name", "channel", "productType", "geography",
    "rateType", "currency", "rateAmountMinor",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    report = build_report(args.source.resolve(strict=True), args.manifest.resolve(strict=True))
    payload = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(payload, encoding="utf-8")
    else:
        print(payload, end="")
    return 0 if report["verdict"] == "PASS" else 1


def build_report(source_root: Path, manifest_path: Path) -> dict[str, Any]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    documents = manifest.get("documents", [])
    if not documents or manifest.get("documentCount") != len(documents):
        raise ValueError("Document count must match the nonempty evaluation manifest.")
    observed_root = manifest_path.parent / "observed"
    totals: Counter[str] = Counter()
    reports = [inspect_document(source_root, observed_root, item, totals)
               for item in documents]
    return {
        "schemaVersion": "advertified.inventory-extraction-quality.v1",
        "datasetVersion": manifest.get("datasetVersion"),
        "verdict": "PASS" if all(item["semanticStatus"] == "PASS" for item in reports)
        else "FAIL",
        "reason": (
            "Durable transport is not semantic acceptance. Each document passes only after "
            "its observed output matches human-authored file-level gold."
        ),
        "sourceDocumentCount": len(documents),
        "sourceFilesAccountedFor": sum(item["physicalIdentityVerified"] for item in reports),
        "candidateCount": totals["candidates"],
        "candidatesWithNoCoreFields": totals["no_core"],
        "candidatesWithParsedRate": totals["rate"],
        "candidatesWithConfirmedAvailability": totals["confirmed_availability"],
        "candidatesWithPolicyDefaultAvailability": totals["policy_availability"],
        "supplierNotSuppliedDocumentCount": totals["supplier_not_supplied"],
        "paidAiCostUsd": 0.0,
        "documents": reports,
    }


def inspect_document(
    source_root: Path,
    observed_root: Path,
    document: dict[str, Any],
    totals: Counter[str],
) -> dict[str, Any]:
    relative = str(document["relativePath"])
    source = (source_root / Path(relative)).resolve(strict=True)
    if source_root not in source.parents:
        raise ValueError(f"Source escapes governed root: {relative}")
    physical_hash = hash_file(source)
    physical_verified = (
        physical_hash == document["sha256"] and source.stat().st_size == document["bytes"]
    )
    artifact = observed_root / f"{document['sha256']}.json"
    observed = json.loads(artifact.read_text(encoding="utf-8")) if artifact.exists() else {}
    candidates = observed.get("candidates", [])
    coverage = Counter()
    no_core = 0
    policy_default = 0
    confirmed = 0
    for candidate in candidates:
        values = candidate.get("values") or {}
        present = [field for field in CORE_FIELDS if has_value(values.get(field))]
        no_core += not present
        coverage.update(present)
        evidence = candidate.get("evidence") or []
        policy_default += any(
            item.get("fieldName") == "availability"
            and item.get("evidenceBasis") == "DERIVED_POLICY"
            for item in evidence
        )
        confirmed += values.get("availability") == "AVAILABLE" and any(
            item.get("fieldName") == "availability"
            and item.get("evidenceBasis") != "DERIVED_POLICY"
            and item.get("verificationState") == "VERIFIED"
            for item in evidence
        )
    totals.update(candidates=len(candidates), no_core=no_core,
                  rate=coverage["rateAmountMinor"], policy_availability=policy_default,
                  confirmed_availability=confirmed,
                  supplier_not_supplied=observed.get("supplierName") == "Not supplied")
    gold_path = observed_root.parent / "gold" / f"{document['sha256']}.json"
    evaluation = None
    if gold_path.exists():
        gold = json.loads(gold_path.read_text(encoding="utf-8"))
        evaluation = evaluate_file_gold(observed, gold)
    semantic_status = (
        "PASS"
        if evaluation and evaluation["passed"]
        else "FAILED_FILE_LEVEL_GOLD_COMPARISON"
        if evaluation
        else "FAILED_PENDING_FILE_LEVEL_GOLD_COMPARISON"
    )
    return {
        "relativePath": relative,
        "sha256": document["sha256"],
        "physicalIdentityVerified": physical_verified,
        "transportStatus": observed.get("status", "MISSING_ARTIFACT"),
        "candidateCount": len(candidates),
        "candidatesWithNoCoreFields": no_core,
        "fieldCoverageCounts": {field: coverage[field] for field in CORE_FIELDS},
        "policyDefaultAvailabilityCount": policy_default,
        "confirmedAvailabilityCount": confirmed,
        "supplierName": observed.get("supplierName"),
        "fileGoldAvailable": evaluation is not None,
        "fileGoldFailureCount": (
            evaluation["failureCount"] if evaluation else None
        ),
        "fileGoldFailures": (
            evaluation["failures"] if evaluation else []
        ),
        "semanticStatus": semantic_status,
    }


def has_value(value: Any) -> bool:
    return value is not None and (not isinstance(value, str) or bool(value.strip()))


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    raise SystemExit(main())
