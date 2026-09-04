"""Certify deterministic physical transcription against independent anchors."""

from __future__ import annotations

import json
import re
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_physical_certification import PhysicalAnchor, physical_anchors

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
MANIFEST_PATH = CORPUS / "source-manifest.json"
SOURCE_MAP_ROOT = CORPUS / "semantic-v1"
TRANSCRIPTION_ROOT = CORPUS / "physical-transcription"
OUTPUT_ROOT = CORPUS / "physical-certification-v2"
DMS_HASH = "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"


def main() -> int:
    manifest = read_json(MANIFEST_PATH)
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    results = []
    for document in manifest.get("documents", []):
        source_hash = str(document["sha256"])
        source_map = read_json(SOURCE_MAP_ROOT / f"{source_hash}.json")
        transcription = read_json(TRANSCRIPTION_ROOT / f"{source_hash}.json")
        result = certify_document(document, source_map, transcription)
        results.append(result)
        write_json(OUTPUT_ROOT / f"{source_hash}.json", result)
    certified = [item for item in results if item["passed"]]
    report = {
        "schemaVersion": "advertified.inventory-physical-certification.v2",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "sourceCount": len(results),
        "certifiedCount": len(certified),
        "failedCount": len(results) - len(certified),
        "all43Certified": len(results) == 43 and len(certified) == 43,
        "physicalAnchorCount": sum(item["physicalAnchorCount"] for item in results),
        "matchedPhysicalAnchorCount": sum(item["matchedPhysicalAnchorCount"] for item in results),
        "transcriptionRowCount": sum(item["transcriptionRowCount"] for item in results),
        "blockerCounts": blocker_counts(results),
        "documents": results,
    }
    write_json(OUTPUT_ROOT / "corpus.json", report)
    (OUTPUT_ROOT / "CORPUS.md").write_text(render_markdown(report), encoding="utf-8")
    write_status(report)
    print(json.dumps({
        "sourceCount": report["sourceCount"],
        "certifiedCount": report["certifiedCount"],
        "failedCount": report["failedCount"],
        "physicalAnchorCount": report["physicalAnchorCount"],
        "matchedPhysicalAnchorCount": report["matchedPhysicalAnchorCount"],
        "transcriptionRowCount": report["transcriptionRowCount"],
        "blockerCounts": report["blockerCounts"],
        "output": str((OUTPUT_ROOT / "corpus.json").relative_to(ROOT)),
    }, indent=2))
    return 0 if report["all43Certified"] else 2


def certify_document(
    document: dict[str, Any],
    source_map: dict[str, Any],
    transcription: dict[str, Any],
) -> dict[str, Any]:
    source_hash = str(document["sha256"])
    file_name = str(document["relativePath"])
    anchors = physical_anchors(source_map)
    rows = transcription.get("rows") or []
    unmatched = [anchor for anchor in anchors if not anchor_match(anchor, rows)]
    row_failures = [failure for row in rows for failure in row_failures_for(row)]
    duplicate_count = duplicates(rows)
    blockers: list[str] = []
    structural_failures = source_map_failures(document, source_map)
    if structural_failures:
        blockers.append("PHYSICAL_SOURCE_MAP_INVALID")
    if not anchors:
        blockers.append("NO_PHYSICAL_INVENTORY_ANCHORS")
    if unmatched:
        blockers.append("PHYSICAL_ANCHORS_UNMATCHED")
    if not rows:
        blockers.append("NO_TRANSCRIPTION_ROWS")
    if row_failures:
        blockers.append("INVALID_TRANSCRIPTION_ROWS")
    if duplicate_count:
        blockers.append("DUPLICATE_TRANSCRIPTION_ROWS")
    if transcription.get("sourceHash") != source_hash:
        blockers.append("TRANSCRIPTION_SOURCE_HASH_MISMATCH")
    if transcription.get("fileName") != file_name:
        blockers.append("TRANSCRIPTION_FILE_NAME_MISMATCH")
    if source_hash == DMS_HASH and not dms_gold_passed():
        blockers.append("DMS_HUMAN_GOLD_NOT_PASSED")
    return {
        "sourceHash": source_hash,
        "fileName": file_name,
        "documentClass": source_map.get("format"),
        "physicalSourceMapSha256": sha256_json(source_map),
        "physicalAnchorCount": len(anchors),
        "matchedPhysicalAnchorCount": len(anchors) - len(unmatched),
        "transcriptionRowCount": len(rows),
        "duplicateTranscriptionRowCount": duplicate_count,
        "invalidTranscriptionRowCount": len(row_failures),
        "sourceMapFailures": structural_failures,
        "rowFailures": row_failures[:200],
        "unmatchedPhysicalAnchors": [anchor_dict(item) for item in unmatched[:200]],
        "passed": not blockers,
        "blockers": blockers,
    }


