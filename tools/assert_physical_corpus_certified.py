"""Fail unless every retained corpus source passed physical certification."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "artifacts" / "inventory-corpus" / "certification" / "physical-corpus-certification.json"


def main() -> int:
    payload = json.loads(REPORT.resolve(strict=True).read_text(encoding="utf-8"))
    summary = payload.get("summary") or {}
    failures = [item for item in payload.get("files") or [] if not item.get("passed")]
    passed = (
        payload.get("passed") is True
        and summary.get("sourceCount") == 43
        and summary.get("passedSourceCount") == 43
        and summary.get("failedSourceCount") == 0
        and summary.get("coreEmptyCandidateCount") == 0
        and summary.get("unmatchedCandidateRateCount") == 0
        and summary.get("duplicateCandidateCount") == 0
    )
    print(json.dumps({
        "passed": passed,
        "sourceCount": summary.get("sourceCount"),
        "passedSourceCount": summary.get("passedSourceCount"),
        "failedSourceCount": summary.get("failedSourceCount"),
        "coreEmptyCandidateCount": summary.get("coreEmptyCandidateCount"),
        "unmatchedCandidateRateCount": summary.get("unmatchedCandidateRateCount"),
        "duplicateCandidateCount": summary.get("duplicateCandidateCount"),
        "failedFiles": [
            {
                "fileName": item.get("file_name"),
                "failures": item.get("failures"),
            }
            for item in failures
        ],
    }, indent=2))
    return 0 if passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
