"""Assemble canonical inventory only from certified Bedrock responses.

The command is read-only with respect to the application database. It fails
closed unless all 43 physical files and all Bedrock packets are certified.
"""

from __future__ import annotations

import hashlib
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
PHYSICAL_MARKER = CORPUS / "physical-certification-v2" / "ALL_43_PHYSICALLY_CERTIFIED.json"
BEDROCK_ROOT = CORPUS / "bedrock-production-certification"
BEDROCK_MARKER = BEDROCK_ROOT / "ALL_BEDROCK_RESPONSES_CERTIFIED.json"
PLAN_PATH = BEDROCK_ROOT / "plan.json"
LEDGER_PATH = BEDROCK_ROOT / "usage-ledger.json"
OUTPUT_ROOT = CORPUS / "certified-canonical-inventory"
OUTPUT_PATH = OUTPUT_ROOT / "canonical-products.json"
COMPLETE_MARKER = OUTPUT_ROOT / "ALL_CANONICAL_PRODUCTS_ASSEMBLED.json"


def main() -> int:
    require(PHYSICAL_MARKER, "all43PhysicallyCertified")
    require(BEDROCK_MARKER, "allBedrockResponsesCertified")
    plan = read_json(PLAN_PATH)
    ledger = read_json(LEDGER_PATH)
    packet_summaries = {str(item["packetId"]): item for item in plan.get("packets") or []}
    completed = {
        str(item["packetId"]): item
        for item in ledger.get("entries") or []
        if item.get("status") == "COMPLETED" and item.get("validationPassed")
    }
    if set(completed) != set(packet_summaries):
        raise RuntimeError("Certified Bedrock packet ledger is incomplete.")

    products: list[dict[str, Any]] = []
    covered_rows: set[str] = set()
    expected_rows: set[str] = set()
    source_hashes: set[str] = set()
    for packet_id, summary in packet_summaries.items():
        packet = read_json(ROOT / summary["requestPath"])
        result = read_json(ROOT / completed[packet_id]["resultPath"])
        response = result.get("response") or {}
        facts = {
            str(row["physicalRowId"]): row
            for row in packet["input"]["physicalRows"]
        }
        expected_rows.update(facts)
        source_hash = str(packet["sourceHash"])
        source_hashes.add(source_hash)
        for product_index, product in enumerate(response.get("products") or []):
            source_ids = [str(value) for value in product.get("sourceRowIds") or []]
            source_rows = [facts[value] for value in source_ids]
            covered_rows.update(source_ids)
            products.append(assemble_product(
                packet,
                result,
                product,
                product_index,
                source_rows,
            ))
        for item in response.get("nonProductFacts") or []:
            covered_rows.add(str(item["sourceRowId"]))
    failures = []
    if len(source_hashes) != 43:
        failures.append("NOT_ALL_SOURCES_PRESENT")
    if covered_rows != expected_rows:
        failures.append("PHYSICAL_ROW_RECONCILIATION_FAILED")
    duplicate_codes = duplicates(item["productCode"] for item in products)
    if duplicate_codes:
        failures.append("DUPLICATE_PRODUCT_CODES")
    duplicate_identities = duplicates(
        canonical_signature(item) for item in products
    )
    if duplicate_identities:
        failures.append("DUPLICATE_CANONICAL_PRODUCTS")
    if not products:
        failures.append("NO_CANONICAL_PRODUCTS")
    if failures:
        COMPLETE_MARKER.unlink(missing_ok=True)
        raise RuntimeError(", ".join(failures))

    payload = {
        "schemaVersion": "advertified.certified-canonical-inventory.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "sourceCount": len(source_hashes),
        "physicalRowCount": len(expected_rows),
        "reconciledPhysicalRowCount": len(covered_rows),
        "canonicalProductCount": len(products),
        "bedrockActualNewCostUsdMicros": sum(
            int(item.get("actualCostUsdMicros") or 0)
            for item in completed.values()
        ),
        "bedrockHistoricalUsageReserveUsdMicros": int(
            ledger.get("historicalUsageReserveUsdMicros") or 0
        ),
        "bedrockProgrammeUsedOrReservedUsdMicros": int(
            ledger.get("programmeUsedOrReservedUsdMicros") or 0
        ),
        "bedrockProgrammeLimitUsdMicros": int(
            ledger.get("programmeLimitUsdMicros") or 5_000_000
        ),
        "products": sorted(products, key=lambda item: (
            item["supplierName"], item["sourceHash"], item["name"], item["productCode"]
        )),
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    write_json(OUTPUT_PATH, payload)
    write_json(COMPLETE_MARKER, {
        "allCanonicalProductsAssembled": True,
        "sourceCount": payload["sourceCount"],
        "physicalRowCount": payload["physicalRowCount"],
        "canonicalProductCount": payload["canonicalProductCount"],
        "canonicalInventorySha256": file_hash(OUTPUT_PATH),
        "bedrockProgrammeUsedOrReservedUsdMicros": payload[
            "bedrockProgrammeUsedOrReservedUsdMicros"
        ],
        "bedrockProgrammeLimitUsdMicros": payload[
            "bedrockProgrammeLimitUsdMicros"
        ],
    })
    print(json.dumps({
        "sourceCount": payload["sourceCount"],
        "physicalRowCount": payload["physicalRowCount"],
        "canonicalProductCount": payload["canonicalProductCount"],
        "bedrockProgrammeUsedOrReservedUsd": payload[
            "bedrockProgrammeUsedOrReservedUsdMicros"
        ] / 1_000_000,
        "bedrockProgrammeLimitUsd": payload[
            "bedrockProgrammeLimitUsdMicros"
        ] / 1_000_000,
        "output": str(OUTPUT_PATH.relative_to(ROOT)),
    }, indent=2))
    return 0


def assemble_product(
    packet: dict[str, Any],
    result: dict[str, Any],
    product: dict[str, Any],
    product_index: int,
    source_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    source_ids = [str(value) for value in product["sourceRowIds"]]
    rate_id = product.get("rateSourceRowId")
    rate_row = next((row for row in source_rows if row["physicalRowId"] == rate_id), None)
    if rate_row is None:
        rate_row = next((row for row in source_rows if row.get("rateRaw")), None)
    seed = "\n".join([
        str(packet["sourceHash"]),
        *sorted(source_ids),
        str(product.get("name") or ""),
    ])
    product_hash = hashlib.sha256(seed.encode()).hexdigest()
    supplier = str(packet["input"]["supplierName"])
    evidence = []
    for row in source_rows:
        evidence.extend(row.get("evidence") or [])
    evidence = unique_evidence(evidence)
    return {
        "productCode": "ADV-" + product_hash[:16].upper(),
        "supplierName": supplier,
        "sourceHash": packet["sourceHash"],
        "sourceFileName": packet["input"]["fileName"],
        "sourcePacketId": packet["packetId"],
        "sourceResultHash": file_hash(ROOT / result_path_for(result)),
        "sourceRowIds": source_ids,
        "sourceLocators": sorted({
            str(row.get("sourceLocator") or "") for row in source_rows
            if row.get("sourceLocator")
        }),
        "name": str(product["name"]).strip(),
        "channel": product["channel"],
        "productType": product["productType"],
        "geography": clean_optional(product.get("geography")),
        "description": str(product["description"]).strip(),
        "rateRaw": rate_row.get("rateRaw") if rate_row else None,
        "rateAmountMinor": rate_row.get("rateAmountMinor") if rate_row else None,
        "currency": rate_row.get("currency") if rate_row else None,
        "rateType": product.get("rateType"),
        "availability": product["availability"],
        "validFrom": product.get("validFrom"),
        "validTo": product.get("validTo"),
        "reviewNotes": product.get("reviewNotes") or [],
        "ambiguityCodes": sorted({
            value for row in source_rows
            for value in row.get("ambiguityCodes") or []
        }),
        "physicalFacts": source_rows,
        "evidence": evidence,
        "bedrock": {
            "modelId": result["modelId"],
            "promptVersion": result["promptVersion"],
            "requestHash": result["requestHash"],
            "packetId": result["packetId"],
            "productIndex": product_index,
            "validationPassed": result["validationPassed"],
            "actualCostUsdMicros": result["actualCostUsdMicros"],
            "purpose": result["purpose"],
            "reason": result["reason"],
        },
    }


def result_path_for(result: dict[str, Any]) -> Path:
    packet_id = str(result["packetId"])
    return Path("artifacts") / "inventory-corpus" / "bedrock-production-certification" / "results" / f"{packet_id}.json"


def canonical_signature(item: dict[str, Any]) -> str:
    return "|".join([
        normalize(item["supplierName"]),
        normalize(item["name"]),
        normalize(str(item.get("geography") or "")),
        str(item.get("rateAmountMinor")),
        normalize(str(item.get("rateRaw") or "")),
    ])


def duplicates(values: Any) -> list[str]:
    counts: dict[str, int] = {}
    for value in values:
        key = str(value)
        counts[key] = counts.get(key, 0) + 1
    return sorted(key for key, count in counts.items() if count > 1)


def unique_evidence(values: list[dict[str, Any]]) -> list[dict[str, Any]]:
    result, seen = [], set()
    for value in values:
        key = (
            str(value.get("field") or ""),
            str(value.get("raw") or ""),
            str(value.get("sourceLocator") or ""),
        )
        if key in seen:
            continue
        seen.add(key)
        result.append(value)
    return result


def clean_optional(value: Any) -> str | None:
    text = " ".join(str(value or "").split())
    return text or None


def normalize(value: str) -> str:
    return "".join(char.lower() for char in value if char.isalnum())


def require(path: Path, field: str) -> None:
    value = read_json(path)
    if not value.get(field):
        raise RuntimeError(f"Required certificate {path.name} is not valid.")


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
