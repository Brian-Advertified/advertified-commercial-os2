"""Build a concise, deterministic profile of every physical corpus source map.

The source maps are generated from the immutable physical files. This utility is
read-only and never calls a paid provider.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any, Iterable


MONEY = re.compile(r"(?<![A-Za-z])(?:ZAR|R)\s*\d[\d\s.,\u00a0]*", re.I)
CODE = re.compile(r"\b[A-Z]{1,8}[- ]?\d{2,8}[A-Z]{0,4}\b", re.I)
TIME = re.compile(r"\b\d{1,2}:\d{2}\s*[-–]\s*\d{1,2}:\d{2}\b")
KEY_VALUE_LABELS = {
    "description", "area", "cityprov", "cityprovince", "trafficcount",
    "impacts", "frequency", "type", "format", "driversside", "gps",
    "gpscoordinate", "ratecard", "discountedrate", "printing", "flighting",
    "targetmall", "ara", "notes", "sitenumber", "size", "illuminated",
    "production", "audiencereach", "audience", "availability", "siteinfo",
}
HEADER_LABELS = {
    "name", "product", "productname", "platform", "adunit", "format",
    "rate", "rates", "price", "cost", "baseprice", "mediumtype", "media",
    "channel", "site", "sitenumber", "sitecode", "code", "location",
    "address", "city", "province", "country", "geography", "duration",
    "timeband", "netrates", "currency", "rateperiod", "description",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, required=True)
    root = parser.parse_args().evidence.resolve(strict=True)
    manifest = read_json(root / "source-manifest.json")
    documents = []
    for item in manifest.get("documents", []):
        source_hash = str(item["sha256"])
        source_map_path = root / "semantic-v1" / f"{source_hash}.json"
        profile = profile_source(item, read_json(source_map_path))
        documents.append(profile)
    result = {
        "schemaVersion": "advertified.inventory-physical-source-profile.v1",
        "datasetVersion": manifest.get("datasetVersion"),
        "sourceCount": len(documents),
        "formatCounts": dict(Counter(item["format"] for item in documents)),
        "totals": {
            "fragments": sum(item["fragmentCount"] for item in documents),
            "tables": sum(item["tableCount"] for item in documents),
            "assets": sum(item["assetCount"] for item in documents),
            "moneyMentions": sum(item["moneyMentionCount"] for item in documents),
            "productCodeMentions": sum(item["productCodeMentionCount"] for item in documents),
            "timeBandMentions": sum(item["timeBandMentionCount"] for item in documents),
            "verticalKeyValueTables": sum(item["verticalKeyValueTableCount"] for item in documents),
            "commercialTables": sum(item["commercialTableCount"] for item in documents),
        },
        "documents": documents,
    }
    output = root / "physical-source-profile.json"
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "sourceCount": result["sourceCount"],
        "formatCounts": result["formatCounts"],
        "totals": result["totals"],
        "output": str(output),
    }, indent=2))
    return 0 if documents else 2


def profile_source(manifest: dict[str, Any], source: dict[str, Any]) -> dict[str, Any]:
    fragments = source.get("fragments") or []
    tables = source.get("tables") or []
    assets = source.get("assets") or []
    fragment_texts = [str(item.get("text") or "") for item in fragments]
    table_rows = [row for table in tables for row in normalized_rows(table)]
    table_texts = [" | ".join(row) for row in table_rows]
    texts = fragment_texts + table_texts
    money = unique_matches(MONEY, texts)
    codes = unique_matches(CODE, texts)
    times = unique_matches(TIME, texts)
    key_value_tables = [table for table in tables if is_key_value_table(table)]
    commercial_tables = [table for table in tables if is_commercial_table(table)]
    pages = sorted({ordinal(item) for item in fragments if ordinal(item) is not None})
    return {
        "relativePath": manifest.get("relativePath"),
        "sha256": manifest.get("sha256"),
        "format": source.get("format") or manifest.get("documentClass"),
        "physicalIdentityVerified": bool(manifest.get("physicalIdentityVerified", True)),
        "fragmentCount": len(fragments),
        "tableCount": len(tables),
        "assetCount": len(assets),
        "pageOrSlideCountObserved": len(pages),
        "moneyMentionCount": len(money),
        "productCodeMentionCount": len(codes),
        "timeBandMentionCount": len(times),
        "verticalKeyValueTableCount": len(key_value_tables),
        "commercialTableCount": len(commercial_tables),
        "moneySamples": money[:12],
        "productCodeSamples": codes[:12],
        "tableShapes": [table_shape(table) for table in tables[:12]],
        "likelyExtractionClasses": extraction_classes(
            source, money, codes, times, key_value_tables, commercial_tables
        ),
    }


def extraction_classes(
    source: dict[str, Any],
    money: list[str],
    codes: list[str],
    times: list[str],
    key_value: list[dict[str, Any]],
    commercial: list[dict[str, Any]],
) -> list[str]:
    classes: list[str] = []
    fmt = str(source.get("format") or "").upper()
    if fmt == "XLSX":
        classes.append("SPREADSHEET_ROWS")
    if key_value:
        classes.append("VERTICAL_KEY_VALUE_SITE_CARDS")
    if times:
        classes.append("DAYPART_RATE_MATRIX")
    if commercial:
        classes.append("HEADERED_OR_HEADERLESS_COMMERCIAL_TABLE")
    if codes:
        classes.append("PRODUCT_OR_SITE_CODE_CARDS")
    if money:
        classes.append("PRICED_TEXT_OR_TABLE_OFFERS")
    if fmt == "PPTX" and not money:
        classes.append("UNPRICED_PRESENTATION_INVENTORY")
    if not classes:
        classes.append("VISUAL_OR_NARRATIVE_REVIEW")
    return classes


def is_key_value_table(table: dict[str, Any]) -> bool:
    rows = normalized_rows(table)
    if len(rows) < 4:
        return False
    pairs = [row for row in rows if len(row) >= 2 and row[0].strip()]
    if len(pairs) < 4:
        return False
    labels = {normalize(row[0]) for row in pairs}
    return len(labels & KEY_VALUE_LABELS) >= 4 and len(pairs) / len(rows) >= 0.6


def is_commercial_table(table: dict[str, Any]) -> bool:
    rows = normalized_rows(table)
    if not rows:
        return False
    first = rows[:6]
    header_score = max(
        (len({normalize(value) for value in row} & HEADER_LABELS) for row in first),
        default=0,
    )
    money_rows = sum(bool(MONEY.search(" | ".join(row))) for row in rows)
    numeric_rows = sum(any(any(ch.isdigit() for ch in value) for value in row) for row in rows)
    return header_score >= 1 and (money_rows > 0 or numeric_rows >= 2)


def normalized_rows(table: dict[str, Any]) -> list[list[str]]:
    result: list[list[str]] = []
    for row in table.get("rows") or []:
        values: list[str] = []
        for cell in row:
            if isinstance(cell, dict):
                value = cell.get("value")
                if value is None:
                    value = cell.get("cachedValue")
            else:
                value = cell
            values.append(str(value or "").strip())
        if any(values):
            result.append(values)
    return result


def table_shape(table: dict[str, Any]) -> dict[str, Any]:
    rows = normalized_rows(table)
    return {
        "locator": table.get("locator"),
        "rows": len(rows),
        "columns": max((len(row) for row in rows), default=0),
        "firstRows": rows[:3],
    }


def unique_matches(pattern: re.Pattern[str], texts: Iterable[str]) -> list[str]:
    values: list[str] = []
    seen: set[str] = set()
    for text in texts:
        for match in pattern.finditer(text):
            value = " ".join(match.group(0).split()).strip(" .,;:|")
            key = value.lower()
            if value and key not in seen:
                seen.add(key)
                values.append(value)
    return values


def normalize(value: str) -> str:
    return "".join(ch.lower() for ch in value if ch.isalnum())


def ordinal(item: dict[str, Any]) -> int | None:
    value = item.get("ordinal")
    if value is None:
        value = item.get("page")
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
