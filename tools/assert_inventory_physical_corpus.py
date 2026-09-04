"""Assert that every corpus source has a current physical-file certification.

The command is read-only with respect to imports and never invokes Bedrock. It
cross-checks certification artifacts against the live OS2 API before writing the
corpus-level physical-validation result.
"""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_corpus_api import InventoryApi

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
MANIFEST_PATH = CORPUS_ROOT / "source-manifest.json"
CERTIFICATION_ROOT = CORPUS_ROOT / "physical-certification"
TENANT_ID = "10000000-0000-0000-0000-000000000020"
API_BASE = "http://127.0.0.1:5197"
RUNTIME_BASE = "http://127.0.0.1:5198"
ORIGIN = "http://localhost:3017"


def main() -> int:
    manifest = read_object(MANIFEST_PATH)
    documents = manifest.get("documents") or []
    client = InventoryApi(API_BASE, ORIGIN, TENANT_ID)
    client.start_session()
    preflight = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    )
    sources = {
        item["sourceHash"]: item for item in preflight.get("sources") or []
    }

    file_results: list[dict[str, Any]] = []
    all_failures: list[str] = []
    total_candidates = 0
    for position, document in enumerate(documents, start=1):
        result = validate_document(position, document, sources, client)
        file_results.append(result)
        total_candidates += int(result.get("candidateCount") or 0)
        all_failures.extend(
            f"{result['fileName']}:{failure}"
            for failure in result["failures"]
        )

    runtime = read_runtime()
    if not runtime["deterministicZeroCost"]:
        all_failures.append("RUNTIME_NOT_DETERMINISTIC_ZERO_COST")
    if bool(preflight.get("liveExecutionEnabled")):
        all_failures.append("BEDROCK_LIVE_EXECUTION_ENABLED")
    committed = int(preflight.get("existingCommittedCostUsdMicros") or 0)
    if committed != 0:
        all_failures.append("ACTIVE_BEDROCK_SCOPE_HAS_COMMITTED_COST")
    if len(documents) != 43 or len(sources) != 43:
        all_failures.append("CORPUS_SOURCE_COUNT_NOT_43")

    result = {
        "schemaVersion": "advertified.inventory-physical-corpus-certification.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "passed": not all_failures,
        "sourceCount": len(documents),
        "liveSourceCount": len(sources),
        "physicallyCertifiedSourceCount": sum(
            1 for item in file_results if item["passed"]
        ),
        "candidateCount": total_candidates,
        "publishedCandidateCount": sum(
            int(item.get("publishedCandidateCount") or 0)
            for item in file_results
        ),
        "bedrockLiveExecutionEnabled": bool(
            preflight.get("liveExecutionEnabled")
        ),
        "bedrockCommittedCostUsdMicros": committed,
        "runtimeDeterministicZeroCost": runtime["deterministicZeroCost"],
        "failures": all_failures,
        "files": file_results,
    }
    json_path = CERTIFICATION_ROOT / "corpus-physical-certification.json"
    md_path = CERTIFICATION_ROOT / "CORPUS_PHYSICAL_CERTIFICATION.md"
    write_json(json_path, result)
    md_path.write_text(render_markdown(result), encoding="utf-8")
    print(json.dumps({
        "passed": result["passed"],
        "sourceCount": result["sourceCount"],
        "physicallyCertifiedSourceCount": result[
            "physicallyCertifiedSourceCount"
        ],
        "candidateCount": result["candidateCount"],
        "publishedCandidateCount": result["publishedCandidateCount"],
        "bedrockLiveExecutionEnabled": result[
            "bedrockLiveExecutionEnabled"
        ],
        "bedrockCommittedCostUsdMicros": result[
            "bedrockCommittedCostUsdMicros"
        ],
        "failureCount": len(all_failures),
        "jsonArtifact": str(json_path.relative_to(REPO_ROOT)),
        "markdownArtifact": str(md_path.relative_to(REPO_ROOT)),
    }, indent=2))
    return 0 if result["passed"] else 2


