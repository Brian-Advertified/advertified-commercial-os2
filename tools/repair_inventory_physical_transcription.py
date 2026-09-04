"""Repair physical transcriptions from independent audit findings.

Only exact unmatched source anchors already found by the independent auditor are
added. No commercial value is inferred or altered.
"""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from typing import Any

from inventory_physical_transcriber import channel_for, parse_amount_minor, supplier_for

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
MANIFEST = CORPUS / "source-manifest.json"
TRANSCRIPTION_ROOT = CORPUS / "physical-transcription"
AUDIT_ROOT = CORPUS / "physical-certification-v2"


def main() -> int:
    manifest = read_json(MANIFEST)
    repaired_files = 0
    added_rows = 0
    for document in manifest.get("documents", []):
        source_hash = str(document["sha256"])
        audit_path = AUDIT_ROOT / f"{source_hash}.json"
        if not audit_path.exists():
            continue
        audit = read_json(audit_path)
        unmatched = audit.get("unmatchedPhysicalAnchors") or []
        if not unmatched:
            continue
        path = TRANSCRIPTION_ROOT / f"{source_hash}.json"
        transcription = read_json(path)
        rows = transcription.get("rows") or []
        before = len(rows)
        for anchor in unmatched:
            row = anchor_row(document, anchor)
            if row is not None and not already_present(row, rows):
                rows.append(row)
        rows.sort(key=lambda item: (
            int(item.get("sourceOrdinal") or 0),
            str(item.get("sourceLocator") or ""),
            str(item.get("identityRaw") or ""),
        ))
        transcription["rows"] = rows
        transcription["rowCount"] = len(rows)
        transcription["auditRepair"] = {
            "source": "independent-physical-anchor-audit",
            "addedAtomicFacts": len(rows) - before,
            "commercialInference": False,
        }
        write_json(path, transcription)
        if len(rows) > before:
            repaired_files += 1
            added_rows += len(rows) - before
    print(json.dumps({
        "repairedFiles": repaired_files,
        "addedAtomicFactRows": added_rows,
        "paidProviderCalls": 0,
    }, indent=2))
    return 0


def anchor_row(document: dict[str, Any], anchor: dict[str, Any]) -> dict[str, Any] | None:
    raw = clean(str(anchor.get("raw") or ""))
    locator = str(anchor.get("sourceLocator") or "")
    kind = str(anchor.get("kind") or "")
    if not raw or not locator:
        return None
    source_hash = str(document["sha256"])
    file_name = str(document["relativePath"])
    ordinal = anchor.get("sourceOrdinal")
    amount = anchor.get("amountMinor")
    rate_raw = raw if kind in {"RATE", "RATE_UNKNOWN", "RADIO_RATE"} else "RATE_ON_REQUEST"
    if kind == "RADIO_RATE":
        identity = " - ".join(value for value in (
            str(anchor.get("dayGroup") or "").strip(),
            str(anchor.get("daypart") or "").strip(),
        ) if value) or fallback_identity(anchor, ordinal)
    elif kind in {"SITE_CODE", "OFFER_IDENTITY", "UNPRICED_LOCATION"}:
        identity = raw
    else:
        identity = context_identity(str(anchor.get("context") or ""), raw, ordinal)
    material = f"{source_hash}\n{locator}\n{identity}\n{rate_raw}\naudit-fact"
    row_hash = hashlib.sha256(material.encode()).hexdigest()
    ambiguity = []
    if kind == "RATE" and amount is None and parse_amount_minor(rate_raw, implicit_zar=False) is None:
        ambiguity.append("AMBIGUOUS_RATE")
    return {
        "physicalRowId": row_hash,
        "productCode": "ADV-" + row_hash[:16].upper(),
        "sourceHash": source_hash,
        "sourceLocator": locator,
        "sourceOrdinal": ordinal,
        "supplierName": supplier_for(file_name),
        "channelHint": channel_for(file_name),
        "identityRaw": identity,
        "geographyRaw": raw if kind == "UNPRICED_LOCATION" else None,
        "descriptionRaw": clean_optional(str(anchor.get("context") or "")),
        "placementRaw": "Radio spot" if kind == "RADIO_RATE" else None,
        "formatRaw": None,
        "dimensionsRaw": None,
        "daypartRaw": clean_optional(str(anchor.get("daypart") or "")),
        "latitudeRaw": None,
        "longitudeRaw": None,
        "rateRaw": rate_raw,
        "rateAmountMinor": amount if amount is not None else parse_amount_minor(rate_raw, implicit_zar=kind == "RADIO_RATE"),
        "currency": "ZAR" if amount is not None or re.search(r"(?i)(?:ZAR|R)", rate_raw) else None,
        "buyingBasisRaw": None,
        "availabilityRaw": None,
        "ambiguityCodes": ambiguity,
        "evidence": [{
            "field": kind,
            "raw": raw,
            "sourceLocator": locator,
        }],
        "sourceExtras": {
            "atomicPhysicalFact": True,
            "anchorKind": kind,
            "dayGroup": anchor.get("dayGroup"),
        },
    }


def context_identity(context: str, raw_rate: str, ordinal: Any) -> str:
    value = context.replace(raw_rate, " ")
    lines = [clean(line) for line in value.splitlines() if clean(line)]
    candidates = [line for line in lines if any(char.isalpha() for char in line) and len(line) <= 240]
    return candidates[-1] if candidates else fallback_identity({}, ordinal)


def fallback_identity(anchor: dict[str, Any], ordinal: Any) -> str:
    kind = str(anchor.get("kind") or "Physical offer").replace("_", " ").title()
    return f"{kind} - source page or slide {ordinal or 'unknown'}"


def already_present(row: dict[str, Any], rows: list[dict[str, Any]]) -> bool:
    raw = normalize(str(row.get("evidence", [{}])[0].get("raw") or ""))
    ordinal = row.get("sourceOrdinal")
    amount = row.get("rateAmountMinor")
    for current in rows:
        if ordinal is not None and current.get("sourceOrdinal") not in (None, ordinal):
            continue
        if amount is not None and current.get("rateAmountMinor") == amount:
            return True
        searchable = normalize(json.dumps(current, sort_keys=True, default=str))
        if raw and raw in searchable:
            return True
    return False


def clean(value: str) -> str:
    return " ".join(value.strip(" :#-\t").split())


def clean_optional(value: str) -> str | None:
    result = clean(value)
    return result or None


def normalize(value: str) -> str:
    return "".join(char.lower() for char in value if char.isalnum())


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
