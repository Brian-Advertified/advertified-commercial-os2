"""Prepare and process the confidential inventory corpus without copying source files."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
import time
from collections import Counter, defaultdict
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Callable
from uuid import uuid4

import requests
try:
    from tools.inventory_corpus_api import InventoryApi
except ModuleNotFoundError:
    from inventory_corpus_api import InventoryApi

GOVERNED_DOCUMENT_COUNT = 43
HOLDOUT_RATIO = 0.20
SUPPORTED_MEDIA_TYPES = {
    ".pdf": "application/pdf",
    ".pptx": "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    ".xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
}
REVIEW_REQUIRED = "REVIEW_REQUIRED"
ACTIVE_ATTEMPT_STATES = {"PENDING", "SUBMITTING", "RUNNING", "FAILED_RETRYABLE"}
RETRYABLE_TERMINAL_STATES = {"FAILED_TERMINAL", "TIMED_OUT", "CANCELLED"}
RECONCILIATION_REQUIRED = "RECONCILIATION_REQUIRED"


def main() -> int:
    args = parse_args()
    source_root = args.source.resolve(strict=True)
    evidence_root = args.evidence.resolve()
    ensure_separate_roots(source_root, evidence_root)
    manifest_path = evidence_root / "source-manifest.json"
    evidence_root.mkdir(parents=True, exist_ok=True)
    cached_manifest = manifest_path.exists()
    manifest = load_or_create_manifest(source_root, manifest_path)
    print_summary(manifest, cached=cached_manifest)
    if not args.process:
        return 0
    if args.expected_paid_ai_cost_usd != 0:
        raise ValueError("This local corpus run requires an expected paid-AI cost of USD 0.00.")
    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    process_documents(
        manifest, manifest_path, source_root, evidence_root,
        args.partition, args.document, args.supplier_name, client,
        args.poll_seconds, args.max_wait_seconds, args.queue_only,
    )
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--process", action="store_true")
    parser.add_argument("--partition", choices=("train", "holdout", "all"), default="train")
    parser.add_argument("--document", action="append", default=[])
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5097")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default="10000000-0000-0000-0000-000000000020")
    parser.add_argument("--supplier-name", default="Not supplied")
    parser.add_argument("--expected-paid-ai-cost-usd", type=float, default=0.0)
    parser.add_argument("--poll-seconds", type=int, default=5)
    parser.add_argument("--max-wait-seconds", type=int, default=4_200)
    parser.add_argument("--queue-only", action="store_true")
    return parser.parse_args()


def ensure_separate_roots(source_root: Path, evidence_root: Path) -> None:
    if not source_root.is_dir():
        raise ValueError("The inventory source must be a directory.")
    if evidence_root == source_root or source_root in evidence_root.parents:
        raise ValueError("Processing evidence must not be written inside the source corpus.")


def load_or_create_manifest(source_root: Path, manifest_path: Path) -> dict[str, Any]:
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        verify_unchanged(source_root, manifest)
        return manifest
    documents = inspect_sources(source_root)
    if len(documents) != GOVERNED_DOCUMENT_COUNT:
        raise ValueError(f"Expected exactly {GOVERNED_DOCUMENT_COUNT} source files.")
    assign_partitions(documents)
    corpus_hash = hash_text("\n".join(item["sha256"] for item in documents))
    manifest = {
        "schemaVersion": "inventory-corpus-manifest-v1",
        "datasetVersion": f"inventory-corpus-2026-09-03-{corpus_hash[:12]}",
        "createdAtUtc": now_utc(),
        "sourceRoot": str(source_root),
        "expectedPaidAiCostUsd": 0.0,
        "paidAiProviderCallsAuthorized": False,
        "documentCount": len(documents),
        "totalBytes": sum(item["bytes"] for item in documents),
        "documents": documents,
    }
    write_json(manifest_path, manifest)
    return manifest


def inspect_sources(source_root: Path) -> list[dict[str, Any]]:
    paths = sorted(path for path in source_root.rglob("*") if path.is_file())
    documents: list[dict[str, Any]] = []
    for path in paths:
        if path.is_symlink() or source_root not in path.resolve().parents:
            raise ValueError("Linked or escaping source files are not accepted.")
        extension = path.suffix.lower()
        if extension not in SUPPORTED_MEDIA_TYPES:
            raise ValueError(f"Unsupported source type: {extension or '[none]'}")
        stat = path.stat()
        digest = hash_file(path)
        documents.append({
            "id": digest,
            "relativePath": path.relative_to(source_root).as_posix(),
            "extension": extension,
            "mediaType": SUPPORTED_MEDIA_TYPES[extension],
            "bytes": stat.st_size,
            "modifiedTimeNs": stat.st_mtime_ns,
            "sha256": digest,
            "partition": None,
            "processing": {"state": "pending"},
        })
    return documents


def assign_partitions(documents: list[dict[str, Any]]) -> None:
    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for document in documents:
        groups[document["extension"]].append(document)
    target = math.ceil(len(documents) * HOLDOUT_RATIO)
    quotas = {key: max(1, math.ceil(len(items) * HOLDOUT_RATIO)) for key, items in groups.items()}
    while sum(quotas.values()) > target:
        candidates = [key for key, quota in quotas.items() if quota > 1]
        key = max(candidates, key=lambda item: (len(groups[item]), item))
        quotas[key] -= 1
    selected: set[str] = set()
    for extension, items in groups.items():
        selected.update(item["id"] for item in sorted(items, key=lambda item: item["sha256"])
                        [:quotas[extension]])
    for document in documents:
        document["partition"] = "holdout" if document["id"] in selected else "train"


def verify_unchanged(source_root: Path, manifest: dict[str, Any]) -> None:
    if manifest.get("documentCount") != GOVERNED_DOCUMENT_COUNT:
        raise ValueError("The recorded corpus does not match the governed document count.")
    recorded = {item["relativePath"]: item for item in manifest["documents"]}
    current = sorted(path for path in source_root.rglob("*") if path.is_file())
    if len(current) != GOVERNED_DOCUMENT_COUNT:
        raise ValueError("The source corpus file count changed after manifest creation.")
    for path in current:
        relative = path.relative_to(source_root).as_posix()
        item = recorded.get(relative)
        stat = path.stat()
        if item is None or stat.st_size != item["bytes"] or stat.st_mtime_ns != item["modifiedTimeNs"]:
            raise ValueError(f"The immutable source corpus changed: {relative}")


def process_documents(
    manifest: dict[str, Any], manifest_path: Path, source_root: Path,
    evidence_root: Path, partition: str, requested: list[str], supplier_name: str,
    client: "InventoryApi", poll_seconds: int, max_wait_seconds: int, queue_only: bool,
) -> None:
    if poll_seconds < 1 or max_wait_seconds <= poll_seconds:
        raise ValueError("Corpus polling bounds are invalid.")
    selected = [item for item in manifest["documents"]
                if partition == "all" or item["partition"] == partition]
    if requested:
        names = set(requested)
        selected = [item for item in selected if item["relativePath"] in names]
        missing = names.difference(item["relativePath"] for item in selected)
        if missing:
            raise ValueError(f"Requested corpus documents were not found: {sorted(missing)}")
    failures = 0
    for index, document in enumerate(selected, start=1):
        state = document["processing"].get("state")
        if state == "review-required" and artifact_is_preserved(document, evidence_root):
            print(f"[{index}/{len(selected)}] cached {document['id'][:12]}", flush=True)
            continue
        try:
            outcome = process_document(
                document, source_root, evidence_root, supplier_name, client,
                lambda: write_json(manifest_path, manifest), poll_seconds, max_wait_seconds,
                queue_only,
            )
        except Exception as error:
            failures += 1
            document["processing"].update(
                state="failed", failedAtUtc=now_utc(), errorType=type(error).__name__)
            write_json(manifest_path, manifest)
            print(f"[{index}/{len(selected)}] failed {document['id'][:12]}: {type(error).__name__}",
                  flush=True)
            continue
        write_json(manifest_path, manifest)
        print(f"[{index}/{len(selected)}] {outcome} {document['id'][:12]}", flush=True)
    if failures:
        raise RuntimeError(f"{failures} corpus documents did not reach human review.")


def process_document(
    document: dict[str, Any], source_root: Path, evidence_root: Path,
    supplier_name: str, client: "InventoryApi", checkpoint: Callable[[], None],
    poll_seconds: int, max_wait_seconds: int, queue_only: bool,
) -> str:
    processing = document["processing"]
    import_id = processing.get("importId")
    if not import_id:
        source_path = source_root / document["relativePath"]
        created = client.create_import(source_path, document, supplier_name)
        if created.get("sourceHash") != document["sha256"]:
            raise ValueError("The API source hash differs from the verified corpus hash.")
        import_id = created["id"]
        processing.update(state="uploaded", importId=import_id, version=created["version"])
        checkpoint()
    current = client.read_import(import_id)
    executed = begin_or_resume_extraction(document, current, client)
    if queue_only and executed.get("status") != REVIEW_REQUIRED:
        latest = (executed.get("extractionAttempts") or [None])[0]
        processing.update(
            state="extracting", queuedAtUtc=now_utc(), version=executed["version"],
            attempt={"importStatus": executed["status"], "attempt": latest},
        )
        checkpoint()
        return "queued"
    if executed.get("status") != REVIEW_REQUIRED:
        executed = client.wait_for_extraction(
            import_id, poll_seconds, max_wait_seconds,
            lambda state: processing.update(state="extracting", attempt=state),
            checkpoint,
        )
    if executed.get("status") != REVIEW_REQUIRED:
        raise ValueError("Production extraction did not stop at human review.")
    preserve_observed_artifact(document, evidence_root, executed, client)
    return "review-required"


def preserve_observed_artifact(
    document: dict[str, Any], evidence_root: Path,
    current: dict[str, Any], client: "InventoryApi",
) -> None:
    import_id = document["processing"]["importId"]
    artifact = client.read_complete_import(import_id)
    artifact_path = evidence_root / "observed" / f"{document['sha256']}.json"
    write_json(artifact_path, artifact)
    document["processing"].update(
        state="review-required", processedAtUtc=now_utc(), apiStatus=current["status"],
        version=current["version"],
        artifactPath=artifact_path.relative_to(evidence_root).as_posix(),
        artifactSha256=hash_file(artifact_path), candidateCounts=current["candidateCounts"],
    )


def begin_or_resume_extraction(
    document: dict[str, Any], current: dict[str, Any], client: "InventoryApi",
) -> dict[str, Any]:
    if current.get("status") == REVIEW_REQUIRED:
        return current
    attempts = current.get("extractionAttempts") or []
    latest = attempts[0] if attempts else None
    if latest and latest["status"] == RECONCILIATION_REQUIRED:
        raise RuntimeError("Extraction requires explicit operator reconciliation.")
    if latest and latest["status"] in RETRYABLE_TERMINAL_STATES:
        return client.retry_import(
            current["id"], current["version"], latest["attemptNumber"] + 1,
            document["sha256"],
        )
    if latest and latest["status"] in ACTIVE_ATTEMPT_STATES:
        return current
    if current.get("status") == "UPLOADED" and not latest:
        return client.execute_import(current["id"], current["version"])
    raise RuntimeError("Inventory import is not in a resumable extraction state.")


def artifact_is_preserved(document: dict[str, Any], evidence_root: Path) -> bool:
    processing = document["processing"]
    relative = processing.get("artifactPath")
    expected = processing.get("artifactSha256")
    if not relative or not expected:
        return False
    path = evidence_root / relative
    return path.is_file() and hash_file(path) == expected


def print_summary(manifest: dict[str, Any], cached: bool) -> None:
    types = Counter(item["extension"] for item in manifest["documents"])
    partitions = Counter(item["partition"] for item in manifest["documents"])
    print(json.dumps({
        "cachedManifest": cached,
        "datasetVersion": manifest["datasetVersion"],
        "documentCount": manifest["documentCount"],
        "totalBytes": manifest["totalBytes"],
        "types": dict(sorted(types.items())),
        "partitions": dict(sorted(partitions.items())),
        "expectedPaidAiCostUsd": manifest["expectedPaidAiCostUsd"],
    }, sort_keys=True), flush=True)


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def hash_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid4().hex}.tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    for attempt in range(5):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:
            if attempt == 4:
                raise
            time.sleep(0.05 * (2 ** attempt))


def now_utc() -> str:
    return datetime.now(UTC).isoformat()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (
        OSError, ValueError, KeyError, RuntimeError,
        json.JSONDecodeError, requests.RequestException,
    ) as error:
        print(json.dumps({"passed": False, "error": str(error)}), file=sys.stderr)
        sys.exit(2)