def source_map_failures(document: dict[str, Any], source_map: dict[str, Any]) -> list[str]:
    failures = []
    source_hash = str(document["sha256"])
    if source_map.get("sourceHash") != source_hash:
        failures.append("SOURCE_HASH_MISMATCH")
    if source_map.get("relativePath") != document.get("relativePath"):
        failures.append("RELATIVE_PATH_MISMATCH")
    counts = source_map.get("counts") or {}
    for field, key in (("fragments", "fragments"), ("tables", "tables"), ("assets", "assets")):
        values = source_map.get(field) or []
        if int(counts.get(key) or 0) != len(values):
            failures.append(f"COUNT_MISMATCH:{field}")
    if not any(source_map.get(field) for field in ("fragments", "tables", "assets")):
        failures.append("EMPTY_SOURCE_MAP")
    for item in source_map.get("fragments") or []:
        if not item.get("locator") or not str(item.get("text") or "").strip():
            failures.append("INVALID_FRAGMENT")
            break
    for table in source_map.get("tables") or []:
        if not table.get("locator") or not table.get("rows"):
            failures.append("INVALID_TABLE")
            break
    for asset in source_map.get("assets") or []:
        if not asset.get("locator") or int(asset.get("bytes") or 0) <= 0:
            failures.append("INVALID_ASSET")
            break
    if not bool(document.get("physicalIdentityVerified", True)):
        failures.append("PHYSICAL_IDENTITY_NOT_VERIFIED")
    return failures


def row_failures_for(row: dict[str, Any]) -> list[dict[str, Any]]:
    failures = []
    required = ("physicalRowId", "productCode", "sourceHash", "sourceLocator", "supplierName", "channelHint", "identityRaw", "rateRaw", "evidence")
    for field in required:
        if row.get(field) in (None, "", []):
            failures.append({"row": row.get("physicalRowId"), "failure": f"MISSING:{field}"})
    if row.get("supplierName") == "UNKNOWN_SUPPLIER":
        failures.append({"row": row.get("physicalRowId"), "failure": "UNKNOWN_SUPPLIER"})
    if row.get("channelHint") == "UNKNOWN_CHANNEL":
        failures.append({"row": row.get("physicalRowId"), "failure": "UNKNOWN_CHANNEL"})
    if row.get("rateAmountMinor") is None and not explicit_unknown_or_ambiguous(row):
        failures.append({"row": row.get("physicalRowId"), "failure": "UNRESOLVED_RATE_WITHOUT_FLAG"})
    for evidence in row.get("evidence") or []:
        if not evidence.get("sourceLocator") or evidence.get("raw") in (None, ""):
            failures.append({"row": row.get("physicalRowId"), "failure": "INVALID_EVIDENCE"})
            break
    return failures


def anchor_match(anchor: PhysicalAnchor, rows: list[dict[str, Any]]) -> bool:
    pool = [row for row in rows if anchor.ordinal is None or row.get("sourceOrdinal") is None or row.get("sourceOrdinal") == anchor.ordinal]
    if not pool:
        pool = rows
    raw = normalize(anchor.raw)
    if anchor.kind == "RADIO_RATE":
        return any(
            anchor.amount_minor is not None
            and row.get("rateAmountMinor") == anchor.amount_minor
            and anchor.daypart and normalize(anchor.daypart) in row_search(row)
            and anchor.day_group and normalize(anchor.day_group) in row_search(row)
            for row in pool
        )
    if anchor.kind in {"RATE", "RATE_UNKNOWN"}:
        if anchor.amount_minor is not None and any(row.get("rateAmountMinor") == anchor.amount_minor for row in pool):
            return True
        return any(raw and raw in row_search(row) for row in pool)
    return any(raw and raw in row_search(row) for row in pool)


