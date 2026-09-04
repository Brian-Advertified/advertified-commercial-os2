"""Summarise current corpus physical-certification failures by root cause."""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
REPORT_ROOT = ROOT / "artifacts" / "inventory-corpus" / "physical-certification"
REPORT = REPORT_ROOT / "corpus-physical-certification.json"
OUTPUT = REPORT_ROOT / "corpus-physical-exception-summary.json"


def main() -> int:
    payload = read_json(REPORT)
    if "summary" in payload and "files" in payload:
        result = summarize_structural(payload)
    else:
        result = summarize_legacy(payload)
    OUTPUT.write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(result, indent=2))
    return 0


def summarize_structural(payload: dict[str, Any]) -> dict[str, Any]:
    blockers = Counter()
    unsupported = Counter()
    unsupported_fields = Counter()
    unmatched_kinds = Counter()
    unmatched_scopes = Counter()
    candidate_failures = Counter()
    failed_files: list[dict[str, Any]] = []
    for item in payload.get("files") or []:
        blockers.update(item.get("blockers") or [])
        if item.get("verdict") == "PASS":
            continue
        source_hash = str(item.get("sourceHash") or "")
        detail_path = REPORT_ROOT / f"{source_hash}.json"
        detail = read_json(detail_path)
        for audit in detail.get("candidate_audits") or []:
            if audit.get("verdict") == "FAIL":
                candidate_failures["FAILED_CANDIDATE"] += 1
            if audit.get("matched_unit_key") is None:
                candidate_failures["CANDIDATE_WITHOUT_UNIT_MATCH"] += 1
            if audit.get("duplicate_of"):
                candidate_failures["DUPLICATE_CANDIDATE"] += 1
            for reason in audit.get("unsupported_evidence") or []:
                parts = reason.split(":", 2)
                unsupported[parts[0]] += 1
                field = parts[1] if len(parts) > 1 else "UNKNOWN"
                unsupported_fields[f"{parts[0]}:{field}"] += 1
        for unit in detail.get("unmatched_physical_units") or []:
            unmatched_kinds[str(unit.get("kind") or "UNKNOWN")] += 1
            unmatched_scopes[str(unit.get("scope") or "UNKNOWN")] += 1
        failed_files.append({
            "fileName": item.get("fileName"),
            "format": item.get("format"),
            "physicalUnits": item.get("physicalUnits"),
            "candidates": item.get("candidates"),
            "matchedPhysicalUnits": item.get("matchedPhysicalUnits"),
            "unmatchedPhysicalUnits": item.get("unmatchedPhysicalUnits"),
            "unsupportedCandidates": item.get("unsupportedCandidates"),
            "duplicates": item.get("duplicates"),
            "blockers": item.get("blockers"),
        })
    summary = payload.get("summary") or {}
    return {
        "schemaVersion": "advertified.inventory-physical-exceptions.v2",
        "verdict": payload.get("verdict"),
        "sourceCount": summary.get("sourceCount"),
        "passedSourceCount": summary.get("passed"),
        "failedSourceCount": summary.get("failed"),
        "candidateCount": summary.get("candidateCount"),
        "physicalUnitCount": summary.get("physicalUnitCount"),
        "matchedPhysicalUnitCount": summary.get("matchedPhysicalUnitCount"),
        "unmatchedPhysicalUnitCount": summary.get("unmatchedPhysicalUnitCount"),
        "unsupportedCandidateCount": summary.get("unsupportedCandidateCount"),
        "duplicateCandidateCount": summary.get("duplicateCandidateCount"),
        "blockerCounts": dict(blockers.most_common()),
        "candidateFailureCounts": dict(candidate_failures.most_common()),
        "unsupportedEvidenceCounts": dict(unsupported.most_common()),
        "unsupportedEvidenceFieldCounts": dict(
            unsupported_fields.most_common()
        ),
        "unmatchedPhysicalUnitKinds": dict(unmatched_kinds.most_common()),
        "unmatchedPhysicalScopes": dict(unmatched_scopes.most_common(40)),
        "failedFiles": failed_files,
    }


def summarize_legacy(payload: dict[str, Any]) -> dict[str, Any]:
    documents = payload.get("documents") or []
    failures = Counter(
        failure.split(":", 1)[0]
        for document in documents
        for failure in document.get("failures") or []
    )
    return {
        "schemaVersion": "advertified.inventory-physical-exceptions.v1",
        "verdict": payload.get("verdict"),
        "sourceCount": payload.get("sourceCount"),
        "passedSourceCount": payload.get("passedSourceCount"),
        "failedSourceCount": payload.get("failedSourceCount"),
        "candidateCount": payload.get("candidateCount"),
        "expectedAnchorCount": payload.get("expectedAnchorCount"),
        "matchedAnchorCount": payload.get("matchedAnchorCount"),
        "unsupportedCandidateCount": payload.get("unsupportedCandidateCount"),
        "failureCounts": dict(failures.most_common()),
    }


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
