"""Fail closed unless one corpus file has a passing physical certification.

This command performs no extraction and makes no provider calls. It is intended
for chaining the one-file-at-a-time production certification workflow.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
MANIFEST = REPO_ROOT / "artifacts" / "inventory-corpus" / "source-manifest.json"
CERTIFICATIONS = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "physical-certification"
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--document", required=True)
    args = parser.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    matches = [
        item for item in manifest.get("documents", [])
        if item.get("relativePath") == args.document
    ]
    if len(matches) != 1:
        raise SystemExit(f"Expected one manifest entry for {args.document!r}.")

    source_hash = matches[0]["sha256"]
    report_path = CERTIFICATIONS / f"{source_hash}.json"
    report = json.loads(report_path.resolve(strict=True).read_text(encoding="utf-8"))
    failures = report.get("failures") or []
    passed = (
        report.get("passed") is True
        and report.get("source_hash") == source_hash
        and report.get("file_name") == args.document
        and report.get("latest_attempt_status") == "COMPLETED"
        and report.get("import_status") == "REVIEW_REQUIRED"
        and not failures
    )
    summary = {
        "fileName": args.document,
        "sourceHash": source_hash,
        "passed": passed,
        "candidateCount": report.get("candidate_count"),
        "matchedAnchorCount": report.get("matched_anchor_count"),
        "expectedAnchorCount": report.get("expected_anchor_count"),
        "unmatchedAnchorCount": report.get("unmatched_anchor_count"),
        "failures": failures,
        "bedrockCalled": False,
    }
    print(json.dumps(summary, indent=2))
    marker = CERTIFICATIONS / f"{source_hash}.pass.json"
    if passed:
        marker.write_text(
            json.dumps(summary, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
    elif marker.exists():
        marker.unlink()
    return 0 if passed else 2


if __name__ == "__main__":
    raise SystemExit(main())
