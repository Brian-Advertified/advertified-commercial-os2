"""Verify that the connected brief-to-proposal journey uses certified corpus inventory."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Iterable
from urllib.parse import urlencode

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
CERT = CORPUS / "certification"
OPENAPI = ROOT / "shared" / "contracts" / "openapi" / "advertified-commercial-api.v1.json"
TENANT = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    upload = read_json(CERT / "inventory-upload-verification.json")
    if upload.get("passed") is not True:
        raise RuntimeError("Inventory upload verification has not passed.")
    if int(upload.get("publishedCandidateCount") or 0) <= 0:
        raise RuntimeError("No certified inventory was published.")

    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = preflight.get("sources") or []
    published_codes: set[str] = set()
    for source in sources:
        view = api.read_complete_import(str(source["importId"]))
        for candidate in view.get("candidates") or []:
            if str(candidate.get("status") or "") != "PUBLISHED":
                continue
            values = candidate_values(candidate)
            code = str(values.get("productCode") or "").strip()
            if code:
                published_codes.add(code)
    if not published_codes:
        raise RuntimeError("Published candidate codes could not be read back.")

    contract = read_json(OPENAPI)
    product_payloads = query_collections(
        api,
        contract,
        include=("inventory", "product"),
        exclude=("import", "candidate", "semantic", "preflight"),
    )
    product_ids = product_identifiers(product_payloads, published_codes)
    proposal_payloads = query_collections(
        api,
        contract,
        include=("proposal",),
        exclude=("pdf", "share", "approve", "download"),
    )
    brief_payloads = query_collections(
        api,
        contract,
        include=("brief",),
        exclude=("approve", "extract", "research"),
    )

    searchable = flatten_strings(proposal_payloads)
    matched_codes = sorted(code for code in published_codes if code in searchable)
    matched_ids = sorted(identifier for identifier in product_ids if identifier in searchable)
    failures: list[str] = []
    if not proposal_payloads:
        failures.append("NO_PROPOSAL_PAYLOAD_FOUND")
    if not brief_payloads:
        failures.append("NO_BRIEF_PAYLOAD_FOUND")
    if not matched_codes and not matched_ids:
        failures.append("PROPOSAL_DOES_NOT_REFERENCE_CERTIFIED_CORPUS_INVENTORY")

    result = {
        "schemaVersion": "advertified.brief-to-proposal-inventory-verification.v1",
        "verifiedAtUtc": datetime.now(UTC).isoformat(),
        "passed": not failures,
        "sourceCount": len(sources),
        "publishedCorpusProductCodeCount": len(published_codes),
        "publishedInventoryProductIdCount": len(product_ids),
        "matchedProductCodes": matched_codes[:100],
        "matchedProductIds": matched_ids[:100],
        "proposalPayloadCount": len(proposal_payloads),
        "briefPayloadCount": len(brief_payloads),
        "failures": failures,
    }
    output = CERT / "brief-to-proposal-verification.json"
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if not failures else 2


def query_collections(
    api: InventoryApi,
    contract: dict[str, Any],
    *,
    include: tuple[str, ...],
    exclude: tuple[str, ...],
) -> list[dict[str, Any]]:
    candidates: list[tuple[int, str]] = []
    for path, item in (contract.get("paths") or {}).items():
        operation = item.get("get")
        if not isinstance(operation, dict):
            continue
        lower = " ".join([
            path.lower(),
            str(operation.get("operationId") or "").lower(),
            str(operation.get("summary") or "").lower(),
        ])
        if not all(word in lower for word in include):
            continue
        if any(word in lower for word in exclude):
            continue
        unresolved = [part for part in route_parameters(path) if part.lower() != "tenantid"]
        if unresolved:
            continue
        score = 10 - path.count("/")
        candidates.append((score, path))
    payloads: list[dict[str, Any]] = []
    errors: list[str] = []
    for _, template in sorted(candidates, reverse=True):
        path = template.replace("{tenantId}", TENANT).replace("{tenantid}", TENANT)
        separator = "&" if "?" in path else "?"
        path += separator + urlencode({"pageSize": 100})
        try:
            value = api.request("GET", path)
        except Exception as error:  # the next compatible collection may work
            errors.append(f"{template}:{error}")
            continue
        if isinstance(value, dict):
            payloads.append(value)
    if not payloads and errors:
        raise RuntimeError("No compatible collection endpoint succeeded: " + " | ".join(errors))
    return payloads


def product_identifiers(
    payloads: list[dict[str, Any]],
    published_codes: set[str],
) -> set[str]:
    result: set[str] = set()
    for node in walk(payloads):
        if not isinstance(node, dict):
            continue
        code = first_value(node, "productCode", "code")
        identifier = first_value(node, "productId", "id")
        if code in published_codes and identifier:
            result.add(identifier)
    return result


def flatten_strings(value: Any) -> set[str]:
    result: set[str] = set()
    for node in walk(value):
        if isinstance(node, str) and node:
            result.add(node)
    return result


def walk(value: Any) -> Iterable[Any]:
    yield value
    if isinstance(value, dict):
        for item in value.values():
            yield from walk(item)
    elif isinstance(value, list):
        for item in value:
            yield from walk(item)


def first_value(node: dict[str, Any], *names: str) -> str | None:
    for name in names:
        value = node.get(name)
        if value is not None and str(value).strip():
            return str(value).strip()
    return None


def route_parameters(path: str) -> list[str]:
    result: list[str] = []
    start = 0
    while True:
        left = path.find("{", start)
        if left < 0:
            return result
        right = path.find("}", left + 1)
        if right < 0:
            return result
        result.append(path[left + 1 : right])
        start = right + 1


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
