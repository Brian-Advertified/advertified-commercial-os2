"""Print the current structural physical-certification evidence for one corpus file."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
REPORTS = CORPUS / "physical-certification"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--document", required=True)
    parser.add_argument("--maximum-examples", type=int, default=12)
    args = parser.parse_args()

    manifest = read_json(CORPUS / "source-manifest.json")
    document = next(
        (
            item for item in manifest.get("documents") or []
            if str(item.get("relativePath") or "") == args.document
        ),
        None,
    )
    if document is None:
        raise ValueError(f"Unknown corpus document: {args.document}")
    report = read_json(REPORTS / f"{document['sha256']}.json")
    failed = [
        item for item in report.get("candidate_audits") or []
        if item.get("verdict") == "FAIL"
    ]
    unmatched = report.get("unmatched_physical_units") or []
    payload = {
        "fileName": report.get("file_name"),
        "verdict": report.get("verdict"),
        "blockers": report.get("blockers"),
        "physicalUnitCount": report.get("physical_unit_count"),
        "candidateCount": report.get("candidate_count"),
        "matchedPhysicalUnitCount": report.get("matched_physical_unit_count"),
        "unmatchedPhysicalUnitCount": len(unmatched),
        "unmatchedKinds": dict(Counter(
            str(item.get("kind") or "UNKNOWN") for item in unmatched
        ).most_common()),
        "failedCandidateCount": len(failed),
        "failureReasons": dict(Counter(
            reason.split(":", 1)[0]
            for item in failed
            for reason in item.get("unsupported_evidence") or []
        ).most_common()),
        "unmatchedExamples": unmatched[: args.maximum_examples],
        "failedCandidateExamples": failed[: args.maximum_examples],
    }
    print(json.dumps(payload, indent=2))
    return 0


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
