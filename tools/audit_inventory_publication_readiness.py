"""Audit every physically and semantically certified candidate for publication."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_corpus_api import InventoryApi

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"
REQUIRED_FIELDS = (
    "productCode",
    "name",
    "channel",
    "productType",
    "geography",
    "rateType",
    "currency",
    "rateAmountMinor",
    "availability",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    args = parser.parse_args()

    root = args.evidence.resolve(strict=True)
    physical = read_json(
        root / "physical-certification" / "corpus-physical-certification.json"
    )
    bedrock = read_json(
        root / "bedrock-certification" / "corpus-bedrock-certification.json"
    )
    if physical.get("verdict") != "PASS" or physical.get("passedSourceCount") != 43:
        raise RuntimeError("Publication audit requires 43/43 physical certification.")
    if bedrock.get("verdict") != "PASS" or bedrock.get("passedSourceCount") != 43:
        raise RuntimeError("Publication audit requires 43/43 Bedrock certification.")

    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    preflight = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    )
    records = []
    missing_counts: Counter[str] = Counter()
    status_counts: Counter[str] = Counter()
    total = 0
    blocking = 0
    for source in sorted(
        preflight.get("sources") or [],
        key=lambda item: item["fileName"].lower(),
    ):
        current = client.read_complete_import(str(source["importId"]))
        candidates = current.get("candidates") or []
        document_missing: Counter[str] = Counter()
        document_blocking = 0
        rows = []
        for candidate in candidates:
            total += 1
            status = str(candidate.get("status") or "")
            status_counts[status] += 1
            values = candidate_values(candidate)
            pending_supplier = (
                (values.get("extension") or {}).get("pricingstatus")
                == "PENDING_SUPPLIER"
            )
            missing = [
                field for field in REQUIRED_FIELDS
                if not has_value(values.get(field))
                and not (
                    pending_supplier
                    and field in {
                        "rateType", "currency", "rateAmountMinor"
                    }
                )
            ]
            for field in missing:
                missing_counts[field] += 1
                document_missing[field] += 1
            issues = candidate.get("validation") or []
            blocking_issues = [
                issue for issue in issues if issue.get("isBlocking")
            ]
            blocking += len(blocking_issues)
            document_blocking += len(blocking_issues)
            if missing or blocking_issues:
                rows.append({
                    "candidateId": candidate.get("id"),
                    "rowNumber": candidate.get("rowNumber"),
                    "sourceLocator": candidate.get("sourceLocator"),
                    "name": values.get("name"),
                    "productCode": values.get("productCode"),
                    "missingFields": missing,
                    "blockingIssues": blocking_issues,
                    "rateRaw": raw_rate(candidate),
                })
        records.append({
            "sourceHash": source["sourceHash"],
            "fileName": source["fileName"],
            "importId": source["importId"],
            "importStatus": current.get("status"),
            "candidateCount": len(candidates),
            "missingFieldCounts": dict(document_missing),
            "blockingIssueCount": document_blocking,
            "readyCandidateCount": len(candidates) - len(rows),
            "notReadyCandidateCount": len(rows),
            "notReadyCandidates": rows[:200],
        })

    report = {
        "schemaVersion": "advertified.inventory-publication-readiness.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "sourceCount": len(records),
        "candidateCount": total,
        "candidateStatusCounts": dict(status_counts),
        "missingFieldCounts": dict(missing_counts),
        "blockingIssueCount": blocking,
        "readyCandidateCount": sum(
            item["readyCandidateCount"] for item in records
        ),
        "notReadyCandidateCount": sum(
            item["notReadyCandidateCount"] for item in records
        ),
        "verdict": "PASS" if not missing_counts and blocking == 0 else "FAIL",
        "documents": records,
    }
    output = root / "publication"
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "publication-readiness.json", report)
    print(json.dumps({
        "verdict": report["verdict"],
        "sourceCount": report["sourceCount"],
        "candidateCount": report["candidateCount"],
        "readyCandidateCount": report["readyCandidateCount"],
        "notReadyCandidateCount": report["notReadyCandidateCount"],
        "blockingIssueCount": report["blockingIssueCount"],
        "missingFieldCounts": report["missingFieldCounts"],
        "report": str(
            (output / "publication-readiness.json").relative_to(REPO_ROOT)
        ),
    }, indent=2))
    return 0 if report["verdict"] == "PASS" else 2


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )


def raw_rate(candidate: dict[str, Any]) -> str | None:
    return next((
        str(item.get("rawValue"))
        for item in candidate.get("evidence") or []
        if item.get("fieldName") == "rate" and item.get("rawValue")
    ), None)


def has_value(value: Any) -> bool:
    return value is not None and value != ""


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
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
