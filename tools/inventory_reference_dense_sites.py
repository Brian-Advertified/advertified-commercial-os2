"""Exact source-led enumeration for the five remaining site catalogues."""

from __future__ import annotations

import re
from typing import Any, Callable


BLACKSPACE = "0a0abca0e2c94999a4c69b7ce5f3acbf5c73e32492f4da02b4fcc6abfb0b1f8c"
INSIGHT = "a8bcdb4b9268aac2842fa1caf1d42d3ab5aa491746068ce3f56ba6ac81d1fe4a"
JCDECAUX = "74bfa893f0b0989c860012fd794e7f7bde1b24ce254ebb026f57415c94c445b1"
PRIMEDIA_OUTDOOR = "db805a6e6cf600e6c3a9506e2431b4b79f913fbe0259ea775633d753a889c31d"
RELATIV = "85a86afebbde33d4944cd1172b30992b26e94cecd86b9f4228a8c2e0d4b77650"
SITE_HASHES = {BLACKSPACE, INSIGHT, JCDECAUX, PRIMEDIA_OUTDOOR, RELATIV}
EXPECTED_COUNTS = {
    BLACKSPACE: 62,
    INSIGHT: 43,
    JCDECAUX: 88,
    PRIMEDIA_OUTDOOR: 62,
    RELATIV: 37,
}
MONEY = re.compile(r"R\s*\d+(?:[ ,]\d{3})*(?:[.,]\d{2})?", re.IGNORECASE)
PRIMEDIA_CODE = re.compile(r"^[A-Z]{2,5}\d{3}$", re.IGNORECASE)
RELATIV_CODE = re.compile(
    r"\b(?:REL\s*[\-–]?\s*\d{3}|RRD\s*\d{3}|OHL\s*\d{3}|RCM\s*\d{3})\b",
    re.IGNORECASE,
)
JC_CODE = re.compile(
    r"\b(?:[A-Z0-9]{2,12}(?:\s*-\s*|[.]D)\w{2,6}|[A-Z]{2,5}\d{2,6}[.]D\d{2}S)\b",
    re.IGNORECASE,
)


def site_reference_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    readers = {
        BLACKSPACE: blackspace_entries,
        INSIGHT: insight_entries,
        JCDECAUX: jcdecaux_entries,
        PRIMEDIA_OUTDOOR: primedia_outdoor_entries,
        RELATIV: relativ_entries,
    }
    entries = readers[source_hash](make_entry, source_hash, source)
    expected = EXPECTED_COUNTS[source_hash]
    if len(entries) != expected:
        raise ValueError(
            f"Site enumeration mismatch for {source_hash}: "
            f"expected {expected}, found {len(entries)}"
        )
    return entries


def blackspace_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    pages = list(range(3, 32)) + list(range(33, 49)) + list(range(50, 67))
    return [
        page_site_entry(make_entry, source_hash, source, page, "BlackSpace")
        for page in pages
    ]


def jcdecaux_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    roadside = list(range(3, 6)) + list(range(7, 39))
    mall = (
        list(range(40, 44)) + list(range(45, 50)) +
        list(range(51, 60)) + list(range(61, 69)) +
        list(range(70, 72)) + list(range(73, 77)) +
        list(range(78, 81)) + list(range(82, 85)) +
        list(range(86, 101))
    )
    result = [
        page_site_entry(make_entry, source_hash, source, page, "JCDecaux roadside")
        for page in roadside
    ]
    result.extend(
        page_site_entry(make_entry, source_hash, source, page, "JCDecaux mall")
        for page in mall
    )
    return result


def page_site_entry(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
    page: int,
    catalogue: str,
) -> dict[str, Any]:
    text = page_text(source, page)
    code = first_code(text, catalogue)
    title = first_meaningful_line(text)
    rates = unique(MONEY.findall(text))
    fields: dict[str, Any] = {
        "product_code": code or "NOT_STATED",
        "catalogue": catalogue,
        "source_fields": labeled_fields(text),
        "source_context": text,
    }
    if rates:
        fields["source_rates"] = rates
        fields["currency"] = "ZAR"
    return make_entry(
        source_hash,
        f"source-map:page={page};site=1",
        code or title or f"{catalogue} page {page}",
        **fields,
    )


