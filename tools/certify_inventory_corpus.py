"""Certify every retained inventory file against immutable physical evidence.

This command is read-only with respect to API/DB state. It makes no Bedrock
request and writes a per-file audit plus an aggregate production register.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_corpus_api import InventoryApi
from inventory_corpus_certification import certify_file, write_certification

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    args = parse_args()
    root = args.evidence.resolve(strict=True)
    manifest = read_json(root / "source-manifest.json")
    documents = manifest.get("documents") or []
    if len(documents) != 43:
        raise RuntimeError(f"Expected 43 physical sources; found {len(documents)}.")

    api = InventoryApi(args.api, args.origin, args.tenant)
    api.start_session()
    preflight = api.request(
        "GET", api.tenant_path("/inventory-semantic-preflight")
    )
    source_by_hash = {
        str(source["sourceHash"]): source
        for source in preflight.get("sources") or []
    }
    current_provider = deployed_provider_version(preflight)
    output = root / "physical-certification"
    output.mkdir(parents=True, exist_ok=True)

    results = []
    for index, document in enumerate(documents, start=1):
        source_hash = str(document["sha256"])
        source = source_by_hash.get(source_hash)
        if source is None:
            raise RuntimeError(
                f"No retained import found for {document['relativePath']}."
            )
        import_view = api.read_complete_import(str(source["importId"]))
        result = certify_file(
            root / "semantic-v1" / f"{source_hash}.json",
            document,
            import_view,
            current_provider,
            human_gold=load_human_gold(root, source_hash),
        )
        write_certification(output / f"{source_hash}.json", result)
        results.append(result)
        print(json.dumps({
            "position": index,
            "total": len(documents),
            "file": result.file_name,
            "verdict": result.verdict,
            "physicalUnits": result.physical_unit_count,
            "candidates": result.candidate_count,
            "blockers": result.blockers,
        }), flush=True)

    register = aggregate(
        manifest,
        preflight,
        current_provider,
        results,
    )
    (output / "corpus-physical-certification.json").write_text(
        json.dumps(register, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (output / "CORPUS_PHYSICAL_CERTIFICATION.md").write_text(
        render_markdown(register),
        encoding="utf-8",
    )
    for marker in output.glob("STATUS_*.marker"):
        marker.unlink()
    marker_name = (
        "STATUS_PASS_43_OF_43.marker"
        if register["verdict"] == "PASS"
        else f"STATUS_FAIL_{register['summary']['failed']}_OF_43.marker"
    )
    marker_payload = json.dumps({
        "verdict": register["verdict"],
        "passed": register["summary"]["passed"],
        "failed": register["summary"]["failed"],
        "blockers": register.get("blockersByFileCount", {}),
    }, indent=2) + "\n"
    (output / marker_name).write_text(
        marker_payload,
        encoding="utf-8",
    )
    (output / "STATUS_CURRENT.marker").write_text(
        marker_payload,
        encoding="utf-8",
    )
    print(json.dumps({
        "verdict": register["verdict"],
        "sourceCount": register["summary"]["sourceCount"],
        "passed": register["summary"]["passed"],
        "failed": register["summary"]["failed"],
        "bedrockLiveExecutionEnabled": register["bedrock"]["liveExecutionEnabled"],
        "bedrockCommittedCostUsdMicros": register["bedrock"]["committedCostUsdMicros"],
        "register": str(
            (output / "corpus-physical-certification.json")
            .relative_to(REPO_ROOT)
        ),
    }, indent=2))
    return 0 if register["verdict"] == "PASS" else 2


def load_human_gold(
    root: Path,
    source_hash: str,
) -> dict[str, Any] | None:
    gold_path = root / "gold" / f"{source_hash}.json"
    if not gold_path.exists():
        return None
    evaluation_paths = (
        root / "certification" / f"{source_hash}.local-evaluation.json",
        root / "certification" / f"{source_hash}.evaluation.json",
    )
    evaluation = next(
        (read_json(path) for path in evaluation_paths if path.exists()),
        None,
    )
    if not evaluation:
        return None
    if (
        not bool(evaluation.get("passed"))
        or str(evaluation.get("documentId") or "") != source_hash
    ):
        return None
    gold = read_json(gold_path)
    return (
        gold
        if str(gold.get("documentId") or "") == source_hash
        else None
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--api", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant", default=TENANT_ID)
    return parser.parse_args()


def deployed_provider_version(preflight: dict[str, Any]) -> str:
    value = str(preflight.get("projectionVersion") or "")
    provider = value.split(";semantic/", 1)[0]
    if not provider:
        raise RuntimeError("The deployed physical projection version is missing.")
    return provider


def aggregate(
    manifest: dict[str, Any],
    preflight: dict[str, Any],
    provider_version: str,
    results: list[Any],
) -> dict[str, Any]:
    passed = [result for result in results if result.verdict == "PASS"]
    failed = [result for result in results if result.verdict != "PASS"]
    blockers: dict[str, int] = {}
    for result in failed:
        for blocker in result.blockers:
            blockers[blocker] = blockers.get(blocker, 0) + 1
    return {
        "schemaVersion": "advertified.inventory-physical-certification.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "providerVersion": provider_version,
        "verdict": "PASS" if len(passed) == 43 else "FAIL",
        "summary": {
            "sourceCount": len(results),
            "passed": len(passed),
            "failed": len(failed),
            "physicalUnitCount": sum(
                result.physical_unit_count for result in results
            ),
            "candidateCount": sum(
                result.candidate_count for result in results
            ),
            "matchedPhysicalUnitCount": sum(
                result.matched_physical_unit_count for result in results
            ),
            "unmatchedPhysicalUnitCount": sum(
                len(result.unmatched_physical_units) for result in results
            ),
            "unsupportedCandidateCount": sum(
                result.unsupported_candidate_count for result in results
            ),
            "duplicateCandidateCount": sum(
                result.duplicate_candidate_count for result in results
            ),
            "blockingCandidateCount": sum(
                result.blocking_candidate_count for result in results
            ),
            "publishedCandidateCount": sum(
                result.published_candidate_count for result in results
            ),
        },
        "blockersByFileCount": dict(sorted(blockers.items())),
        "bedrock": {
            "liveExecutionEnabled": bool(
                preflight.get("liveExecutionEnabled")
            ),
            "committedCostUsdMicros": int(
                preflight.get("existingCommittedCostUsdMicros") or 0
            ),
            "maximumProjectedCostUsdMicros": int(
                preflight.get("newMaximumCostUsdMicros") or 0
            ),
        },
        "files": [
            {
                "sourceHash": result.source_hash,
                "fileName": result.file_name,
                "format": result.document_format,
                "verdict": result.verdict,
                "physicalUnits": result.physical_unit_count,
                "candidates": result.candidate_count,
                "matchedPhysicalUnits": result.matched_physical_unit_count,
                "unmatchedPhysicalUnits": len(result.unmatched_physical_units),
                "unsupportedCandidates": result.unsupported_candidate_count,
                "duplicates": result.duplicate_candidate_count,
                "blockingCandidates": result.blocking_candidate_count,
                "blockers": list(result.blockers),
                "warnings": list(result.warnings),
            }
            for result in sorted(results, key=lambda item: item.file_name.lower())
        ],
    }


def render_markdown(register: dict[str, Any]) -> str:
    summary = register["summary"]
    lines = [
        "# Inventory corpus physical certification",
        "",
        f"Generated: `{register['generatedAtUtc']}`",
        "",
        f"**Verdict: {register['verdict']}**",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Source files | {summary['sourceCount']} |",
        f"| Passed | {summary['passed']} |",
        f"| Failed | {summary['failed']} |",
        f"| Physical sellable units | {summary['physicalUnitCount']} |",
        f"| API candidates | {summary['candidateCount']} |",
        f"| Matched physical units | {summary['matchedPhysicalUnitCount']} |",
        f"| Unmatched physical units | {summary['unmatchedPhysicalUnitCount']} |",
        f"| Unsupported candidates | {summary['unsupportedCandidateCount']} |",
        f"| Duplicate candidates | {summary['duplicateCandidateCount']} |",
        f"| Blocking candidates | {summary['blockingCandidateCount']} |",
        f"| Published candidates | {summary['publishedCandidateCount']} |",
        "",
        "## File results",
        "",
        "| File | Format | Physical | Candidates | Matched | Verdict | Blockers |",
        "|---|---|---:|---:|---:|---|---|",
    ]
    for item in register["files"]:
        blockers = ", ".join(item["blockers"]) or "None"
        lines.append(
            f"| {escape(item['fileName'])} | {item['format']} | "
            f"{item['physicalUnits']} | {item['candidates']} | "
            f"{item['matchedPhysicalUnits']} | {item['verdict']} | "
            f"{escape(blockers)} |"
        )
    lines.extend([
        "",
        "Certification passes only when all 43 immutable physical sources match "
        "the current candidate projection with no unsupported, duplicate, stale, "
        "approved or published records. Bedrock is evaluated only after this gate.",
        "",
    ])
    return "\n".join(lines)


def escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError,
        ValueError,
        KeyError,
        RuntimeError,
        json.JSONDecodeError,
        requests.RequestException,
    ) as error:
        print(json.dumps({"verdict": "ERROR", "error": str(error)}), file=sys.stderr)
        raise SystemExit(3)
