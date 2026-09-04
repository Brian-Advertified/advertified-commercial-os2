"""Reproject retained corpus artifacts in a bounded, resumable local batch."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

import requests

try:
    from tools.inventory_corpus_api import (
        ACTIVE_ATTEMPT_STATES,
        REVIEW_REQUIRED,
        InventoryApi,
    )
    from tools.process_inventory_corpus import (
        preserve_observed_artifact,
        write_json,
    )
except ModuleNotFoundError:
    from inventory_corpus_api import (
        ACTIVE_ATTEMPT_STATES,
        REVIEW_REQUIRED,
        InventoryApi,
    )
    from process_inventory_corpus import (
        preserve_observed_artifact,
        write_json,
    )

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_EVIDENCE = REPO_ROOT / "artifacts" / "inventory-corpus"
RETAINED_PROVIDER = "retained-docling-projection"
CALIBRATION_DOCUMENTS = (
    "DMS Digital Rate Card .xlsx",
    "Reveel - ZA - Publisher Media Kit.pptx",
    "SABC May 2026 TV Rates (1).pdf",
    "Algoa FM - Algoa Club Package - Plan A - Generic & Sponsorship -2026.pdf",
)


def main() -> int:
    args = parse_args()
    evidence_root = args.evidence.resolve(strict=True)
    manifest_path = evidence_root / "source-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    selected = select_documents(
        manifest, args.document, args.all, args.maximum
    )
    client = InventoryApi(
        args.api_base_url, args.origin, args.tenant_id
    )
    client.start_session()
    for index, document in enumerate(selected, start=1):
        state = reproject_document(
            document, evidence_root, client,
            args.poll_seconds, args.max_wait_seconds,
        )
        write_json(manifest_path, manifest)
        print(json.dumps({
            "position": index,
            "total": len(selected),
            "source": document["relativePath"],
            "state": state,
        }), flush=True)
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--evidence", type=Path, default=DEFAULT_EVIDENCE
    )
    parser.add_argument("--document", action="append", default=[])
    parser.add_argument(
        "--all", action="store_true",
        help="Explicitly select the complete governed corpus.",
    )
    parser.add_argument(
        "--maximum", type=int, default=4,
        help="Maximum documents in this bounded run.",
    )
    parser.add_argument(
        "--api-base-url", default="http://127.0.0.1:5197"
    )
    parser.add_argument(
        "--origin", default="http://localhost:3017"
    )
    parser.add_argument(
        "--tenant-id",
        default="10000000-0000-0000-0000-000000000020",
    )
    parser.add_argument("--poll-seconds", type=int, default=2)
    parser.add_argument("--max-wait-seconds", type=int, default=900)
    return parser.parse_args()


def select_documents(
    manifest: dict[str, Any],
    requested: list[str],
    select_all: bool,
    maximum: int,
) -> list[dict[str, Any]]:
    if requested and select_all:
        raise ValueError(
            "--document and --all are mutually exclusive."
        )
    if maximum not in range(1, 44):
        raise ValueError(
            "The maximum batch size must be between 1 and 43."
        )
    names = set(
        document["relativePath"]
        for document in manifest["documents"]
        if select_all
    )
    if not names:
        names = set(requested or CALIBRATION_DOCUMENTS)
    selected = [
        document for document in manifest["documents"]
        if document["relativePath"] in names
    ]
    missing = names.difference(
        document["relativePath"] for document in selected
    )
    if missing:
        raise ValueError(
            f"Unknown corpus documents: {sorted(missing)}"
        )
    if len(selected) > maximum:
        raise ValueError(
            f"Selected {len(selected)} documents; maximum is {maximum}."
        )
    return selected


def reproject_document(
    document: dict[str, Any],
    evidence_root: Path,
    client: InventoryApi,
    poll_seconds: int,
    max_wait_seconds: int,
) -> str:
    import_id = document.get("processing", {}).get("importId")
    if not import_id:
        raise RuntimeError(
            "Corpus document does not have a durable import."
        )
    current = client.read_import(import_id)
    latest = latest_attempt(current)
    if latest and latest["providerName"] == RETAINED_PROVIDER:
        if latest["status"] in ACTIVE_ATTEMPT_STATES:
            current = wait(
                client, import_id, poll_seconds, max_wait_seconds
            )
        elif (
            latest["status"] == "COMPLETED"
            and current["status"] == REVIEW_REQUIRED
        ):
            preflight = client.request(
                "GET",
                client.tenant_path(
                    "/inventory-semantic-preflight?importId=" +
                    import_id
                ),
            )
            sources = preflight.get("sources") or []
            if not any(
                item.get("importId") == import_id and
                item.get("safeToReproject") is True
                for item in sources
            ):
                preserve_observed_artifact(
                    document, evidence_root, current, client
                )
                return "cached-reprojection"
            current = client.reproject_import(
                import_id,
                current["version"],
                document["sha256"],
                latest["attemptNumber"] + 1,
            )
            if current["status"] != REVIEW_REQUIRED:
                current = wait(
                    client, import_id, poll_seconds,
                    max_wait_seconds,
                )
        elif (
            latest["status"] == "FAILED_TERMINAL"
            and current["status"] == REVIEW_REQUIRED
        ):
            current = client.reproject_import(
                import_id, current["version"], document["sha256"],
                latest["attemptNumber"] + 1,
            )
            if current["status"] != REVIEW_REQUIRED:
                current = wait(
                    client, import_id, poll_seconds,
                    max_wait_seconds,
                )
        else:
            raise RuntimeError(
                "The retained reprojection is terminal and requires "
                "operator review."
            )
    elif current["status"] == REVIEW_REQUIRED:
        current = client.reproject_import(
            import_id, current["version"], document["sha256"],
            (latest["attemptNumber"] + 1) if latest else 1,
        )
        if current["status"] != REVIEW_REQUIRED:
            current = wait(
                client, import_id, poll_seconds, max_wait_seconds
            )
    else:
        raise RuntimeError(
            "The corpus import is not ready for retained reprojection."
        )
    if current["status"] != REVIEW_REQUIRED:
        raise RuntimeError(
            "Retained reprojection did not stop at human review."
        )
    latest = latest_attempt(current)
    if (
        latest is None
        or latest["providerName"] != RETAINED_PROVIDER
        or latest["status"] != "COMPLETED"
        or not latest.get("extractedArtifactId")
    ):
        raise RuntimeError(
            "Retained reprojection did not create an accepted projection."
        )
    document.setdefault("processing", {})["attempt"] = {
        "importStatus": current["status"],
        "attempt": latest,
    }
    preserve_observed_artifact(
        document, evidence_root, current, client
    )
    return "reprojected"


def wait(
    client: InventoryApi,
    import_id: str,
    poll_seconds: int,
    max_wait_seconds: int,
) -> dict[str, Any]:
    return client.wait_for_extraction(
        import_id, poll_seconds, max_wait_seconds,
        lambda _: None, lambda: None,
    )


def latest_attempt(
    current: dict[str, Any],
) -> dict[str, Any] | None:
    attempts = current.get("extractionAttempts") or []
    return max(
        attempts,
        key=lambda attempt: attempt["attemptNumber"],
        default=None,
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError, ValueError, KeyError, RuntimeError,
        json.JSONDecodeError, requests.RequestException,
    ) as error:
        print(json.dumps({
            "passed": False,
            "error": str(error),
        }), file=sys.stderr)
        raise SystemExit(2)