def row_search(row: dict[str, Any]) -> str:
    return normalize(json.dumps(row, sort_keys=True, default=str))


def explicit_unknown_or_ambiguous(row: dict[str, Any]) -> bool:
    raw = normalize(str(row.get("rateRaw") or ""))
    return any(value in raw for value in ("rateonrequest", "tbc", "poa")) or bool(row.get("ambiguityCodes"))


def duplicates(rows: list[dict[str, Any]]) -> int:
    signatures = Counter((row.get("sourceOrdinal"), normalize(str(row.get("identityRaw") or "")), normalize(str(row.get("rateRaw") or "")), row.get("rateAmountMinor")) for row in rows)
    return sum(count - 1 for count in signatures.values() if count > 1)


def dms_gold_passed() -> bool:
    for name in (f"{DMS_HASH}.local-evaluation.json", f"{DMS_HASH}.evaluation.json"):
        path = CORPUS / "certification" / name
        if path.exists():
            value = read_json(path)
            return bool(value.get("passed")) and value.get("documentId") == DMS_HASH
    return False


def anchor_dict(anchor: PhysicalAnchor) -> dict[str, Any]:
    return {
        "kind": anchor.kind,
        "raw": anchor.raw,
        "sourceLocator": anchor.locator,
        "sourceOrdinal": anchor.ordinal,
        "amountMinor": anchor.amount_minor,
        "daypart": anchor.daypart,
        "dayGroup": anchor.day_group,
        "context": anchor.context,
    }


def blocker_counts(results: list[dict[str, Any]]) -> dict[str, int]:
    counts: Counter[str] = Counter()
    for item in results:
        counts.update(item.get("blockers") or [])
    return dict(sorted(counts.items()))


def write_status(report: dict[str, Any]) -> None:
    status = OUTPUT_ROOT / "status"
    status.mkdir(parents=True, exist_ok=True)
    for item in status.glob("STATUS_*.txt"):
        item.unlink()
    name = (
        f"STATUS_{report['certifiedCount']}_OF_{report['sourceCount']}"
        f"__ANCHORS_{report['matchedPhysicalAnchorCount']}_OF_{report['physicalAnchorCount']}"
        f"__ROWS_{report['transcriptionRowCount']}.txt"
    )
    (status / name).write_text(json.dumps(report["blockerCounts"], sort_keys=True) + "\n", encoding="utf-8")
    marker = OUTPUT_ROOT / "ALL_43_PHYSICALLY_CERTIFIED.json"
    if report["all43Certified"]:
        write_json(marker, {
            "all43PhysicallyCertified": True,
            "sourceCount": 43,
            "physicalAnchorCount": report["physicalAnchorCount"],
            "matchedPhysicalAnchorCount": report["matchedPhysicalAnchorCount"],
            "transcriptionRowCount": report["transcriptionRowCount"],
        })
    elif marker.exists():
        marker.unlink()


def render_markdown(report: dict[str, Any]) -> str:
    rows = [
        "# Inventory physical transcription certification", "",
        f"Generated: `{report['generatedAtUtc']}`", "",
        f"**Result: {'PASS' if report['all43Certified'] else 'FAIL'}**", "",
        "| Measure | Result |", "|---|---:|",
        f"| Sources | {report['sourceCount']} |",
        f"| Certified | {report['certifiedCount']} |",
        f"| Failed | {report['failedCount']} |",
        f"| Physical anchors | {report['physicalAnchorCount']} |",
        f"| Matched anchors | {report['matchedPhysicalAnchorCount']} |",
        f"| Physical transcription rows | {report['transcriptionRowCount']} |", "",
        "## Files", "",
        "| File | Anchors | Matched | Rows | Result | Blockers |", "|---|---:|---:|---:|---|---|",
    ]
    for item in report["documents"]:
        rows.append(
            "| " + str(item["fileName"]).replace("|", "\\|")
            + f" | {item['physicalAnchorCount']} | {item['matchedPhysicalAnchorCount']}"
            + f" | {item['transcriptionRowCount']} | {'PASS' if item['passed'] else 'FAIL'}"
            + " | " + ", ".join(item["blockers"]) + " |"
        )
    return "\n".join(rows) + "\n"


def sha256_json(value: dict[str, Any]) -> str:
    import hashlib
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


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
