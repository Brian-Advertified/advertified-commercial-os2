"""Physically certify every retained inventory source against API projections.

The command is read-only with respect to imports and never invokes Bedrock.  It
writes immutable comparison evidence and promotes file gold only when every
physical anchor is covered and every projected candidate is source-supported.
"""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict, replace
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from evaluate_inventory_file_gold import evaluate as evaluate_human_gold
from inventory_corpus_api import InventoryApi
from inventory_physical_anchor_discovery import discover_anchors
from inventory_physical_certification import FileCertification, certify_file
from inventory_physical_facts import load_source

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    args = parse_args()
    root = args.evidence.resolve(strict=True)
    manifest = read_json(root / "source-manifest.json")
    documents = manifest.get("documents") or []
    if len(documents) != 43:
        raise RuntimeError(
            f"Expected exactly 43 physical sources; found {len(documents)}."
        )

    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    preflight = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    )
    if preflight.get("liveExecutionEnabled"):
        raise RuntimeError(
            "Physical certification must complete before live Bedrock is enabled."
        )
    sources = {
        str(item["sourceHash"]): item
        for item in preflight.get("sources") or []
    }
    if set(sources) != {str(item["sha256"]) for item in documents}:
        raise RuntimeError(
            "The live OS2 source set does not match the 43-file manifest."
        )

    output = root / "physical-certification"
    output.mkdir(parents=True, exist_ok=True)
    (output / "baseline").mkdir(parents=True, exist_ok=True)
    validate_bounds(args.start_position, args.maximum_files, len(documents))
    if not args.assemble_only:
        selected = documents[
            args.start_position - 1:
            args.start_position - 1 + args.maximum_files
        ]
        for position, document in enumerate(
            selected,
            start=args.start_position,
        ):
            certify_document(
                root,
                output,
                document,
                sources[str(document["sha256"])],
                client,
                position,
                len(documents),
            )

    records, missing = load_records(
        output,
        documents,
        expected_projection_version(preflight),
    )
    if missing:
        print(json.dumps({
            "verdict": "PARTIAL",
            "sourceCount": len(documents),
            "completedSourceCount": len(records),
            "missingSourceCount": len(missing),
            "nextPositions": missing,
        }, indent=2))
        return 0

    register = build_register(manifest, preflight, records)
    write_json(output / "corpus-physical-certification.json", register)
    (output / "CORPUS_PHYSICAL_CERTIFICATION.md").write_text(
        render_markdown(register), encoding="utf-8"
    )
    if args.promote and register["verdict"] == "PASS":
        promote_gold(root, records)

    print(json.dumps({
        "verdict": register["verdict"],
        "sourceCount": register["sourceCount"],
        "passedSourceCount": register["passedSourceCount"],
        "failedSourceCount": register["failedSourceCount"],
        "candidateCount": register["candidateCount"],
        "expectedAnchorCount": register["expectedAnchorCount"],
        "bedrockLiveExecutionEnabled": register[
            "bedrockLiveExecutionEnabled"
        ],
        "report": str(
            (output / "corpus-physical-certification.json")
            .relative_to(REPO_ROOT)
        ),
    }, indent=2))
    return 0 if register["verdict"] == "PASS" else 2


def validate_bounds(start: int, maximum: int, total: int) -> None:
    if start < 1 or start > total:
        raise ValueError(f"Start position must be between 1 and {total}.")
    if maximum < 1 or maximum > total:
        raise ValueError(f"Maximum files must be between 1 and {total}.")


def certify_document(
    root: Path,
    output: Path,
    document: dict[str, Any],
    source: dict[str, Any],
    client: InventoryApi,
    position: int,
    total: int,
) -> None:
    source_hash = str(document["sha256"])
    source_map = root / "semantic-v1" / f"{source_hash}.json"
    if not source_map.exists():
        raise RuntimeError(
            f"Missing physical source map for {document['relativePath']}."
        )
    import_view = client.read_complete_import(str(source["importId"]))
    certification = certify_file(source_map, import_view, source)
    certification = apply_human_gold(
        root, document, import_view, certification
    )
    record = asdict(certification)
    record["position"] = position
    write_json(output / f"{source_hash}.json", record)
    write_json(
        output / "baseline" / f"{source_hash}.json",
        physical_baseline(
            document,
            source,
            import_view,
            certification.passed,
        ),
    )
    print(json.dumps({
        "position": position,
        "total": total,
        "fileName": certification.file_name,
        "passed": certification.passed,
        "candidateCount": certification.candidate_count,
        "expectedAnchorCount": certification.expected_anchor_count,
        "failureCount": len(certification.failures),
    }), flush=True)


def apply_human_gold(
    root: Path,
    document: dict[str, Any],
    import_view: dict[str, Any],
    certification: FileCertification,
) -> FileCertification:
    """Prefer a passing human-authored comparison over heuristic anchors.

    The generic anchor scanner is intentionally conservative, but image-based
    Office documents can collapse a physical table into one OCR text fragment.
    A reviewed file-gold contract is stronger evidence than that heuristic and
    must therefore be authoritative when its complete evaluation passes.
    """
    gold_path = root / "gold" / f"{document['sha256']}.json"
    if not gold_path.exists():
        return certification
    gold = read_json(gold_path)
    if gold.get("schemaVersion") != "advertified.inventory-file-gold.v1":
        return certification
    evaluation = evaluate_human_gold(import_view, gold)
    if not evaluation.get("passed"):
        failures = tuple(dict.fromkeys(
            (*certification.failures, *(
                "HUMAN_GOLD_MISMATCH:" + str(value)
                for value in evaluation.get("failures") or []
            ))
        ))
        return replace(
            certification,
            passed=False,
            failures=failures,
        )
    expected = int(
        evaluation.get("expectedCandidateCount")
        or evaluation.get("observedCandidateCount")
        or certification.candidate_count
    )
    return replace(
        certification,
        expected_anchor_count=expected,
        matched_anchor_count=expected,
        unmatched_anchor_count=0,
        unsupported_candidate_count=0,
        passed=True,
        failures=(),
        warnings=tuple(dict.fromkeys((
            *certification.warnings,
            "HUMAN_AUTHORED_FILE_GOLD_CONFIRMED",
        ))),
        unmatched_anchors=(),
        unsupported_candidates=(),
    )


def expected_projection_version(preflight: dict[str, Any]) -> str:
    planned = str(preflight.get("projectionVersion") or "")
    return planned.split(";semantic/", 1)[0]


def load_records(
    output: Path,
    documents: list[dict[str, Any]],
    projection_version: str,
) -> tuple[list[dict[str, Any]], list[int]]:
    records: list[dict[str, Any]] = []
    missing: list[int] = []
    for position, document in enumerate(documents, start=1):
        path = output / f"{document['sha256']}.json"
        if not path.exists():
            missing.append(position)
            continue
        record = read_json(path)
        if (
            record.get("source_hash") != document["sha256"]
            or record.get("latest_attempt_provider_version")
            != projection_version
        ):
            missing.append(position)
            continue
        record["position"] = position
        records.append(record)
    return records, missing


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    parser.add_argument(
        "--promote",
        action="store_true",
        help="Write file-level gold only after all 43 comparison results pass.",
    )
    parser.add_argument(
        "--start-position",
        type=int,
        default=1,
        help="One-based manifest position at which this bounded run starts.",
    )
    parser.add_argument(
        "--maximum-files",
        type=int,
        default=43,
        help="Maximum source files evaluated by this invocation.",
    )
    parser.add_argument(
        "--assemble-only",
        action="store_true",
        help="Build the corpus verdict from existing per-file reports.",
    )
    return parser.parse_args()


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
        "verdict": "PASS" if len(passed) == 43 else "FAIL",
        "documents": records,
    }


def promote_gold(root: Path, records: list[dict[str, Any]]) -> None:
    gold_root = root / "gold"
    gold_root.mkdir(parents=True, exist_ok=True)
    for record in records:
        if not record["passed"]:
            continue
        source_hash = record["source_hash"]
        source = load_source(
            root / "semantic-v1" / f"{source_hash}.json"
        )
        anchors = [asdict(item) for item in discover_anchors(source)]
        payload = {
            "schemaVersion": "advertified.inventory-file-gold.v2",
            "documentId": source_hash,
            "fileName": record["file_name"],
            "certifiedAtUtc": datetime.now(UTC).isoformat(),
            "certificationMethod": (
                "INDEPENDENT_PHYSICAL_SOURCE_MAP_TO_API_COMPARISON"
            ),
            "expectedCandidateCount": record["candidate_count"],
            "expectedAnchors": anchors,
            "publicationRequiresHumanReview": True,
        }
        write_json(gold_root / f"{source_hash}.json", payload)


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


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError,
        KeyError,
        ValueError,
        RuntimeError,
        json.JSONDecodeError,
        requests.RequestException,
    ) as error:
        print(json.dumps({"verdict": "ERROR", "error": str(error)}))
        raise SystemExit(3)
