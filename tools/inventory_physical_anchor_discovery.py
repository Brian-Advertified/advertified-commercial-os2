"""Independent discovery of sellable inventory anchors in physical source maps."""

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


from inventory_physical_text_anchors import (
    site_code_anchors,
    location_site_anchors,
    text_rate_anchors,
    has_commercial_money,
    route_number_match,
)

def discover_anchors(source: PhysicalSource) -> tuple[PhysicalAnchor, ...]:
    key_value_sites = key_value_table_anchors(source)
    key_value_ordinals = {item.ordinal for item in key_value_sites}
    structured = [
        item for item in structured_table_anchors(source)
        if item.ordinal not in key_value_ordinals
    ]
    if source.document_format == "XLSX" and structured:
        # A structured workbook row is the authoritative physical unit.
        # Do not add weaker OCR/text anchors for the same cells.
        return deduplicate_anchors(structured)

    structured_ordinals = {
        *(item.ordinal for item in key_value_sites),
        *(item.ordinal for item in structured),
    }
    anchors: list[PhysicalAnchor] = [*key_value_sites, *structured]
    anchors.extend(
        item for item in radio_table_anchors(source)
        if item.ordinal not in structured_ordinals
    )
    anchors.extend(
        item for item in table_rate_anchors(source)
        if item.ordinal not in structured_ordinals
    )
    anchors.extend(
        item for item in site_code_anchors(source)
        if item.ordinal not in structured_ordinals
    )
    anchors.extend(
        item for item in location_site_anchors(source)
        if item.ordinal not in structured_ordinals
    )
    if not (structured and has_site_evidence(source)):
        anchors.extend(
            item for item in text_rate_anchors(source)
            if item.ordinal not in structured_ordinals
        )
    return deduplicate_anchors(anchors)


def key_value_table_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    """Discover one physical site from each vertical label/value table."""
    result: list[PhysicalAnchor] = []
    texts_by_ordinal: dict[int, list[SourceText]] = {}
    for item in source.texts:
        texts_by_ordinal.setdefault(item.ordinal, []).append(item)
    site_labels = {
        "description", "area", "cityprov", "cityprovince", "location",
        "trafficcount", "impacts", "impressions", "frequency", "type",
        "format", "driversside", "gps", "gpscoordinate", "ratecard",
        "discountedrate", "monthlyrental", "targetmall", "venue", "ara",
        "notes", "sitenumber", "sitecode", "productcode", "availability",
        "size", "dimensions", "production", "printing", "flighting",
    }
    primary_rate_labels = (
        "ratecard", "monthlyrental", "monthlyrate", "rate", "price",
        "investment", "packagecost",
    )
    identity_labels = (
        "area", "cityprov", "cityprovince", "location", "format",
        "targetmall", "venue", "description",
    )
    for table in source.tables:
        pairs: dict[str, tuple[str, int]] = {}
        for row_number, row in enumerate(table.rows, start=1):
            if len(row) < 2:
                continue
            label = normalize_header(row[0])
            value = row[1].strip()
            if label in site_labels and value:
                pairs.setdefault(label, (value, row_number))
        if len(set(pairs).intersection(site_labels)) < 4:
            continue
        has_identity = any(label in pairs for label in identity_labels)
        has_site_detail = any(
            label in pairs
            for label in (
                "format", "gps", "gpscoordinate", "sitenumber", "sitecode",
                "productcode", "ratecard", "monthlyrental", "availability",
            )
        )
        if not has_identity or not has_site_detail:
            continue

        code = next(
            (
                clean_product_code(pairs[label][0])
                for label in ("productcode", "sitecode", "sitenumber")
                if label in pairs and clean_product_code(pairs[label][0])
            ),
            None,
        )
        same_page_text = [
            item for item in texts_by_ordinal.get(table.ordinal, [])
            if item.confidence is None or item.confidence >= 0.5
        ]
        if not code:
            joined = "\n".join(item.text for item in same_page_text)
            contextual = re.search(
                r"\b(?:ISO|ISJ|ISC|ISD|ISEC|ISNW)\s*[-/]?\s*\d{2,6}[A-Z]?\b",
                joined,
                re.IGNORECASE,
            )
            code = clean_product_code(contextual.group(0)) if contextual else None
        if not code:
            code = first_product_code(
                "\n".join(item.text for item in same_page_text)
            )

        rate_value = next(
            (
                pairs[label][0]
                for label in primary_rate_labels
                if label in pairs
            ),
            "",
        )
        raw_rate = clean_money(rate_value) or None
        identity_values = [
            pairs[label][0]
            for label in identity_labels
            if label in pairs and pairs[label][0]
        ]
        identity = " | ".join(dict.fromkeys(identity_values[:3]))
        if code and code.lower() not in identity.lower():
            identity = f"{code} - {identity}" if identity else code
        if not identity:
            continue
        context = tuple(
            f"{label}: {value}"
            for label, (value, _) in pairs.items()
        )
        result.append(PhysicalAnchor(
            anchor_type="KEY_VALUE_SITE",
            locator=f"{table.locator};site=1",
            ordinal=table.ordinal,
            identity=identity,
            product_code=code,
            raw_rate=raw_rate,
            currency=normalize_currency(raw_rate),
            context=context,
        ))
    return result


