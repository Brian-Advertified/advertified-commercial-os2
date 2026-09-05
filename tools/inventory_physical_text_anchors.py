"""Historical text-anchor calibration rules; not production schema discovery."""

from __future__ import annotations

import re
from typing import Iterable

from inventory_physical_facts import (
    DIMENSION_PATTERN,
    MONEY_PATTERN,
    TIME_BAND_PATTERN,
    PhysicalAnchor,
    PhysicalSource,
    SourceText,
    clean_money,
    clean_product_code,
    day_labels_for,
    first_index,
    first_product_code,
    last_meaningful_line,
    has_site_evidence,
    looks_numeric_rate,
    non_primary_rate,
    normalize_compact,
    normalize_currency,
    normalize_header,
    normalize_money,
    primary_rate_from_items,
    product_codes,
    site_identity,
    station_for,
    value_at,
)


def site_code_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    if not has_site_evidence(source):
        return []
    result: list[PhysicalAnchor] = []
    by_ordinal: dict[int, list[SourceText]] = {}
    for item in source.texts:
        by_ordinal.setdefault(item.ordinal, []).append(item)
    seen: set[str] = set()
    for ordinal, items in sorted(by_ordinal.items()):
        authoritative = [
            item
            for item in items
            if item.confidence is None or item.confidence >= 0.5
        ]
        joined = "\n".join(item.text for item in authoritative)
        for code in product_codes(joined):
            normalized = normalize_compact(code)
            if normalized in seen:
                continue
            seen.add(normalized)
            result.append(PhysicalAnchor(
                anchor_type="SITE_CODE",
                locator=next(
                    (
                        item.locator
                        for item in items
                        if code.lower() in item.text.lower()
                    ),
                    items[0].locator,
                ),
                ordinal=ordinal,
                identity=site_identity(items, code) or code,
                product_code=code,
                raw_rate=primary_rate_from_items(items),
                currency=normalize_currency(primary_rate_from_items(items)),
                context=tuple(item.text for item in items if item.text),
            ))
    return result


def location_site_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    grouped: dict[int, list[SourceText]] = {}
    for item in source.texts:
        grouped.setdefault(item.ordinal, []).append(item)
    ignored = (
        "thank you", "digital network", "static ooh snapshot",
        "main market screens", "programmatic", "contact us",
        "who we are", "media kit", "publisher media kit",
        "nationwide coverage", "highway", "introduction",
    )
    table_ordinals = {table.ordinal for table in source.tables}
    for ordinal, items in sorted(grouped.items()):
        if ordinal in table_ordinals:
            continue
        for item in items:
            if item.confidence is not None and item.confidence < 0.5:
                continue
            value = "\n".join(
                line.strip()
                for line in item.text.splitlines()
                if line.strip()
            )
            normalized = value.lower()
            if (
                not value
                or len(value) > 140
                or has_commercial_money(value)
                or DIMENSION_PATTERN.fullmatch(value)
                or any(term in normalized for term in ignored)
            ):
                continue
            lines = value.splitlines()
            first_line_letters = [
                character for character in lines[0]
                if character.isalpha()
            ] if lines else []
            uppercase_heading = (
                len(first_line_letters) >= 3
                and all(character.isupper() for character in first_line_letters)
            )
            looks_location = (
                len(lines) >= 2
                and len(lines[0]) <= 50
                and len(lines[1]) <= 90
                and uppercase_heading
            ) or any(term in normalized for term in (
                "street", "road", "freeway", "airport", "mall",
                "sandton", "soweto", "johannesburg", "cape town",
                "durban", "umhlanga", "ballito", "braamfontein",
            ))
            if not looks_location:
                continue
            result.append(PhysicalAnchor(
                anchor_type="SITE_LOCATION",
                locator=item.locator,
                ordinal=ordinal,
                identity=" | ".join(lines),
                product_code=None,
                raw_rate=None,
                currency=None,
                context=(value,),
            ))
    return result


def text_rate_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    previous_by_ordinal: dict[int, str] = {}
    for item in source.texts:
        previous = previous_by_ordinal.get(item.ordinal, "")
        for match in MONEY_PATTERN.finditer(item.text):
            if route_number_match(item.text, match):
                continue
            raw_rate = clean_money(match.group(0))
            prefix = item.text[: match.start()].strip(" :-–—|\n\t")
            identity = (
                last_meaningful_line(prefix)
                or last_meaningful_line(previous)
            )
            if not identity or non_primary_rate(identity):
                continue
            result.append(PhysicalAnchor(
                anchor_type="TEXT_RATE",
                locator=item.locator,
                ordinal=item.ordinal,
                identity=identity,
                product_code=first_product_code(item.text),
                raw_rate=raw_rate,
                currency=normalize_currency(raw_rate),
                context=(item.text,),
            ))
        previous_by_ordinal[item.ordinal] = item.text
    return result


def has_commercial_money(value: str) -> bool:
    return any(
        not route_number_match(value, match)
        for match in MONEY_PATTERN.finditer(value)
    )


def route_number_match(value: str, match: re.Match[str]) -> bool:
    token = "".join(match.group(0).split())
    if not re.fullmatch(r"R\d{1,3}", token, re.IGNORECASE):
        return False
    line_start = max(
        value.rfind("\n", 0, match.start()),
        value.rfind("\r", 0, match.start()),
    ) + 1
    prefix = value[line_start : match.start()].strip()
    suffix = value[match.end() :].lstrip()
    if prefix:
        return False
    if re.match(
        r"^(?:/|\\|[-–—])|^(?:road|route|freeway|highway|intersection|interchange)\b",
        suffix,
        re.IGNORECASE,
    ):
        return True
    if re.match(
        r"^(?:per|each|cpm|cpc|cpl|cpa|day|week|month|spot|unit|incl|excl|vat)\b",
        suffix,
        re.IGNORECASE,
    ):
        return False
    return bool(re.match(r"^[A-Za-z][A-Za-z .'-]+$", suffix))
