"""Repair the governed DMS workbook with local Docling only, then verify file gold."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import requests

from evaluate_inventory_file_gold import evaluate
from inventory_corpus_api import InventoryApi

TENANT_ID = "10000000-0000-0000-0000-000000000020"
IMPORT_ID = "7e536ea1-f4fa-4e2e-963e-c9fc796e5ac3"
SOURCE_HASH = (
    "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"
)
REPO_ROOT = Path(__file__).resolve().parents[1]
GOLD_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "gold" /
    f"{SOURCE_HASH}.json"
)
OUTPUT_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus" / "certification"


def main() -> int:
    assert_zero_cost_runtime()
    client = InventoryApi(
        "http://127.0.0.1:5197",
        "http://localhost:3017",
        TENANT_ID,
    )
    client.start_session()
    current = client.read_complete_import(IMPORT_ID)
    validate_reprojection_start(current)
    attempts = current.get("extractionAttempts") or []
    next_attempt = max(
        (int(item["attemptNumber"]) for item in attempts),
        default=0,
    ) + 1
    client.reproject_import(
        IMPORT_ID,
        int(current["version"]),
        SOURCE_HASH,
        next_attempt,
    )
    repaired = client.wait_for_extraction(
        IMPORT_ID,
        poll_seconds=2,
        max_wait_seconds=900,
        observe=lambda _: None,
        checkpoint=lambda: None,
    )
    if repaired.get("status") != "REVIEW_REQUIRED":
        raise RuntimeError("The local reprojection did not return to human review.")

    complete = client.read_complete_import(IMPORT_ID)
    observed = to_observed(complete)
    gold = json.loads(GOLD_PATH.read_text(encoding="utf-8"))
    report = evaluate(observed, gold)
    preflight = client.request(
        "GET",
        client.tenant_path(
            "/inventory-semantic-preflight?importId=" + IMPORT_ID
        ),
    )
    validate_no_bedrock_cost(preflight)

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    observed_path = OUTPUT_ROOT / f"{SOURCE_HASH}.local-reprojection.json"
    report_path = OUTPUT_ROOT / f"{SOURCE_HASH}.local-evaluation.json"
    write_json(observed_path, observed)
    write_json(report_path, report)

    summary = {
        "importId": IMPORT_ID,
        "sourceHash": SOURCE_HASH,
        "status": complete.get("status"),
        "failureCode": complete.get("failureCode"),
        "candidateCount": len(observed["candidates"]),
        "goldVerdict": report["verdict"],
        "goldFailures": report["failures"],
        "bedrockLiveExecutionEnabled": preflight.get(
            "liveExecutionEnabled"
        ),
        "bedrockCommittedCostUsdMicros": preflight.get(
            "existingCommittedCostUsdMicros"
        ),
        "observedArtifact": str(observed_path.relative_to(REPO_ROOT)),
        "evaluationArtifact": str(report_path.relative_to(REPO_ROOT)),
    }
    print(json.dumps(summary, indent=2))
    return 0 if report["passed"] else 1


def assert_zero_cost_runtime() -> None:
    response = requests.get(
        "http://127.0.0.1:5198/health/ready",
        timeout=10,
    )
    response.raise_for_status()
    payload = response.json()
    checks = payload.get("checks") or []
    if "deterministic-zero-cost" not in checks:
        raise RuntimeError(
            "The agent runtime is not in deterministic zero-cost mode."
        )


def validate_reprojection_start(import_view: dict[str, Any]) -> None:
    if import_view.get("id") != IMPORT_ID:
        raise RuntimeError("The DMS import identity did not match.")
    if import_view.get("sourceHash") != SOURCE_HASH:
        raise RuntimeError("The DMS physical source hash did not match.")
    if import_view.get("status") != "REVIEW_REQUIRED":
        raise RuntimeError("The DMS import is not ready for safe reprojection.")
    candidates = import_view.get("candidates") or []
    if any(item.get("status") != "REVIEW_REQUIRED" for item in candidates):
        raise RuntimeError(
            "A DMS candidate has already left review-required state."
        )


def to_observed(import_view: dict[str, Any]) -> dict[str, Any]:
    candidates = []
    for candidate in import_view.get("candidates") or []:
        values = (
            candidate.get("canonicalValues")
            or candidate.get("proposedValues")
            or candidate.get("values")
            or {}
        )
        candidates.append({
            "rowNumber": candidate.get("rowNumber"),
            "status": candidate.get("status"),
            "sourceLocator": candidate.get("sourceLocator"),
            "values": values,
            "evidence": candidate.get("evidence") or [],
        })
    candidates.sort(key=lambda item: int(item.get("rowNumber") or 0))
    return {
        "sourceHash": import_view.get("sourceHash"),
        "status": import_view.get("status"),
        "candidates": candidates,
    }


def validate_no_bedrock_cost(preflight: dict[str, Any]) -> None:
    if preflight.get("liveExecutionEnabled") is not False:
        raise RuntimeError("Live Bedrock execution became enabled unexpectedly.")
    if int(preflight.get("existingCommittedCostUsdMicros") or 0) != 0:
        raise RuntimeError("The zero-cost DMS repair recorded Bedrock spend.")


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    raise SystemExit(main())
