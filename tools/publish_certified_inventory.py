"""Approve and publish the fully certified 43-file corpus through the API."""

from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_corpus_api import InventoryApi
from inventory_publication_contract import ContractError, discover_operation
from inventory_publication_operations import (
    approve_candidates,
    publish_import,
    verify_publication,
)

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
OPENAPI_PATH = (
    REPO_ROOT / "shared" / "contracts" / "openapi"
    / "advertified-commercial-api.v1.json"
)
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    args = parse_args()
    root = args.evidence.resolve(strict=True)
    require_certifications(root)
    readiness = read_json(
        root / "publication" / "publication-readiness.json"
    )
    if readiness.get("verdict") != "PASS":
        raise RuntimeError(
            "Every certified candidate must pass publication readiness."
        )

    contract = read_json(OPENAPI_PATH)
    review_operation = discover_operation(
        contract,
        path_terms=("inventory-imports", "candidate"),
        operation_terms=("review",),
    )
    publish_operation = discover_operation(
        contract,
        path_terms=("inventory-imports", ":publish"),
        operation_terms=("publish",),
    )
    client = InventoryApi(
        args.api_base_url, args.origin, args.tenant_id
    )
    client.start_session()
    sources = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    ).get("sources") or []
    if len(sources) != 43:
        raise RuntimeError("The live API does not expose all 43 sources.")

    records = process_sources(
        client,
        contract,
        review_operation,
        publish_operation,
        sources,
        args.dry_run,
    )
    verification = verify_publication(
        client, records, args.dry_run
    )
    report = publication_report(
        records, verification, args.dry_run
    )
    output = root / "publication" / "corpus-publication.json"
    write_json(output, report)
    print(json.dumps({
        "verdict": report["verdict"],
        "sourceCount": report["sourceCount"],
        "approvedCandidateCount": report["approvedCandidateCount"],
        "publishedProductCount": report["publishedProductCount"],
        "publishedRateCount": report["publishedRateCount"],
        "publishedAvailabilityCount": report[
            "publishedAvailabilityCount"
        ],
        "lineageSourceCount": report["lineageSourceCount"],
        "failureCount": len(report["failures"]),
    }, indent=2))
    return 0 if report["verdict"] == "PASS" else 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    parser.add_argument(
        "--api-base-url", default="http://127.0.0.1:5197"
    )
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def require_certifications(root: Path) -> None:
    physical = read_json(
        root / "physical-certification"
        / "corpus-physical-certification.json"
    )
    bedrock = read_json(
        root / "bedrock-certification"
        / "corpus-bedrock-certification.json"
    )
    if (
        physical.get("verdict") != "PASS"
        or physical.get("passedSourceCount") != 43
    ):
        raise RuntimeError("43/43 physical certification is required.")
    if (
        bedrock.get("verdict") != "PASS"
        or bedrock.get("passedSourceCount") != 43
    ):
        raise RuntimeError("43/43 Bedrock certification is required.")
    cost = bedrock.get("cost") or {}
    if (
        not cost.get("passed")
        or int(cost.get("budgetAccountedTotalUsdMicros") or 0)
        > 5_000_000
    ):
        raise RuntimeError("The Bedrock run exceeds the US$5 ceiling.")


def process_sources(
    client: InventoryApi,
    contract: dict[str, Any],
    review_operation: dict[str, Any],
    publish_operation: dict[str, Any],
    sources: list[dict[str, Any]],
    dry_run: bool,
) -> list[dict[str, Any]]:
    records = []
    ordered = sorted(sources, key=lambda item: item["fileName"].lower())
    for position, source in enumerate(ordered, start=1):
        current = client.read_complete_import(str(source["importId"]))
        approved = approve_candidates(
            client,
            contract,
            review_operation,
            source,
            current,
            dry_run,
        )
        current = client.read_complete_import(str(source["importId"]))
        published = publish_import(
            client,
            contract,
            publish_operation,
            source,
            current,
            dry_run,
        )
        record = {
            "position": position,
            "fileName": source["fileName"],
            "sourceHash": source["sourceHash"],
            "importId": source["importId"],
            "approvedCandidateCount": approved,
            "publishResult": published,
        }
        records.append(record)
        print(json.dumps({
            "position": position,
            "total": len(ordered),
            "fileName": source["fileName"],
            "approvedCandidateCount": approved,
            "published": published.get("published", False),
        }), flush=True)
    return records


def publication_report(
    records: list[dict[str, Any]],
    verification: dict[str, Any],
    dry_run: bool,
) -> dict[str, Any]:
    return {
        "schemaVersion": "advertified.inventory-corpus-publication.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "dryRun": dry_run,
        "verdict": "PASS" if verification["passed"] else "FAIL",
        "sourceCount": len(records),
        "approvedCandidateCount": sum(
            item["approvedCandidateCount"] for item in records
        ),
        "publishedProductCount": verification[
            "publishedProductCount"
        ],
        "publishedRateCount": verification["publishedRateCount"],
        "publishedAvailabilityCount": verification[
            "publishedAvailabilityCount"
        ],
        "lineageSourceCount": verification["lineageSourceCount"],
        "failures": verification["failures"],
        "documents": records,
    }


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(
        path.resolve(strict=True).read_text(encoding="utf-8")
    )
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
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
        ContractError,
        RuntimeError,
        json.JSONDecodeError,
        requests.RequestException,
    ) as error:
        print(json.dumps({"verdict": "ERROR", "error": str(error)}))
        raise SystemExit(3)