def validate_document(
    position: int,
    document: dict[str, Any],
    sources: dict[str, dict[str, Any]],
    client: InventoryApi,
) -> dict[str, Any]:
    source_hash = str(document["sha256"])
    file_name = str(document["relativePath"])
    report_path = CERTIFICATION_ROOT / f"{source_hash}.json"
    marker_path = CERTIFICATION_ROOT / f"{source_hash}.pass.json"
    failures: list[str] = []

    report = read_optional_object(report_path, failures, "REPORT_MISSING")
    marker = read_optional_object(marker_path, failures, "PASS_MARKER_MISSING")
    source = sources.get(source_hash)
    if source is None:
        failures.append("LIVE_SOURCE_MISSING")
        import_view: dict[str, Any] = {}
    else:
        import_view = client.read_complete_import(str(source["importId"]))

    require(report.get("passed") is True, failures, "REPORT_NOT_PASSED")
    require(not (report.get("failures") or []), failures, "REPORT_HAS_FAILURES")
    require(report.get("source_hash") == source_hash, failures, "REPORT_HASH_MISMATCH")
    require(report.get("file_name") == file_name, failures, "REPORT_FILE_MISMATCH")
    require(marker.get("passed") is True, failures, "PASS_MARKER_NOT_PASSED")
    require(marker.get("sourceHash") == source_hash, failures, "PASS_MARKER_HASH_MISMATCH")
    require(marker.get("fileName") == file_name, failures, "PASS_MARKER_FILE_MISMATCH")

    candidates = import_view.get("candidates") or []
    attempts = import_view.get("extractionAttempts") or []
    latest = max(
        attempts,
        key=lambda item: int(item.get("attemptNumber") or 0),
        default={},
    )
    require(import_view.get("status") == "REVIEW_REQUIRED", failures, "IMPORT_NOT_REVIEW_REQUIRED")
    require(not import_view.get("failureCode"), failures, "IMPORT_FAILURE_PRESENT")
    require(latest.get("status") == "COMPLETED", failures, "LATEST_ATTEMPT_NOT_COMPLETED")
    require(
        source is not None and import_view.get("sourceHash") == source_hash,
        failures,
        "LIVE_IMPORT_HASH_MISMATCH",
    )
    require(
        int(report.get("candidate_count") or -1) == len(candidates),
        failures,
        "CANDIDATE_COUNT_MISMATCH",
    )
    require(
        int(report.get("unmatched_anchor_count") or 0) == 0,
        failures,
        "UNMATCHED_PHYSICAL_ANCHORS_REMAIN",
    )
    require(
        int(report.get("matched_anchor_count") or 0)
        == int(report.get("expected_anchor_count") or 0),
        failures,
        "PHYSICAL_ANCHOR_TOTAL_MISMATCH",
    )
    published = sum(
        1 for candidate in candidates
        if candidate.get("status") == "PUBLISHED"
    )
    require(published == 0, failures, "CANDIDATE_ALREADY_PUBLISHED")

    reported_provider_version = report.get("latest_attempt_provider_version")
    if reported_provider_version:
        require(
            reported_provider_version == latest.get("providerVersion"),
            failures,
            "CERTIFICATION_NOT_FOR_LATEST_PROVIDER_VERSION",
        )

    return {
        "position": position,
        "fileName": file_name,
        "sourceHash": source_hash,
        "documentClass": source.get("documentClass") if source else None,
        "importId": source.get("importId") if source else None,
        "latestAttemptNumber": latest.get("attemptNumber"),
        "latestAttemptProviderVersion": latest.get("providerVersion"),
        "candidateCount": len(candidates),
        "expectedAnchorCount": report.get("expected_anchor_count"),
        "matchedAnchorCount": report.get("matched_anchor_count"),
        "publishedCandidateCount": published,
        "passed": not failures,
        "failures": failures,
    }


def read_runtime() -> dict[str, Any]:
    response = requests.get(RUNTIME_BASE + "/health/ready", timeout=10)
    response.raise_for_status()
    payload = response.json()
    checks = payload.get("checks") or []
    return {
        "status": payload.get("status"),
        "deterministicZeroCost": "deterministic-zero-cost" in checks,
        "checks": checks,
    }


def require(condition: bool, failures: list[str], code: str) -> None:
    if not condition and code not in failures:
        failures.append(code)


def read_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


def read_optional_object(
    path: Path,
    failures: list[str],
    missing_code: str,
) -> dict[str, Any]:
    if not path.exists():
        failures.append(missing_code)
        return {}
    return read_object(path)


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def render_markdown(result: dict[str, Any]) -> str:
    lines = [
        "# Inventory corpus physical certification",
        "",
        f"Generated: `{result['generatedAtUtc']}`",
        "",
        f"**Verdict: {'PASS' if result['passed'] else 'FAIL'}**",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Physical source files | {result['sourceCount']} |",
        f"| Physically certified files | {result['physicallyCertifiedSourceCount']} |",
        f"| Extracted candidates | {result['candidateCount']} |",
        f"| Published candidates | {result['publishedCandidateCount']} |",
        f"| Bedrock live execution | {result['bedrockLiveExecutionEnabled']} |",
        f"| Active Bedrock committed cost (USD micros) | {result['bedrockCommittedCostUsdMicros']} |",
        "",
        "## Per-file result",
        "",
        "| # | File | Format | Candidates | Anchors | Result |",
        "|---:|---|---|---:|---:|---|",
    ]
    for item in result["files"]:
        lines.append(
            f"| {item['position']} | {escape(item['fileName'])} | "
            f"{item.get('documentClass') or ''} | {item['candidateCount']} | "
            f"{item.get('matchedAnchorCount') or 0}/"
            f"{item.get('expectedAnchorCount') or 0} | "
            f"{'PASS' if item['passed'] else 'FAIL'} |"
        )
    if result["failures"]:
        lines.extend(["", "## Blocking failures", ""])
        lines.extend(f"- `{failure}`" for failure in result["failures"])
    lines.extend([
        "",
        "Bedrock was not invoked by this certification command. Semantic evaluation "
        "must remain disabled until this report passes for all 43 files.",
        "",
    ])
    return "\n".join(lines)


def escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


if __name__ == "__main__":
    raise SystemExit(main())
