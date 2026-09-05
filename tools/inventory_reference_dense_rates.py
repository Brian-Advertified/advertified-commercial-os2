"""Source-led priced-variant enumeration for remaining dense rate cards."""

from __future__ import annotations

import re
from typing import Any, Callable

from inventory_reference_emedia import emedia_entries


BUSINESS_DAY = "b5780f91ecee7d4a4c956c92ce570628ea4ecac711efa7798e3660a1a9f4be95"
SOWETAN = "37da01f085bae3f95da37cc4129b75c3ed6590cfdc821e04efae9ce7c6daad88"
SUNDAY_TIMES = "f5e663f95ed569192414acb956ae29c72a276e1d0364cea0cab2bc3d0d94da8e"
HERALD = "9f25d44359b0ffd0becd605f66e6a2015b104021b9993e1e65f53ca0dc4e6c7a"
EMEDIA = "217bebb97b2d136c2875addedaf9f5619613e013c5171f678e8d027ab74b117d"
MEDIA_DECK = "8932a7849710407e06af6128de2c1c4ccfa7cc6dbd3c4cc2b5b8852b9dcceb23"
PRIMEDIA_BROADCAST = "9bab2c24402cf8cb283633d473380e0214c644c607e52c6ef0be79afe35fb1eb"
RATE_HASHES = {
    BUSINESS_DAY, SOWETAN, SUNDAY_TIMES, HERALD,
    EMEDIA, MEDIA_DECK, PRIMEDIA_BROADCAST,
}
MONEY = re.compile(r"R\s*\d+(?:[ ,]\d{3})*(?:[.,]\d{2})?", re.IGNORECASE)
OTHER_RATE = re.compile(
    r"(?:RATE\s+ON\s+REQUEST|\bPOA\b|RATE\s*\+\s*\d+(?:[.,]\d+)?%|"
    r"\d+(?:[.,]\d+)?%\s+(?:LOADING|PREMIUM|FEE|COMMISSION))",
    re.IGNORECASE,
)
SUMMARY = re.compile(
    r"(?:SUB ?TOTALS?|TOTAL(?: VALUE| INVESTMENT| INVOICE| EXCL VAT)?|"
    r"VAT|SAVINGS?|LESS DISCOUNT|DISCOUNT)",
    re.IGNORECASE,
)


def dense_rate_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    if source_hash == EMEDIA:
        return emedia_entries(make_entry, source_hash)
    if source_hash == MEDIA_DECK:
        return presentation_entries(make_entry, source_hash, source)
    return fragment_entries(make_entry, source_hash, source, "TEXT")


def presentation_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = table_entries(make_entry, source_hash, source)
    table_pages = {
        int(table.get("slide") or table.get("page") or 0)
        for table in source.get("tables", [])
    }
    subset = {
        **source,
        "fragments": [
            item for item in source.get("fragments", [])
            if int(item.get("ordinal") or 0) not in table_pages
        ],
    }
    result.extend(fragment_entries(make_entry, source_hash, subset, "TEXT"))
    return result


def table_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
) -> list[dict[str, Any]]:
    result = []
    for table in source.get("tables", []):
        rows = table.get("rows", [])
        headers = [clean(value) for value in rows[0]] if rows else []
        for row_number, row in enumerate(rows, start=1):
            cells = [clean(value) for value in row]
            for column, cell in enumerate(cells, start=1):
                rates = list(MONEY.finditer(cell)) + list(OTHER_RATE.finditer(cell))
                for occurrence, match in enumerate(sorted(rates, key=lambda item: item.start()), start=1):
                    identity = table_identity(cells, headers, column, cell[:match.start()])
                    if is_summary(identity):
                        continue
                    raw_rate = clean(match.group(0))
                    result.append(make_entry(
                        source_hash,
                        f"{table['locator']};row={row_number};column={column};rate={occurrence}",
                        identity,
                        rate=normalize_rate(raw_rate),
                        raw_rate=raw_rate,
                        currency="ZAR" if MONEY.fullmatch(raw_rate) else "NOT_APPLICABLE",
                        source_cell=cell,
                        row_context=cells,
                        column_context=headers[column - 1] if column <= len(headers) else "",
                    ))
    return result