def structured_table_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    for table in source.tables:
        if len(table.rows) < 2:
            continue
        headers = [normalize_header(value) for value in table.rows[0]]
        name_column = first_index(
            headers,
            (
                "name", "sitename", "platform", "product", "productname",
                "site", "offering", "adunit", "placement", "package",
                "packagename", "advertisingoption",
            ),
        )
        rate_column = first_index(
            headers,
            (
                "baseprice", "rate", "price", "cost", "ratecard",
                "monthlyrental", "monthlyrate", "netrate", "netrates",
                "packagecost", "investment", "amount", "costpermonth",
            ),
        )
        currency_column = first_index(headers, ("currency",))
        code_column = first_index(
            headers, ("productcode", "sitecode", "sitenumber")
        )
        if name_column is None and code_column is None:
            continue
        for offset, row in enumerate(table.rows[1:], start=2):
            identity = value_at(row, name_column)
            code = clean_product_code(value_at(row, code_column))
            if not identity and not code:
                continue
            raw_rate = value_at(row, rate_column)
            currency = normalize_currency(
                value_at(row, currency_column) or raw_rate
            )
            normalized_rate = clean_money(raw_rate) or raw_rate.strip() or None
            if (
                normalized_rate
                and currency
                and normalize_currency(normalized_rate) is None
            ):
                normalized_rate = f"{currency} {normalized_rate}"
            result.append(PhysicalAnchor(
                anchor_type=(
                    "WORKBOOK_ROW"
                    if source.document_format == "XLSX"
                    else "TABLE_ROW"
                ),
                locator=f"{table.locator};row={offset}",
                ordinal=table.ordinal,
                identity=identity or code,
                product_code=code,
                raw_rate=normalized_rate,
                currency=currency,
                context=tuple(value for value in row if value),
            ))
    return result


def radio_table_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    for table in source.tables:
        if len(table.rows) < 2:
            continue
        header = [normalize_header(value) for value in table.rows[0]]
        pair_starts = [
            index
            for index in range(len(header) - 1)
            if header[index] == "timeband"
            and header[index + 1] in {"netrates", "rate", "rates"}
        ]
        if not pair_starts:
            continue
        station = station_for(source, table.ordinal)
        days = day_labels_for(source, table.ordinal, len(pair_starts))
        for row_number, row in enumerate(table.rows[1:], start=2):
            for pair_index, column in enumerate(pair_starts):
                time_band = value_at(row, column)
                raw_rate = value_at(row, column + 1)
                if (
                    not time_band
                    or not raw_rate
                    or not TIME_BAND_PATTERN.match(time_band)
                    or not looks_numeric_rate(raw_rate)
                ):
                    continue
                day = (
                    days[pair_index]
                    if pair_index < len(days)
                    else f"DAY_{pair_index + 1}"
                )
                identity = " - ".join(
                    value for value in (station, day, time_band) if value
                )
                result.append(PhysicalAnchor(
                    anchor_type="RADIO_RATE",
                    locator=(
                        f"{table.locator};row={row_number};pair={pair_index + 1}"
                    ),
                    ordinal=table.ordinal,
                    identity=identity,
                    product_code=None,
                    raw_rate=raw_rate.strip(),
                    currency="ZAR",
                    context=(station or "", day, time_band, raw_rate.strip()),
                ))
    return result


def table_rate_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    for table in source.tables:
        for row_number, row in enumerate(table.rows, start=1):
            for column, cell in enumerate(row):
                for match in MONEY_PATTERN.finditer(cell):
                    if route_number_match(cell, match):
                        continue
                    raw_rate = clean_money(match.group(0))
                    identity = identity_from_table_row(
                        row, column, cell, match.start()
                    )
                    if not raw_rate or not identity or non_primary_rate(identity):
                        continue
                    result.append(PhysicalAnchor(
                        anchor_type="TABLE_RATE",
                        locator=(
                            f"{table.locator};row={row_number};cell={column + 1}"
                        ),
                        ordinal=table.ordinal,
                        identity=identity,
                        product_code=first_product_code(" ".join(row)),
                        raw_rate=raw_rate,
                        currency=normalize_currency(raw_rate),
                        context=tuple(value for value in row if value),
                    ))
    return result


def deduplicate_anchors(
    values: Iterable[PhysicalAnchor],
) -> tuple[PhysicalAnchor, ...]:
    result: list[PhysicalAnchor] = []
    seen: set[tuple[str, str, str, int]] = set()
    for item in values:
        key = (
            normalize_compact(item.product_code or item.identity or ""),
            normalize_money(item.raw_rate or ""),
            item.anchor_type if item.anchor_type == "RADIO_RATE" else "OFFER",
            item.ordinal,
        )
        if not key[0] and not key[1]:
            continue
        if key in seen:
            continue
        seen.add(key)
        result.append(item)
    return tuple(result)


def identity_from_table_row(
    row: tuple[str, ...],
    money_column: int,
    money_cell: str,
    money_start: int,
) -> str | None:
    prefix = money_cell[:money_start].strip(" :-–—|\n\t")
    if prefix:
        return last_meaningful_line(prefix)
    for index in range(money_column - 1, -1, -1):
        value = row[index].strip()
        if value and not looks_numeric_rate(value):
            return last_meaningful_line(value)
    return None
