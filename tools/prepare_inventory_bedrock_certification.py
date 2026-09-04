"""Prepare governed Bedrock packets from 43 certified physical transcriptions.

No network request is made. The script fails closed unless all 43 sources have
passed physical certification and the conservative programme maximum is <= $5.
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
TRANSCRIPTION_ROOT = CORPUS / "physical-transcription"
MANIFEST_PATH = CORPUS / "source-manifest.json"
OUTPUT_ROOT = CORPUS / "bedrock-production-certification"
REQUEST_ROOT = OUTPUT_ROOT / "requests"
PLAN_PATH = OUTPUT_ROOT / "plan.json"
APPROVED_MARKER = OUTPUT_ROOT / "PREFLIGHT_APPROVED.json"

MODEL_ID = "us.amazon.nova-pro-v1:0"
PROMPT_VERSION = "inventory-production-certification-v1"
PROGRAMME_LIMIT_USD_MICROS = 5_000_000
# Covers the previously audited direct-call uncertainty as well as known retained usage.
HISTORICAL_USAGE_RESERVE_USD_MICROS = 188_122
INPUT_USD_MICROS_PER_MILLION = 800_000
OUTPUT_USD_MICROS_PER_MILLION = 3_200_000
MAX_ROWS_PER_PACKET = 25
MAX_PACKET_CHARACTERS = 45_000
OUTPUT_TOKENS_PER_ROW = 180
OUTPUT_OVERHEAD_TOKENS = 500
INPUT_TOKEN_CHARACTER_DIVISOR = 3

SYSTEM_INSTRUCTION = """You classify and describe already-certified advertising inventory source facts.
The physical transcription is immutable source truth. Never invent a supplier, product, rate, rate basis,
date, geography, availability exception, dimension, audience fact, deliverable, package component, or term.
Use only supplied row IDs and evidence. Unknown remains null. A source fact may be marked NON_PRODUCT only
when it is clearly a heading, explanatory note, contact detail, term, or duplicate evidence attached to a
product. Every source row ID must appear exactly once either in a product sourceRowIds list or in
nonProductFacts. Preserve ambiguous raw prices and add a review note; never complete missing digits.
Availability policy: unless the source explicitly states not available, blackout, or conflict, classify the
product as AVAILABLE. Buying basis and validity dates may only be populated when explicit in source facts.
Return JSON matching the supplied schema and no other text."""

OUTPUT_SCHEMA = {
    "type": "object",
    "required": ["sourceHash", "products", "nonProductFacts"],
    "properties": {
        "sourceHash": {"type": "string"},
        "products": {
            "type": "array",
            "items": {
                "type": "object",
                "required": [
                    "sourceRowIds", "name", "channel", "productType",
                    "availability", "description", "reviewNotes",
                ],
                "properties": {
                    "sourceRowIds": {"type": "array", "minItems": 1, "items": {"type": "string"}},
                    "name": {"type": "string"},
                    "channel": {"type": "string"},
                    "productType": {"type": "string"},
                    "geography": {"type": ["string", "null"]},
                    "description": {"type": "string"},
                    "rateSourceRowId": {"type": ["string", "null"]},
                    "rateType": {"type": ["string", "null"]},
                    "availability": {"type": "string", "enum": ["AVAILABLE", "UNAVAILABLE", "LIMITED"]},
                    "validFrom": {"type": ["string", "null"]},
                    "validTo": {"type": ["string", "null"]},
                    "reviewNotes": {"type": "array", "items": {"type": "string"}},
                },
                "additionalProperties": False,
            },
        },
        "nonProductFacts": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["sourceRowId", "reason", "attachToProductIndex"],
                "properties": {
                    "sourceRowId": {"type": "string"},
                    "reason": {"type": "string"},
                    "attachToProductIndex": {"type": ["integer", "null"]},
                },
                "additionalProperties": False,
            },
        },
    },
    "additionalProperties": False,
}


def main() -> int:
    physical = read_json(PHYSICAL_MARKER)
    if not physical.get("all43PhysicallyCertified") or int(physical.get("sourceCount") or 0) != 43:
        raise RuntimeError("All 43 physical files must be certified before Bedrock preflight.")
    manifest = read_json(MANIFEST_PATH)
    documents = manifest.get("documents") or []
    if len(documents) != 43:
        raise RuntimeError("The governed source manifest must contain exactly 43 files.")
    REQUEST_ROOT.mkdir(parents=True, exist_ok=True)
    for old in REQUEST_ROOT.glob("*.json"):
        old.unlink()
    packets: list[dict[str, Any]] = []
    for document in documents:
        source_hash = str(document["sha256"])
        transcription = read_json(TRANSCRIPTION_ROOT / f"{source_hash}.json")
        verify_transcription(document, transcription)
        rows = [compact_row(row) for row in (transcription.get("rows") or [])]
        for index, chunk in enumerate(chunk_rows(rows), start=1):
            packet = make_packet(document, transcription, chunk, index)
            path = REQUEST_ROOT / f"{source_hash}.{index:03d}.json"
            write_json(path, packet)
            packets.append(packet_summary(packet, path))
    projected = sum(item["maximumCostUsdMicros"] for item in packets)
    programme_maximum = HISTORICAL_USAGE_RESERVE_USD_MICROS + projected
    remaining_after = PROGRAMME_LIMIT_USD_MICROS - programme_maximum
    plan = {
        "schemaVersion": "advertified.inventory-bedrock-production-plan.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "modelId": MODEL_ID,
        "promptVersion": PROMPT_VERSION,
        "purpose": "PHYSICAL_INVENTORY_CLASSIFICATION_AND_DESCRIPTION",
        "sourceCount": 43,
        "packetCount": len(packets),
        "physicalRowCount": sum(item["rowCount"] for item in packets),
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "historicalUsageReserveUsdMicros": HISTORICAL_USAGE_RESERVE_USD_MICROS,
        "newMaximumCostUsdMicros": projected,
        "programmeMaximumCostUsdMicros": programme_maximum,
        "remainingProgrammeAllowanceUsdMicros": remaining_after,
        "withinProgrammeLimit": programme_maximum <= PROGRAMME_LIMIT_USD_MICROS,
        "pricing": {
            "inputUsdMicrosPerMillionTokens": INPUT_USD_MICROS_PER_MILLION,
            "outputUsdMicrosPerMillionTokens": OUTPUT_USD_MICROS_PER_MILLION,
        },
        "packets": packets,
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    write_json(PLAN_PATH, plan)
    if not plan["withinProgrammeLimit"]:
        if APPROVED_MARKER.exists():
            APPROVED_MARKER.unlink()
        raise RuntimeError("Conservative Bedrock plan exceeds the total US$5 programme limit.")
    write_json(APPROVED_MARKER, {
        "approved": True,
        "physicalCertificationMarkerSha256": file_hash(PHYSICAL_MARKER),
        "planSha256": file_hash(PLAN_PATH),
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "programmeMaximumCostUsdMicros": programme_maximum,
        "remainingProgrammeAllowanceUsdMicros": remaining_after,
        "paidCallsMade": 0,
    })
    print(json.dumps({
        "approved": True,
        "sourceCount": 43,
        "packetCount": len(packets),
        "physicalRowCount": plan["physicalRowCount"],
        "programmeLimitUsd": PROGRAMME_LIMIT_USD_MICROS / 1_000_000,
        "historicalUsageReserveUsd": HISTORICAL_USAGE_RESERVE_USD_MICROS / 1_000_000,
        "newMaximumCostUsd": projected / 1_000_000,
        "programmeMaximumCostUsd": programme_maximum / 1_000_000,
        "remainingProgrammeAllowanceUsd": remaining_after / 1_000_000,
        "paidCallsMade": 0,
    }, indent=2))
    return 0


def make_packet(
    document: dict[str, Any],
    transcription: dict[str, Any],
    rows: list[dict[str, Any]],
    chunk_index: int,
) -> dict[str, Any]:
    source_hash = str(document["sha256"])
    payload = {
        "sourceHash": source_hash,
        "fileName": document["relativePath"],
        "supplierName": transcription["supplierName"],
        "channelHint": transcription["channelHint"],
        "chunkIndex": chunk_index,
        "physicalRows": rows,
    }
    input_json = json.dumps(payload, separators=(",", ":"), sort_keys=True)
    estimated_input = max(1, (len(SYSTEM_INSTRUCTION) + len(input_json) + len(json.dumps(OUTPUT_SCHEMA))) // INPUT_TOKEN_CHARACTER_DIVISOR)
    maximum_output = OUTPUT_OVERHEAD_TOKENS + OUTPUT_TOKENS_PER_ROW * len(rows)
    maximum_cost = cost_usd_micros(estimated_input, maximum_output)
    packet_id = hashlib.sha256(f"{source_hash}\n{chunk_index}\n{input_json}".encode()).hexdigest()
    return {
        "schemaVersion": "advertified.inventory-bedrock-request.v1",
        "packetId": packet_id,
        "purpose": "PHYSICAL_INVENTORY_CLASSIFICATION_AND_DESCRIPTION",
        "reason": f"Classify and describe certified physical inventory from {document['relativePath']} without changing source facts.",
        "modelId": MODEL_ID,
        "promptVersion": PROMPT_VERSION,
        "systemInstruction": SYSTEM_INSTRUCTION,
        "outputSchema": OUTPUT_SCHEMA,
        "input": payload,
        "estimatedInputTokens": estimated_input,
        "maximumOutputTokens": maximum_output,
        "maximumCostUsdMicros": maximum_cost,
        "maximumAttempts": 1,
        "sourceHash": source_hash,
        "physicalCertificationRequired": True,
    }


def compact_row(row: dict[str, Any]) -> dict[str, Any]:
    evidence: list[dict[str, Any]] = []
    seen: set[tuple[str, str, str]] = set()
    for item in row.get("evidence") or []:
        field = str(item.get("field") or "source")[:80]
        raw = " ".join(str(item.get("raw") or "").split())[:600]
        locator = str(item.get("sourceLocator") or "")[:300]
        key = (field, raw, locator)
        if not raw or not locator or key in seen:
            continue
        seen.add(key)
        evidence.append({"field": field, "raw": raw, "sourceLocator": locator})
        if len(evidence) >= 10:
            break
    return {
        "physicalRowId": row.get("physicalRowId"),
        "productCode": row.get("productCode"),
        "sourceLocator": row.get("sourceLocator"),
        "sourceOrdinal": row.get("sourceOrdinal"),
        "supplierName": row.get("supplierName"),
        "channelHint": row.get("channelHint"),
        "identityRaw": row.get("identityRaw"),
        "geographyRaw": row.get("geographyRaw"),
        "descriptionRaw": str(row.get("descriptionRaw") or "")[:1_200] or None,
        "placementRaw": row.get("placementRaw"),
        "formatRaw": row.get("formatRaw"),
        "dimensionsRaw": row.get("dimensionsRaw"),
        "daypartRaw": row.get("daypartRaw"),
        "latitudeRaw": row.get("latitudeRaw"),
        "longitudeRaw": row.get("longitudeRaw"),
        "rateRaw": row.get("rateRaw"),
        "rateAmountMinor": row.get("rateAmountMinor"),
        "currency": row.get("currency"),
        "buyingBasisRaw": row.get("buyingBasisRaw"),
        "availabilityRaw": row.get("availabilityRaw"),
        "ambiguityCodes": row.get("ambiguityCodes") or [],
        "evidence": evidence,
    }


def chunk_rows(rows: list[dict[str, Any]]) -> list[list[dict[str, Any]]]: 
    chunks: list[list[dict[str, Any]]] = []
    current: list[dict[str, Any]] = []
    characters = 0
    for row in rows:
        size = len(json.dumps(row, separators=(",", ":"), sort_keys=True))
        if current and (len(current) >= MAX_ROWS_PER_PACKET or characters + size > MAX_PACKET_CHARACTERS):
            chunks.append(current)
            current = []
            characters = 0
        current.append(row)
        characters += size
    if current:
        chunks.append(current)
    return chunks


def packet_summary(packet: dict[str, Any], path: Path) -> dict[str, Any]:
    return {
        "packetId": packet["packetId"],
        "sourceHash": packet["sourceHash"],
        "fileName": packet["input"]["fileName"],
        "purpose": packet["purpose"],
        "reason": packet["reason"],
        "rowCount": len(packet["input"]["physicalRows"]),
        "estimatedInputTokens": packet["estimatedInputTokens"],
        "maximumOutputTokens": packet["maximumOutputTokens"],
        "maximumCostUsdMicros": packet["maximumCostUsdMicros"],
        "requestPath": str(path.relative_to(ROOT)),
    }


def cost_usd_micros(input_tokens: int, output_tokens: int) -> int:
    input_cost = (input_tokens * INPUT_USD_MICROS_PER_MILLION + 999_999) // 1_000_000
    output_cost = (output_tokens * OUTPUT_USD_MICROS_PER_MILLION + 999_999) // 1_000_000
    return input_cost + output_cost


def verify_transcription(document: dict[str, Any], transcription: dict[str, Any]) -> None:
    if transcription.get("sourceHash") != document.get("sha256"):
        raise RuntimeError(f"Transcription hash mismatch for {document.get('relativePath')}")
    if transcription.get("fileName") != document.get("relativePath"):
        raise RuntimeError(f"Transcription filename mismatch for {document.get('relativePath')}")
    if not transcription.get("rows"):
        raise RuntimeError(f"No physical rows for {document.get('relativePath')}")


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
