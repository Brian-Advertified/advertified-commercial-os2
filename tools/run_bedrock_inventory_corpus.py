"""Run governed Bedrock enrichment for the physically certified 43-file corpus.

All requests go through the Commercial API budget ledger. The command stops before
any call if the full conservative plan exceeds the US$4.75 new-work allowance.
"""

from __future__ import annotations

import json
import sys
import time
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
TENANT = "10000000-0000-0000-0000-000000000020"
NEW_RUN_CEILING = 4_750_000
PROGRAMME_CEILING = 5_000_000
HISTORICAL_RESERVE = 250_000
POLL_SECONDS = 3
MAX_WAIT_SECONDS = 3_600


def main() -> int:
    physical = read_json(CORPUS / "certification" / "physical-corpus-certification.json")
    baseline = read_json(CORPUS / "certification" / "pre-bedrock-candidate-baseline.json")
    if physical.get("passed") is not True or physical.get("summary", {}).get("passedSourceCount") != 43:
        raise RuntimeError("Bedrock corpus run requires physical certification 43/43.")
    if baseline.get("sourceCount") != 43:
        raise RuntimeError("The immutable pre-Bedrock baseline is incomplete.")

    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    validate_preflight(preflight)
    sources = sorted(
        preflight.get("sources") or [],
        key=lambda item: str(item.get("fileName") or "").lower(),
    )
    planned = int(preflight.get("newMaximumCostUsdMicros") or 0)
    existing = int(preflight.get("existingCommittedCostUsdMicros") or 0)
    progress_path = CORPUS / "certification" / "bedrock-corpus-progress.json"
    progress = {
        "schemaVersion": "advertified.inventory-bedrock-corpus-run.v1",
        "startedAtUtc": datetime.now(UTC).isoformat(),
        "physicalCertificationHash": physical.get("datasetVersion"),
        "baselineHash": baseline.get("baselineHash"),
        "programmeLimitUsdMicros": PROGRAMME_CEILING,
        "historicalReserveUsdMicros": HISTORICAL_RESERVE,
        "newRunCeilingUsdMicros": NEW_RUN_CEILING,
        "plannedMaximumCostUsdMicros": planned,
        "startingCommittedCostUsdMicros": existing,
        "sources": [],
    }
    write_json(progress_path, progress)

    for index, source in enumerate(sources, start=1):
        current_preflight = api.request(
            "GET", api.tenant_path("/inventory-semantic-preflight")
        )
        committed_before = int(
            current_preflight.get("existingCommittedCostUsdMicros") or 0
        )
        source_plan = next(
            item for item in current_preflight.get("sources") or []
            if item.get("sourceHash") == source.get("sourceHash")
        )
        maximum = int(source_plan.get("newMaximumCostUsdMicros") or source_plan.get("maximumCostUsdMicros") or 0)
        if committed_before + maximum > NEW_RUN_CEILING:
            raise RuntimeError(
                f"Budget fence stopped before {source['fileName']}: "
                f"{committed_before}+{maximum}>{NEW_RUN_CEILING}."
            )
        if source_plan.get("blocker") or not source_plan.get("safeToReproject"):
            raise RuntimeError(
                f"Source preflight blocked {source['fileName']}: "
                f"{source_plan.get('blocker')}."
            )

        import_id = str(source["importId"])
        current = api.read_import(import_id)
        if current.get("status") != "REVIEW_REQUIRED":
            raise RuntimeError(
                f"Import is not review-fenced before Bedrock: {source['fileName']}"
            )
        latest = latest_attempt(current)
        next_attempt = int(latest.get("attemptNumber") or 0) + 1 if latest else 1
        current = api.reproject_import(
            import_id,
            current["version"],
            str(source["sourceHash"]),
            next_attempt,
        )
        if current.get("status") != "REVIEW_REQUIRED":
            current = api.wait_for_extraction(
                import_id,
                POLL_SECONDS,
                MAX_WAIT_SECONDS,
                lambda _: None,
                lambda: None,
            )
        latest = latest_attempt(current)
        if (
            current.get("status") != "REVIEW_REQUIRED"
            or current.get("failureCode")
            or not latest
            or latest.get("status") != "COMPLETED"
            or not latest.get("extractedArtifactId")
        ):
            raise RuntimeError(
                f"Bedrock enrichment was not accepted for {source['fileName']}: "
                f"status={current.get('status')} failure={current.get('failureCode')} "
                f"attempt={latest}."
            )

        after = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
        committed_after = int(after.get("existingCommittedCostUsdMicros") or 0)
        if committed_after > NEW_RUN_CEILING:
            raise RuntimeError("The new-work Bedrock ceiling was exceeded.")
        if HISTORICAL_RESERVE + committed_after > PROGRAMME_CEILING:
            raise RuntimeError("The total US$5 Bedrock programme ceiling was exceeded.")
        progress["sources"].append({
            "position": index,
            "fileName": source["fileName"],
            "sourceHash": source["sourceHash"],
            "importId": import_id,
            "attemptNumber": latest.get("attemptNumber"),
            "attemptStatus": latest.get("status"),
            "committedCostBeforeUsdMicros": committed_before,
            "committedCostAfterUsdMicros": committed_after,
            "incrementalCommittedCostUsdMicros": committed_after - committed_before,
            "reason": "CLASSIFY_AND_DESCRIBE_PHYSICALLY_CERTIFIED_INVENTORY",
        })
        progress["lastUpdatedAtUtc"] = datetime.now(UTC).isoformat()
        progress["completedSourceCount"] = len(progress["sources"])
        progress["committedCostUsdMicros"] = committed_after
        write_json(progress_path, progress)

    progress["completedAtUtc"] = datetime.now(UTC).isoformat()
    progress["status"] = "COMPLETED"
    write_json(progress_path, progress)
    print(json.dumps({
        "status": progress["status"],
        "sourceCount": len(progress["sources"]),
        "committedCostUsdMicros": progress.get("committedCostUsdMicros", existing),
        "programmeLimitUsdMicros": PROGRAMME_CEILING,
        "historicalReserveUsdMicros": HISTORICAL_RESERVE,
        "progress": str(progress_path.relative_to(ROOT)),
    }, indent=2))
    return 0