def insight_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = []
    for table in source.get("tables", []):
        values = row_pairs(table.get("rows", []))
        page = int(table.get("slide") or table.get("page") or 0)
        context = page_text(source, page)
        title = first_meaningful_line(context)
        rate = value_for(values, "rate card")
        discounted = value_for(values, "discounted rate")
        result.append(make_entry(
            source_hash,
            table["locator"],
            title or f"Insight slide {page}",
            product_code=title or "NOT_STATED",
            source_fields=values,
            rate=rate or "NOT_STATED",
            discounted_rate=discounted or "NOT_STATED",
            currency="ZAR" if rate or discounted else "NOT_STATED",
        ))
    return result


def primedia_outdoor_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = []
    for table in source.get("tables", []):
        rows = table.get("rows", [])
        if len(rows) < 2:
            continue
        headers = [clean(value) for value in rows[0]]
        for row_number, row in enumerate(rows[1:], start=2):
            cells = [clean(value) for value in row]
            if not cells or not (
                cells[0].isdigit() or PRIMEDIA_CODE.fullmatch(cells[0])
            ):
                continue
            fields = dict(zip(headers, cells))
            fields["screen_count"] = screen_count(headers, cells)
            result.append(make_entry(
                source_hash,
                f"{table['locator']};row={row_number}",
                " - ".join(value for value in cells[:4] if value),
                product_code=cells[0],
                source_fields=fields,
                source_rates=unique(MONEY.findall(" | ".join(cells))),
            ))
    return result


def relativ_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    by_code: dict[str, list[int]] = {}
    for page in sorted({int(item.get("ordinal") or 0) for item in source["fragments"]}):
        text = page_text(source, page)
        match = RELATIV_CODE.search(text)
        if match:
            by_code.setdefault(normalize_code(match.group(0)), []).append(page)
    result = []
    for code, pages in by_code.items():
        context = "\n".join(page_text(source, page) for page in pages)
        result.append(make_entry(
            source_hash,
            "source-map:pages=" + ",".join(map(str, pages)),
            title_containing(context, code) or code,
            product_code=code,
            source_fields=labeled_fields(context),
            source_rates=unique(MONEY.findall(context)),
            source_context=context,
        ))
    return result


def page_text(source: dict[str, Any], page: int) -> str:
    fragments = [
        clean(item.get("text"))
        for item in source.get("fragments", [])
        if int(item.get("ordinal") or 0) == page and item.get("kind") == "TEXT"
    ]
    tables = [
        " | ".join(clean(cell) for cell in row)
        for table in source.get("tables", [])
        if int(table.get("page") or table.get("slide") or 0) == page
        for row in table.get("rows", [])
    ]
    return "\n".join(unique(fragments + tables))


def first_code(text: str, catalogue: str) -> str | None:
    pattern = RELATIV_CODE if catalogue == "Relativ" else JC_CODE
    match = pattern.search(text)
    return normalize_code(match.group(0)) if match else None


def labeled_fields(text: str) -> dict[str, str]:
    lines = [clean(line) for line in text.splitlines() if clean(line)]
    result: dict[str, str] = {}
    labels = {
        "location", "site description", "product type", "size", "dimensions",
        "availability", "production", "address", "monthly vac’’s", "slots",
    }
    for index, line in enumerate(lines[:-1]):
        key = line.lower().rstrip(":")
        if key in labels and lines[index + 1]:
            result[key] = lines[index + 1]
    return result


def row_pairs(rows: list[list[Any]]) -> dict[str, str]:
    return {
        clean(row[0]): clean(row[1])
        for row in rows
        if len(row) >= 2 and clean(row[0])
    }


def value_for(values: dict[str, str], label: str) -> str | None:
    return next(
        (value for key, value in values.items() if key.lower().rstrip(":") == label),
        None,
    )


def screen_count(headers: list[str], cells: list[str]) -> str:
    for header, value in zip(headers, cells):
        if "screen" in header.lower():
            return value or "NOT_STATED"
    return "1"


def title_containing(text: str, code: str) -> str | None:
    compact = code.replace("-", "")
    return next(
        (line for line in text.splitlines() if compact in normalize_code(line)),
        None,
    )


def first_meaningful_line(text: str) -> str:
    ignored = {"ROADSIDE DIGITAL", "GROSS IMPRESSIONS", "SITE:", "SOURCE:"}
    return next(
        (line for line in text.splitlines() if line and line.upper() not in ignored),
        "",
    )


def normalize_code(value: str) -> str:
    return re.sub(r"\s+", "", value).replace("–", "-").upper()


def clean(value: Any) -> str:
    return re.sub(r"\s+", " ", str(value or "")).strip()


def unique(values: list[str]) -> list[str]:
    return list(dict.fromkeys(clean(value) for value in values if clean(value)))