def fragment_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    source: dict[str, Any],
    kind: str,
) -> list[dict[str, Any]]:
    selected = [item for item in source.get("fragments", []) if item.get("kind") == kind]
    by_page: dict[int, list[dict[str, Any]]] = {}
    for item in selected:
        by_page.setdefault(int(item.get("ordinal") or 0), []).append(item)
    result = []
    for page, fragments in by_page.items():
        for fragment in fragments:
            result.extend(fragment_rate_entries(
                make_entry, source_hash, page, fragment, fragments
            ))
    return result


def fragment_rate_entries(
    make_entry: Callable[..., dict[str, Any]],
    source_hash: str,
    page: int,
    fragment: dict[str, Any],
    page_fragments: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    result = []
    text = str(fragment.get("text") or "")
    for line_number, line in enumerate(text.splitlines(), start=1):
        rates = list(MONEY.finditer(line)) + list(OTHER_RATE.finditer(line))
        for occurrence, match in enumerate(sorted(rates, key=lambda item: item.start()), start=1):
            local = clean(line[:match.start()]).strip("-|:• ")
            identity = local or spatial_identity(fragment, page_fragments)
            if not identity:
                identity = previous_line_identity(text, line_number)
            if is_summary(identity):
                continue
            raw_rate = clean(match.group(0))
            result.append(make_entry(
                source_hash,
                f"{fragment['locator']};line={line_number};rate={occurrence}",
                identity or f"source price page {page}",
                rate=normalize_rate(raw_rate),
                raw_rate=raw_rate,
                currency="ZAR" if MONEY.fullmatch(raw_rate) else "NOT_APPLICABLE",
                source_line=clean(line),
                source_bbox=fragment.get("bbox"),
            ))
    return result


def spatial_identity(
    fragment: dict[str, Any],
    page_fragments: list[dict[str, Any]],
) -> str:
    box = fragment.get("bbox")
    if not isinstance(box, list) or len(box) != 4:
        return ""
    center_y = (box[1] + box[3]) / 2
    candidates = []
    for item in page_fragments:
        other = item.get("bbox")
        text = clean(item.get("text"))
        if item is fragment or not text or MONEY.search(text) or OTHER_RATE.search(text):
            continue
        if not isinstance(other, list) or len(other) != 4 or other[2] > box[0] + 5:
            continue
        distance = abs(((other[1] + other[3]) / 2) - center_y)
        if distance <= max(20, box[3] - box[1]):
            candidates.append((distance, box[0] - other[2], text))
    return min(candidates)[2] if candidates else ""


def previous_line_identity(text: str, line_number: int) -> str:
    lines = [clean(line) for line in text.splitlines()]
    for index in range(line_number - 2, max(-1, line_number - 5), -1):
        if lines[index] and not MONEY.search(lines[index]) and not OTHER_RATE.search(lines[index]):
            return lines[index]
    return ""


def table_identity(
    cells: list[str],
    headers: list[str],
    column: int,
    local: str,
) -> str:
    parts = [clean(local)] if clean(local) else []
    parts.extend(
        cell for cell in cells[:2]
        if cell and not MONEY.search(cell) and not OTHER_RATE.search(cell) and cell not in parts
    )
    if column <= len(headers):
        header = headers[column - 1]
        if header and header not in parts:
            parts.append(header)
    return " - ".join(parts)[:500] or f"table column {column}"


def is_summary(identity: str) -> bool:
    return bool(SUMMARY.fullmatch(clean(identity).rstrip(":")))


def normalize_rate(value: str) -> str:
    if not MONEY.fullmatch(value):
        return value.upper().replace("  ", " ")
    raw = re.sub(r"[^0-9,.]", "", value)
    if "," in raw and "." not in raw and len(raw.rsplit(",", 1)[-1]) == 2:
        return raw.replace(".", "").replace(",", ".")
    return raw.replace(",", "")


def clean(value: Any) -> str:
    return re.sub(r"\s+", " ", str(value or "")).strip()
