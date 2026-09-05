"""Physical-source primitives used by independent inventory certification."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any

MONEY_PATTERN = re.compile(
    r"(?<![A-Za-z])(?:ZAR|R)\s*R?\s*\d[\d\s.,\u00a0]*",
    re.IGNORECASE,
)
PRODUCT_CODE_PATTERN = re.compile(
    r"(?<![A-Za-z0-9])[A-Z]{1,6}\s*[-/]?\s*\d{2,6}[A-Z]{0,4}"
    r"(?![A-Za-z0-9])",
)
TIME_BAND_PATTERN = re.compile(
    r"^\s*\d{1,2}:\d{2}\s*[-–—]\s*\d{1,2}:\d{2}\s*$"
)
DIMENSION_PATTERN = re.compile(
    r"\b\d+(?:[.,]\d+)?\s*m?\s*[x×]\s*\d+(?:[.,]\d+)?\s*m?\b",
    re.IGNORECASE,
)
YEAR_PATTERN = re.compile(r"^(?:19|20)\d{2}$")
NON_PRIMARY_RATE_LABELS = (
    "printing", "flighting", "production", "installation",
    "set up fee", "setup fee", "management fee", "discount",
    "vat", "traffic count", "impacts", "impressions",
    "listenership", "audience reach", "investment summary",
    "total value", "total investment", "total invoice", "subtotal",
    "savings",
)


@dataclass(frozen=True)
class SourceText:
    locator: str
    ordinal: int
    text: str
    confidence: float | None


@dataclass(frozen=True)
class SourceTable:
    locator: str
    ordinal: int
    rows: tuple[tuple[str, ...], ...]


@dataclass(frozen=True)
class PhysicalAnchor:
    anchor_type: str
    locator: str
    ordinal: int
    identity: str | None
    product_code: str | None
    raw_rate: str | None
    currency: str | None
    context: tuple[str, ...]


@dataclass(frozen=True)
class PhysicalSource:
    source_hash: str
    relative_path: str
    document_format: str
    texts: tuple[SourceText, ...]
    tables: tuple[SourceTable, ...]
    asset_count: int

    @property
    def searchable_text(self) -> str:
        values = [item.text for item in self.texts]
        values.extend(
            cell
            for table in self.tables
            for row in table.rows
            for cell in row
        )
        return "\n".join(values)


def load_source(path: Path) -> PhysicalSource:
    payload = json.loads(path.read_text(encoding="utf-8"))
    texts = source_texts(payload)
    tables = tuple(
        SourceTable(
            locator=str(item.get("locator") or ""),
            ordinal=int(
                item.get("page") or item.get("slide")
                or table_ordinal(item)
            ),
            rows=tuple(
                tuple(cell_text(cell) for cell in row)
                for row in item.get("rows") or []
            ),
        )
        for item in payload.get("tables") or []
    )
    return PhysicalSource(
        source_hash=str(payload["sourceHash"]),
        relative_path=str(payload["relativePath"]),
        document_format=str(payload.get("format") or ""),
        texts=texts,
        tables=tables,
        asset_count=len(payload.get("assets") or []),
    )


def source_texts(payload: dict[str, Any]) -> tuple[SourceText, ...]:
    result: list[SourceText] = []
    seen: set[tuple[str, str]] = set()
    for item in payload.get("fragments") or []:
        text = str(item.get("text") or "").strip()
        locator = str(item.get("locator") or "")
        if not text or (locator, text) in seen:
            continue
        seen.add((locator, text))
        result.append(SourceText(
            locator=locator,
            ordinal=int(
                item.get("ordinal") or item.get("page")
                or item.get("slide") or 0
            ),
            text=text,
            confidence=as_float(item.get("confidence")),
        ))
    for item in payload.get("assets") or []:
        text = str(item.get("ocrText") or "").strip()
        locator = str(item.get("locator") or "")
        if not text or (locator, text) in seen:
            continue
        seen.add((locator, text))
        result.append(SourceText(
            locator=locator,
            ordinal=int(
                item.get("page") or item.get("slide")
                or table_ordinal(item)
            ),
            text=text,
            confidence=as_float(item.get("ocrConfidence")),
        ))
    return tuple(result)


def all_source_values(source: PhysicalSource) -> tuple[str, ...]:
    return tuple(
        value
        for value in [item.text for item in source.texts]
        + [
            cell
            for table in source.tables
            for row in table.rows
            for cell in row
        ]
        if value
    )


def product_codes(text: str) -> tuple[str, ...]:
    result: list[str] = []
    for match in PRODUCT_CODE_PATTERN.finditer(text.upper()):
        value = clean_product_code(match.group(0))
        if not value or YEAR_PATTERN.match(value.replace(" ", "")):
            continue
        prefix = "".join(character for character in value if character.isalpha())
        if prefix in {
            "R", "ZAR", "FM", "TV", "CPM", "LSM", "TEL", "FAX",
            "SEC", "MIN", "HR", "HRS", "ISO", "VAT", "MALE", "FEMALE",
            "ON", "WIDE", "EVERY", "ROLL", "PRE", "POST", "PER", "UP",
            "MAX", "MINIMUM", "MAXIMUM", "DAY", "DAYS", "MONTH",
            "MONTHS", "YEAR", "YEARS", "PAGE", "PAGES", "RATE", "COST",
            "PLAN", "OPTION", "PACKAGE", "ADULTS", "AGE", "TARGET",
            "REACH", "VIEW", "VIEWS", "AUDIENCE", "TOTAL", "SPOT",
            "SPOTS", "VIDEO", "AUDIO", "FORMAT", "SIZE", "SECONDS",
            "MINUTES",
        }:
            continue
        if DIMENSION_PATTERN.fullmatch(value):
            continue
        digits = "".join(character for character in value if character.isdigit())
        letters = "".join(character for character in value if character.isalpha())
        if not letters or len(digits) < 2:
            continue
        if value not in result:
            result.append(value)
    return tuple(result)


def first_product_code(text: str) -> str | None:
    values = product_codes(text)
    return values[0] if values else None


def clean_product_code(value: str | None) -> str | None:
    if not value:
        return None
    return " ".join(value.upper().replace("/", " ").split()).strip(" -") or None


def clean_money(value: str | None) -> str | None:
    if not value:
        return None
    match = MONEY_PATTERN.search(value)
    if not match:
        return None
    return " ".join(
        match.group(0).replace("\u00a0", " ").split()
    ).rstrip(".,")


@lru_cache(maxsize=16_384)
def normalize_money(value: str) -> str:
    return "".join(
        character for character in value.upper() if character.isalnum()
    )


@lru_cache(maxsize=16_384)
def normalize_compact(value: str) -> str:
    return "".join(
        character.lower() for character in value if character.isalnum()
    )


def normalize_header(value: str) -> str:
    return normalize_compact(value)


def normalize_currency(value: str | None) -> str | None:
    return (
        "ZAR"
        if value and re.search(r"\bZAR\b|(?<![A-Za-z])R", value, re.I)
        else None
    )


def cell_text(cell: Any) -> str:
    if isinstance(cell, dict):
        value = cell.get("value")
        if value is None:
            value = cell.get("cachedValue")
        return "" if value is None else str(value).strip()
    return "" if cell is None else str(cell).strip()


def table_ordinal(item: dict[str, Any]) -> int:
    match = re.search(
        r"(?:page|slide)=(\d+)", str(item.get("locator") or "")
    )
    return int(match.group(1)) if match else 0


def as_float(value: Any) -> float | None:
    try:
        return float(value) if value is not None else None
    except (TypeError, ValueError):
        return None


def first_index(headers: list[str], names: tuple[str, ...]) -> int | None:
    for name in names:
        if name in headers:
            return headers.index(name)
    return None


def value_at(values: tuple[str, ...], index: int | None) -> str:
    return (
        values[index].strip()
        if index is not None and index < len(values)
        else ""
    )


def last_meaningful_line(value: str) -> str | None:
    lines = [
        line.strip(" :-–—|\t")
        for line in value.splitlines()
        if line.strip()
    ]
    return lines[-1] if lines else None


def looks_numeric_rate(value: str) -> bool:
    compact = (
        value.replace(" ", "")
        .replace("\u00a0", "")
        .replace(",", "")
        .replace(".", "")
    )
    return compact.isdigit() and len(compact) <= 12


def non_primary_rate(identity: str) -> bool:
    normalized = identity.lower()
    return any(label in normalized for label in NON_PRIMARY_RATE_LABELS)


def has_site_evidence(source: PhysicalSource) -> bool:
    """Recognize explicit source labels, not a supplier or filename convention."""
    return bool(re.search(r"(?i)\bsite\s*(?:code|number|no\.?|inventory)\b", source.searchable_text))


def station_for(source: PhysicalSource, ordinal: int) -> str | None:
    candidates = [
        item.text.splitlines()[0].strip()
        for item in source.texts
        if item.ordinal == ordinal and " fm" in item.text.lower()
    ]
    for value in candidates:
        match = re.search(r"\b([A-Za-z0-9 .&'-]+?\sFM)\b", value, re.I)
        if match:
            return " ".join(match.group(1).split()).upper()
    return None


def day_labels_for(
    source: PhysicalSource,
    ordinal: int,
    count: int,
) -> tuple[str, ...]:
    joined = "\n".join(
        item.text for item in source.texts if item.ordinal == ordinal
    ).upper()
    known: list[str] = []
    for label, code in (
        ("MONDAY - FRIDAY", "MONDAY_FRIDAY"),
        ("MONDAY–FRIDAY", "MONDAY_FRIDAY"),
        ("SATURDAY", "SATURDAY"),
        ("SUNDAY", "SUNDAY"),
    ):
        if label in joined and code not in known:
            known.append(code)
    if len(known) >= count:
        return tuple(known[:count])
    return ("MONDAY_FRIDAY", "SATURDAY", "SUNDAY")[:count]


def primary_rate_from_items(items: list[SourceText]) -> str | None:
    for index, item in enumerate(items):
        if (
            "rate card" not in item.text.lower()
            and "monthly rate" not in item.text.lower()
        ):
            continue
        own = clean_money(item.text)
        if own:
            return own
        for nearby in items[max(0, index - 2): index + 4]:
            value = clean_money(nearby.text)
            if value and not non_primary_rate(nearby.text):
                return value
    return None


def site_identity(items: list[SourceText], code: str) -> str | None:
    for item in items:
        if code.lower() in item.text.lower() and len(item.text) > len(code):
            return " ".join(item.text.split())
    for item in items:
        value = " ".join(item.text.split())
        if (
            value
            and len(value) <= 180
            and not MONEY_PATTERN.search(value)
            and not DIMENSION_PATTERN.fullmatch(value)
            and "site number" not in value.lower()
        ):
            return f"{code} - {value}"
    return code
