"""Print one file's physical-anchor-to-candidate matching evidence.

Read-only diagnostic. It never invokes Bedrock or mutates an inventory import.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from inventory_corpus_api import InventoryApi
from inventory_physical_anchor_discovery import discover_anchors
from inventory_physical_certification_support import (
    anchor_view,
    candidate_match_score,
    candidate_view,
    match_inventory,
)
from inventory_physical_facts import load_source

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--document", required=True)
    parser.add_argument("--api-base-url", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    args = parser.parse_args()

    root = args.evidence.resolve(strict=True)
    manifest = json.loads((root / "source-manifest.json").read_text(encoding="utf-8"))
    document = next(
        (item for item in manifest["documents"] if item["relativePath"] == args.document),
        None,
    )
    if document is None:
        raise ValueError(f"Unknown corpus document: {args.document}")

    client = InventoryApi(args.api_base_url, args.origin, args.tenant_id)
    client.start_session()
    preflight = client.request("GET", client.tenant_path("/inventory-semantic-preflight"))
    if preflight.get("liveExecutionEnabled"):
        raise RuntimeError("Physical diagnostics require Bedrock to be disabled.")
    source = next(
        item for item in preflight["sources"]
        if item["sourceHash"] == document["sha256"]
    )
    import_view = client.read_complete_import(source["importId"])
    physical = load_source(root / "semantic-v1" / f"{document['sha256']}.json")
    anchors = discover_anchors(physical)

    from inventory_physical_certification_support import candidate_fact
    candidates = tuple(candidate_fact(item) for item in import_view.get("candidates") or [])
    matches, unmatched, unsupported = match_inventory(physical, anchors, candidates)

    payload = {
        "fileName": args.document,
        "anchorCount": len(anchors),
        "candidateCount": len(candidates),
        "matches": [
            {
                "anchor": anchor_view(anchor),
                "candidate": candidate_view(candidate),
                "score": candidate_match_score(anchor, candidate),
            }
            for anchor, candidate in matches
        ],
        "unmatchedAnchors": [anchor_view(item) for item in unmatched],
        "unsupportedCandidates": [candidate_view(item) for item in unsupported],
    }
    print(json.dumps(payload, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
