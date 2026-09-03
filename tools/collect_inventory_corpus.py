"""Collect completed durable corpus attempts without serial queue blocking."""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from typing import Any

import requests

try:
    from tools.inventory_corpus_api import ACTIVE_ATTEMPT_STATES, InventoryApi
    from tools.process_inventory_corpus import (
        REVIEW_REQUIRED,
        artifact_is_preserved,
        ensure_separate_roots,
        load_or_create_manifest,
        now_utc,
        preserve_observed_artifact,
        write_json,
    )
except ModuleNotFoundError:
    from inventory_corpus_api import ACTIVE_ATTEMPT_STATES, InventoryApi
    from process_inventory_corpus import (
        REVIEW_REQUIRED,
        artifact_is_preserved,
        ensure_separate_roots,
        load_or_create_manifest,
        now_utc,
        preserve_observed_artifact,
        write_json,
    )


def main() -> int:
    args = parse_args()
    source_root = args.source.resolve(strict=True)
    evidence_root = args.evidence.resolve()
    ensure_separate_roots(source_root, evidence_root)
    manifest_path = evidence_root / "source-manifest.json"
    manifest = load_or_create_manifest(source_root, manifest_path)
    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    return collect(
        manifest, manifest_path, evidence_root, client,
        args.poll_seconds, args.max_wait_seconds,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5097")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default="10000000-0000-0000-0000-000000000020")
    parser.add_argument("--poll-seconds", type=int, default=30)
    parser.add_argument("--max-wait-seconds", type=int, default=36_000)
    return parser.parse_args()


def collect(
    manifest: dict[str, Any], manifest_path: Path, evidence_root: Path,
    client: InventoryApi, poll_seconds: int, max_wait_seconds: int,
) -> int:
    if poll_seconds < 5 or max_wait_seconds <= poll_seconds:
        raise ValueError("Corpus collection bounds are invalid.")
    remaining = {
        item["id"]: item for item in manifest["documents"]
        if not artifact_is_preserved(item, evidence_root)
    }
    missing = [item["relativePath"] for item in remaining.values()
               if not item["processing"].get("importId")]
    if missing:
        raise RuntimeError(f"Corpus entries were not queued: {missing}")
    markers: dict[str, tuple[str, str | None]] = {}
    failures: dict[str, str] = {}
    deadline = time.monotonic() + max_wait_seconds
    while remaining and time.monotonic() < deadline:
        for document_id, document in list(remaining.items()):
            current = client.read_import(document["processing"]["importId"])
            attempts = current.get("extractionAttempts") or []
            latest = attempts[0] if attempts else None
            marker = (current["status"], latest["status"] if latest else None)
            if markers.get(document_id) != marker:
                markers[document_id] = marker
                print(json.dumps({"sourceHash": document_id, "state": marker}), flush=True)
            document["processing"].update(
                state="extracting", attempt={"importStatus": marker[0], "attempt": latest})
            if current["status"] == REVIEW_REQUIRED:
                preserve_observed_artifact(document, evidence_root, current, client)
                remaining.pop(document_id)
                print(json.dumps({"sourceHash": document_id, "collected": True}), flush=True)
            elif not latest or latest["status"] not in ACTIVE_ATTEMPT_STATES:
                failures[document_id] = latest["status"] if latest else "NO_ATTEMPT"
                document["processing"].update(
                    state="failed", failedAtUtc=now_utc(),
                    errorType="TerminalExtractionState")
                remaining.pop(document_id)
            write_json(manifest_path, manifest)
        if remaining:
            time.sleep(poll_seconds)
    if remaining:
        raise RuntimeError(
            f"Collection observation expired with {len(remaining)} active documents.")
    if failures:
        print(json.dumps({"passed": False, "terminalFailures": failures}), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (
        OSError, ValueError, KeyError, RuntimeError,
        json.JSONDecodeError, requests.RequestException,
    ) as error:
        print(json.dumps({"passed": False, "error": str(error)}), file=sys.stderr)
        sys.exit(2)
