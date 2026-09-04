"""Plan explicit Nova Pro retries for semantic-only certification failures."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = (
    ROOT / "artifacts" / "inventory-corpus" / "bedrock-certification"
    / "corpus-bedrock-certification.json"
)
OUTPUT = REPORT.with_name("semantic-retry-plan.json")
RETRYABLE_PREFIXES = (
    "CLASSIFICATION_MISSING:",
    "DESCRIPTION_MISSING:",
    "DESCRIPTION_NOT_GROUNDED:",
)


def main() -> int:
    report = json.loads(
        REPORT.resolve(strict=True).read_text(encoding="utf-8")
    )
    retry = []
    non_retryable = []
    for document in report.get("documents") or []:
        failures = document.get("failures") or []
        if not failures:
            continue
        invalid = [
            failure
            for failure in failures
            if not str(failure).startswith(RETRYABLE_PREFIXES)
        ]
        if invalid:
            non_retryable.append({
                "fileName": document.get("fileName"),
                "failures": invalid,
            })
        else:
            retry.append({
                "fileName": document.get("fileName"),
                "sourceHash": document.get("sourceHash"),
                "failureCount": len(failures),
            })
    cost_failures = (report.get("cost") or {}).get("failures") or []
    if cost_failures:
        non_retryable.append({
            "fileName": None,
            "failures": cost_failures,
        })
    payload = {
        "schemaVersion": "advertified.inventory-semantic-retry-plan.v1",
        "initialVerdict": report.get("verdict"),
        "retryDocumentCount": len(retry),
        "retryDocuments": retry,
        "nonRetryableFailureCount": sum(
            len(item["failures"]) for item in non_retryable
        ),
        "nonRetryableFailures": non_retryable,
        "passed": not non_retryable,
    }
    OUTPUT.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(payload, indent=2))
    return 0 if payload["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
