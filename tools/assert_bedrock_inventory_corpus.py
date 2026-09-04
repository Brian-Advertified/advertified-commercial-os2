"""Fail unless all 43 governed Bedrock outputs passed source-grounding review."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "artifacts" / "inventory-corpus" / "certification" / "bedrock-corpus-evaluation.json"


def main() -> int:
    report = json.loads(REPORT.resolve(strict=True).read_text(encoding="utf-8"))
    passed = (
        report.get("passed") is True
        and report.get("sourceCount") == 43
        and report.get("passedSourceCount") == 43
        and report.get("changedSourceFactCount") == 0
        and report.get("missingDescriptionCount") == 0
        and report.get("invalidSemanticCodeCount") == 0
        and int(report.get("historicalReserveUsdMicros") or 0)
        + int(report.get("committedCostUsdMicros") or 0)
        <= int(report.get("programmeLimitUsdMicros") or 0)
    )
    print(json.dumps({
        "passed": passed,
        "sourceCount": report.get("sourceCount"),
        "passedSourceCount": report.get("passedSourceCount"),
        "candidateCount": report.get("candidateCount"),
        "changedSourceFactCount": report.get("changedSourceFactCount"),
        "missingDescriptionCount": report.get("missingDescriptionCount"),
        "invalidSemanticCodeCount": report.get("invalidSemanticCodeCount"),
        "committedCostUsdMicros": report.get("committedCostUsdMicros"),
        "historicalReserveUsdMicros": report.get("historicalReserveUsdMicros"),
        "programmeLimitUsdMicros": report.get("programmeLimitUsdMicros"),
        "failureCount": len(report.get("failures") or []),
    }, indent=2))
    return 0 if passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
