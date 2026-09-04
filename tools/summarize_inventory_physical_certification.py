"""Summarise corpus physical-certification failures by root cause."""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = (
    ROOT / "artifacts" / "inventory-corpus" / "physical-certification"
    / "corpus-physical-certification.json"
)
OUTPUT = REPORT.with_name("corpus-physical-exception-summary.json")


def main() -> int:
    payload = json.loads(REPORT.resolve(strict=True).read_text(encoding="utf-8"))
    documents = payload.get("documents") or []
    failures = Counter(
        failure.split(":", 1)[0]
        for document in documents
        for failure in document.get("failures") or []
    )
    result = {
        "verdict": payload.get("verdict"),
        "sourceCount": payload.get("sourceCount"),
        "passedSourceCount": payload.get("passedSourceCount"),
        "failedSourceCount": payload.get("failedSourceCount"),
        "candidateCount": payload.get("candidateCount"),
        "expectedAnchorCount": payload.get("expectedAnchorCount"),
        "matchedAnchorCount": payload.get("matchedAnchorCount"),
        "unsupportedCandidateCount": payload.get("unsupportedCandidateCount"),
        "failureCounts": dict(failures.most_common()),
        "failedFiles": [
            {
                "fileName": item.get("file_name"),
                "format": item.get("document_format"),
                "candidates": item.get("candidate_count"),
                "anchors": item.get("expected_anchor_count"),
                "matched": item.get("matched_anchor_count"),
                "unsupported": item.get("unsupported_candidate_count"),
                "failures": item.get("failures"),
            }
            for item in documents
            if not item.get("passed")
        ],
    }
    OUTPUT.write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
