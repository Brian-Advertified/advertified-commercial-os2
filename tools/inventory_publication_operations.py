"""Production API operations for certified inventory publication."""

from __future__ import annotations

import json
from typing import Any

from inventory_corpus_api import InventoryApi
from inventory_publication_contract import fill_path, request_body


def approve_candidates(
    client: InventoryApi,
    contract: dict[str, Any],
    operation: dict[str, Any],
    source: dict[str, Any],
    current: dict[str, Any],
    dry_run: bool,
) -> int:
    candidates = current.get("candidates") or []
    approved = 0
    for candidate in candidates:
        status = str(candidate.get("status") or "")
        if status in {"APPROVED", "PUBLISHED"}:
            approved += 1
            continue
        if status != "REVIEW_REQUIRED":
            raise RuntimeError(
                f"Candidate {candidate.get('id')} has unexpected status {status}."
            )
        blocking = [
            item
            for item in candidate.get("validation") or []
            if item.get("isBlocking")
        ]
        if blocking:
            raise RuntimeError(
                f"Candidate {candidate.get('id')} still has blocking issues: "
                + json.dumps(blocking, sort_keys=True)
            )
        path = fill_path(
            operation["path"],
            tenantId=client.tenant_id,
            importId=str(source["importId"]),
            candidateId=str(candidate["id"]),
        )
        body = request_body(
            contract,
            operation["operation"],
            expected_version=(
                candidate.get("version") or current.get("version")
            ),
            decision="APPROVE",
            values=candidate_values(candidate),
            source_hash=str(source["sourceHash"]),
        )
        if not dry_run:
            client.request(operation["method"], path, body)
        approved += 1
    return approved


def publish_import(
    client: InventoryApi,
    contract: dict[str, Any],
    operation: dict[str, Any],
    source: dict[str, Any],
    current: dict[str, Any],
    dry_run: bool,
) -> dict[str, Any]:
    statuses = {
        str(candidate.get("status") or "")
        for candidate in current.get("candidates") or []
    }
    if statuses == {"PUBLISHED"} or current.get("status") == "COMPLETED":
        return {"published": True, "cached": True}
    if statuses != {"APPROVED"}:
        raise RuntimeError(
            f"Import {source['importId']} is not fully approved: {sorted(statuses)}"
        )
    if dry_run:
        return {"published": False, "dryRunReady": True}
    path = fill_path(
        operation["path"],
        tenantId=client.tenant_id,
        importId=str(source["importId"]),
    )
    body = request_body(
        contract,
        operation["operation"],
        expected_version=current.get("version"),
        decision=None,
        values=None,
        source_hash=str(source["sourceHash"]),
    )
    response = client.request(operation["method"], path, body)
    return {"published": True, "response": response}


def verify_publication(
    client: InventoryApi,
    records: list[dict[str, Any]],
    dry_run: bool,
) -> dict[str, Any]:
    if dry_run:
        expected = sum(item["approvedCandidateCount"] for item in records)
        return {
            "passed": True,
            "publishedProductCount": expected,
            "publishedRateCount": 0,
            "publishedAvailabilityCount": expected,
            "lineageSourceCount": len(records),
            "failures": [],
        }
    expected = sum(item["approvedCandidateCount"] for item in records)
    published_products = 0
    published_rates = 0
    published_availability = 0
    lineage_sources = 0
    failures: list[str] = []
    for record in records:
        current = client.read_complete_import(str(record["importId"]))
        candidates = current.get("candidates") or []
        statuses = {
            str(candidate.get("status") or "")
            for candidate in candidates
        }
        if current.get("status") != "COMPLETED":
            failures.append(f"IMPORT_NOT_COMPLETED:{record['importId']}")
        if statuses != {"PUBLISHED"}:
            failures.append(
                f"CANDIDATES_NOT_PUBLISHED:{record['importId']}:{sorted(statuses)}"
            )
        if (
            str(current.get("sourceHash") or "")
            != str(record["sourceHash"])
        ):
            failures.append(
                f"PUBLISHED_SOURCE_HASH_CHANGED:{record['importId']}"
            )
        if current.get("status") == "COMPLETED" and statuses == {"PUBLISHED"}:
            lineage_sources += 1
        for candidate in candidates:
            if candidate.get("status") != "PUBLISHED":
                continue
            published_products += 1
            values = candidate_values(candidate)
            if values.get("rateAmountMinor") is not None:
                published_rates += 1
            if values.get("availability"):
                published_availability += 1
    if published_products != expected:
        failures.append(
            "PUBLISHED_PRODUCT_COUNT_DIFFERS_FROM_APPROVED_COUNT"
        )
    if lineage_sources != len(records):
        failures.append("PUBLISHED_LINEAGE_DOES_NOT_COVER_43_SOURCES")
    return {
        "passed": not failures,
        "publishedProductCount": published_products,
        "publishedRateCount": published_rates,
        "publishedAvailabilityCount": published_availability,
        "lineageSourceCount": lineage_sources,
        "failures": failures,
    }


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )
