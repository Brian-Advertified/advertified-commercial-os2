"""Verify that published certified inventory is visible through the normal API."""

from __future__ import annotations

import hashlib
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_certified_upload_client import (
    CertifiedInventoryApi,
    default_scalar,
    flatten_objects,
)

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
UPLOAD_ROOT = CORPUS / "certified-upload"
PUBLICATION_MARKER = UPLOAD_ROOT / "PUBLISHABLE_INVENTORY_PUBLISHED.json"
CONTRACT_PATH = UPLOAD_ROOT / "api-contract.json"
CANONICAL_PATH = CORPUS / "certified-canonical-inventory" / "canonical-products.json"
OUTPUT_PATH = UPLOAD_ROOT / "marketplace-visibility.json"
MARKER_PATH = UPLOAD_ROOT / "CERTIFIED_INVENTORY_VISIBLE.json"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    require(PUBLICATION_MARKER, "publishableInventoryPublished")
    contract = read_json(CONTRACT_PATH)
    canonical = read_json(CANONICAL_PATH)
    expected = {
        str(item["productCode"]): item
        for item in canonical.get("products") or []
    }
    client = CertifiedInventoryApi(
        "http://127.0.0.1:5197",
        "http://localhost:3017",
        TENANT_ID,
        contract,
    )
    client.authenticate()
    operations = [
        item for item in contract.get("operations") or []
        if item.get("method") == "GET"
        and any(token in str(item.get("path") or "").lower() for token in (
            "inventory-products", "marketplace", "/inventory",
        ))
        and "inventory-imports" not in str(item.get("path") or "").lower()
        and "{" not in str(item.get("path") or "").replace("{tenantId}", "")
    ]
    if not operations:
        raise RuntimeError("No inventory marketplace/list operation exists in OpenAPI.")
    observed_objects: list[dict[str, Any]] = []
    operation_results = []
    for operation in operations:
        params = parameters(operation)
        response = client.request_operation(operation, params=params)
        if response.status_code >= 400:
            operation_results.append({
                "path": operation["path"],
                "statusCode": response.status_code,
                "objectCount": 0,
            })
            continue
        payload = response.json()
        objects = flatten_objects(payload)
        observed_objects.extend(objects)
        operation_results.append({
            "path": operation["path"],
            "statusCode": response.status_code,
            "objectCount": len(objects),
        })
    observed_codes = {
        str(item.get("productCode"))
        for item in observed_objects
        if item.get("productCode")
    }
    matched_codes = sorted(observed_codes & set(expected))
    matched_sources = sorted({
        str(expected[code]["sourceHash"])
        for code in matched_codes
    })
    publication = read_json(PUBLICATION_MARKER)
    expected_published = int(publication.get("publishedCandidateCount") or 0)
    failures = []
    if not matched_codes:
        failures.append("NO_CERTIFIED_PRODUCT_VISIBLE")
    if len(matched_codes) < min(1, expected_published):
        failures.append("VISIBLE_CERTIFIED_PRODUCT_COUNT_TOO_LOW")
    report = {
        "schemaVersion": "advertified.certified-marketplace-visibility.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "publicationCertificateSha256": file_hash(PUBLICATION_MARKER),
        "expectedPublishedCandidateCount": expected_published,
        "observedProductCodeCount": len(observed_codes),
        "visibleCertifiedProductCount": len(matched_codes),
        "visibleCertifiedSourceCount": len(matched_sources),
        "visibleCertifiedProductCodes": matched_codes,
        "visibleCertifiedSourceHashes": matched_sources,
        "operations": operation_results,
        "passed": not failures,
        "failures": failures,
    }
    write_json(OUTPUT_PATH, report)
    if failures:
        MARKER_PATH.unlink(missing_ok=True)
        raise RuntimeError(", ".join(failures))
    write_json(MARKER_PATH, {
        "certifiedInventoryVisible": True,
        "visibleCertifiedProductCount": len(matched_codes),
        "visibleCertifiedSourceCount": len(matched_sources),
        "marketplaceVisibilitySha256": file_hash(OUTPUT_PATH),
    })
    print(json.dumps(report, indent=2))
    return 0


def parameters(operation: dict[str, Any]) -> dict[str, Any]:
    result = {}
    for parameter in operation.get("parameters") or []:
        if parameter.get("in") != "query":
            continue
        name = str(parameter.get("name") or "")
        lower = name.lower()
        if lower in {"pagesize", "limit", "take"}:
            result[name] = 100
        elif lower in {"includeinactive", "includearchived"}:
            result[name] = False
        elif parameter.get("required"):
            result[name] = default_scalar(parameter.get("schema") or {}, {})
    return result


def require(path: Path, field: str) -> None:
    value = read_json(path)
    if not value.get(field):
        raise RuntimeError(f"Required certificate {path.name} is invalid.")


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.resolve(strict=True).read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