def validate_preflight(preflight: dict[str, Any]) -> None:
    sources = preflight.get("sources") or []
    existing = int(preflight.get("existingCommittedCostUsdMicros") or 0)
    planned = int(preflight.get("newMaximumCostUsdMicros") or 0)
    worst = int(preflight.get("worstCaseTotalCostUsdMicros") or 0)
    blockers = preflight.get("blockers") or []
    if preflight.get("liveExecutionEnabled") is not True:
        raise RuntimeError("Live Bedrock execution is not enabled.")
    if len(sources) != 43:
        raise RuntimeError(f"Expected 43 preflight sources; found {len(sources)}.")
    if blockers:
        raise RuntimeError(f"Bedrock preflight blockers remain: {blockers}")
    if planned <= 0:
        raise RuntimeError("Bedrock preflight produced no paid work plan.")
    if existing + planned > NEW_RUN_CEILING or worst > NEW_RUN_CEILING:
        raise RuntimeError(
            f"Conservative Bedrock plan exceeds US$4.75: "
            f"existing={existing} planned={planned} worst={worst}."
        )
    if HISTORICAL_RESERVE + worst > PROGRAMME_CEILING:
        raise RuntimeError("Conservative plan exceeds the total US$5 programme limit.")


def latest_attempt(current: dict[str, Any]) -> dict[str, Any] | None:
    return max(
        current.get("extractionAttempts") or [],
        key=lambda item: int(item.get("attemptNumber") or 0),
        default=None,
    )


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, KeyError, json.JSONDecodeError) as error:
        print(json.dumps({"status": "FAILED", "error": str(error)}), file=sys.stderr)
        raise SystemExit(2)
