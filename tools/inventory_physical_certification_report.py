"""Render retained physical certification outcomes without changing their verdicts."""

from __future__ import annotations

from datetime import UTC, datetime
from typing import Any


def physical_baseline(
    document: dict[str, Any],
    source: dict[str, Any],
    import_view: dict[str, Any],
    passed: bool,
) -> dict[str, Any]:
    candidates = []
    for item in import_view.get("candidates") or []:
        candidates.append({
            "id": item.get("id"),
            "rowNumber": item.get("rowNumber"),
            "status": item.get("status"),
            "sourceLocator": item.get("sourceLocator"),
            "values": (
                item.get("canonicalValues")
                or item.get("proposedValues")
                or item.get("values")
                or {}
            ),
            "evidence": item.get("evidence") or [],
            "validation": item.get("validation") or [],
        })
    return {
        "schemaVersion": "advertified.inventory-physical-baseline.v1",
        "sourceHash": document["sha256"],
        "fileName": document["relativePath"],
        "importId": source["importId"],
        "physicalCertificationPassed": passed,
        "candidateCount": len(candidates),
        "candidates": candidates,
    }


def build_register(
    manifest: dict[str, Any],
    preflight: dict[str, Any],
    records: list[dict[str, Any]],
) -> dict[str, Any]:
    passed = [item for item in records if item["passed"]]
    failed = [item for item in records if not item["passed"]]
    return {
        "schemaVersion": "advertified.inventory-physical-certification.v2",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "sourceCount": len(records),
        "passedSourceCount": len(passed),
        "failedSourceCount": len(failed),
        "candidateCount": sum(item["candidate_count"] for item in records),
        "expectedAnchorCount": sum(
            item["expected_anchor_count"] for item in records
        ),
        "matchedAnchorCount": sum(
            item["matched_anchor_count"] for item in records
        ),
        "unsupportedCandidateCount": sum(
            item["unsupported_candidate_count"] for item in records
        ),
        "blockingCandidateCount": sum(
            item["blocking_candidate_count"] for item in records
        ),
        "bedrockLiveExecutionEnabled": bool(
            preflight.get("liveExecutionEnabled")
        ),
        "bedrockCommittedCostUsdMicros": int(
            preflight.get("existingCommittedCostUsdMicros") or 0
        ),
        "verdict": "PASS" if passed and len(records) == len(manifest["documents"]) and
        {item["source_hash"] for item in passed} ==
        {item["sha256"] for item in manifest["documents"]} and not failed else "FAIL",
        "documents": records,
    }


def render_markdown(register: dict[str, Any]) -> str:
    rows = [
        "# Corpus physical certification",
        "",
        f"Verdict: **{register['verdict']}**",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Sources | {register['sourceCount']} |",
        f"| Passed | {register['passedSourceCount']} |",
        f"| Failed | {register['failedSourceCount']} |",
        f"| Expected physical anchors | {register['expectedAnchorCount']} |",
        f"| Matched physical anchors | {register['matchedAnchorCount']} |",
        f"| API candidates | {register['candidateCount']} |",
        f"| Unsupported API candidates | {register['unsupportedCandidateCount']} |",
        "",
        "| File | Format | Candidates | Anchors | Matched | Verdict | Failures |",
        "|---|---|---:|---:|---:|---|---|",
    ]
    for item in register["documents"]:
        rows.append(
            f"| {escape(item['file_name'])} | {item['document_format']}"
            f" | {item['candidate_count']} | {item['expected_anchor_count']}"
            f" | {item['matched_anchor_count']}"
            f" | {'PASS' if item['passed'] else 'FAIL'}"
            f" | {escape(', '.join(item['failures']))} |"
        )
    rows.append("")
    return "\n".join(rows)


def escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def escape_markdown(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")
