"""Shared model and normalisation helpers for physical inventory evidence."""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from typing import Any

MONEY = re.compile(
    r"(?<![A-Za-z])(?:ZAR|R)\s*\d[\d\s.,\u00a0]*",
    re.IGNORECASE,
)
CODE = re.compile(r"\b[A-Z]{1,6}[ -]?\d{2,7}[A-Z]?\b")
TIME = re.compile(r"^\s*\d{1,2}:\d{2}\s*[-–]\s*\d{1,2}:\d{2}\s*$")
DIMENSION = re.compile(
    r"\b\d+(?:[.,]\d+)?\s*m?\s*[x×]\s*\d+(?:[.,]\d+)?\s*m?\b",
    re.IGNORECASE,
)
RATE_LABELS = {
    "rate", "rate card", "discounted rate", "net rate", "net rates",
    "cost", "package cost", "base price", "base_price", "price", "cpm",
}
IDENTITY_LABELS = {
    "name", "product", "product name", "platform", "ad unit", "site",
    "site number", "site code", "code", "placement", "description",
    "format", "element", "programme", "program", "show", "daypart",
    "time band", "duration", "medium type", "medium_type",
}
VERTICAL_LABELS = {
    "description", "area", "city prov", "city province", "traffic count",
    "impacts", "frequency", "type", "format", "drivers side", "gps",
    "rate card", "discounted rate", "printing", "flighting", "target mall",
    "site number", "size", "production", "availability", "site info",
}
NON_SELLABLE = re.compile(
    r"\b(?:vat|terms?|conditions?|contact|telephone|phone|fax|email|copyright|"
    r"commission|penalty|deposit|cancellation|late fee|minimum spend)\b",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class PhysicalUnit:
    key: str
    locator: str
    scope: str
    kind: str
    identity: str
    raw_rate: str | None
    evidence: tuple[str, ...]


@dataclass(frozen=True)
class PhysicalDocument:
    source_hash: str
    file_name: str
    document_format: str
    locators: frozenset[str]
    raw_text: str
    normalized_text: str
    units: tuple[PhysicalUnit, ...]
    pages_or_slides: tuple[str, ...]


def table_rows(table: dict[str, Any]) -> list[list[str]]:
    result: list[list[str]] = []
    for row in table.get("rows") or []:
        values: list[str] = []
        for item in row:
            if isinstance(item, dict):
                value = item.get("value")
                if value is None:
                    value = item.get("cachedValue")
            else:
                value = item
            values.append(str(value).strip() if value is not None else "")
        if any(values):
            result.append(values)
    return result


def group_by_scope(items: list[dict[str, Any]]) -> dict[str, list[dict[str, Any]]]:
    result: dict[str, list[dict[str, Any]]] = {}
    for item in items:
        scope = locator_scope(str(item.get("locator") or ""))
        if scope:
            result.setdefault(scope, []).append(item)
    return result


def locator_scope(locator: str) -> str:
    for pattern in (
        r"(?:docling|pdf):page=(\d+)",
        r"pptx:slide=(\d+)",
    ):
        match = re.search(pattern, locator, re.IGNORECASE)
        if match:
            return f"ordinal:{int(match.group(1))}"
    match = re.search(r"xlsx:sheet=([^;]+)", locator, re.IGNORECASE)
    if not match:
        return ""
    sheet = match.group(1).split(":", 1)[-1]
    return f"sheet:{normalize(sheet)}"


def normalize(value: str) -> str:
    value = unicodedata.normalize("NFKC", value or "").casefold()
    value = re.sub(r"[^a-z0-9]+", " ", value)
    return " ".join(value.split())


def normalize_label(value: str) -> str:
    return normalize(value).replace(" city prov ", " city province ").strip()


def money_fingerprint(value: str | None) -> str:
    if not value:
        return ""
    compact = re.sub(r"\s+", "", value.upper().replace("ZAR", "R"))
    return compact.rstrip(".,")


def extract_money(value: str) -> str | None:
    match = MONEY.search(value or "")
    return match.group(0).strip().rstrip(".,") if match else None


def numeric_rate(value: str) -> bool:
    compact = (value or "").replace(" ", "").replace("\u00a0", "")
    return bool(re.fullmatch(r"\d[\d.,]*", compact))


def meaningful_identity(value: str) -> bool:
    normalized = normalize(value)
    if len(normalized) < 3 or normalized.isdigit():
        return False
    if (
        normalized in RATE_LABELS
        or normalized in VERTICAL_LABELS
        or normalized in {
            "multiply by",
            "multiplier",
            "loading factor",
            "duration multiplier",
        }
    ):
        return False
    return not NON_SELLABLE.fullmatch(value.strip())


def first_value(values: dict[str, str], *keys: str) -> str:
    return next((values[key] for key in keys if values.get(key)), "")


def cell(row: list[str], index: int) -> str:
    return row[index].strip() if index < len(row) else ""


def deduplicate_units(units: list[PhysicalUnit]) -> list[PhysicalUnit]:
    result: list[PhysicalUnit] = []
    seen: set[tuple[str, str, str]] = set()
    for unit in units:
        normalized_identity = normalize(unit.identity)
        explicit_code = CODE.search(unit.identity)
        is_workbook_row = unit.locator.lower().startswith("xlsx:")
        identity_key = (
            normalized_identity
            if is_workbook_row or explicit_code is None
            else normalize(explicit_code.group(0))
        )
        scope_key = (
            unit.scope
            if is_workbook_row or explicit_code is None
            else ""
        )
        signature = (
            scope_key,
            identity_key,
            money_fingerprint(unit.raw_rate),
        )
        if signature in seen:
            continue
        seen.add(signature)
        result.append(unit)
    return result
