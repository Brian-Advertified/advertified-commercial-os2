"""Independent discovery of sellable inventory anchors in physical source maps."""

from __future__ import annotations

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
    looks_like_site_inventory,
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


def discover_anchors(source: PhysicalSource) -> tuple[PhysicalAnchor, ...]:
    anchors: list[PhysicalAnchor] = []
    if source.document_format == "XLSX":
        anchors.extend(workbook_anchors(source))
    anchors.extend(radio_table_anchors(source))
    anchors.extend(table_rate_anchors(source))
    anchors.extend(site_code_anchors(source))
    anchors.extend(location_site_anchors(source))
    anchors.extend(text_rate_anchors(source))
    return deduplicate_anchors(anchors)


def workbook_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    result: list[PhysicalAnchor] = []
    for table in source.tables:
        if len(table.rows) < 2:
            continue
        headers = [normalize_header(value) for value in table.rows[0]]
        name_column = first_index(
            headers, ("name", "platform", "product", "site")
        )
        rate_column = first_index(
            headers, ("baseprice", "rate", "price", "cost", "ratecard")
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
            currency = value_at(row, currency_column)
            result.append(PhysicalAnchor(
                anchor_type="WORKBOOK_ROW",
                locator=f"{table.locator};row={offset}",
                ordinal=table.ordinal,
                identity=identity or code,
                product_code=code,
                raw_rate=(clean_money(raw_rate) or raw_rate.strip() or None),
                currency=normalize_currency(currency or raw_rate),
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


def site_code_anchors(source: PhysicalSource) -> list[PhysicalAnchor]:
    if not looks_like_site_inventory(source.relative_path):
        return []
    result: list[PhysicalAnchor] = []
    by_ordinal: dict[int, list[SourceText]] = {}
    for item in source.texts:
        by_ordinal.setdefault(item.ordinal, []).append(item)
    seen: set[str] = set()
    for ordinal, items in sorted(by_ordinal.items()):
        joined = "\n".join(item.text for item in items)
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
    if not looks_like_site_inventory(source.relative_path):
        return []
    normalized_file = source.relative_path.lower()
    if not any(
        token in normalized_file
        for token in ("reveel", "virgin active")
    ):
        return []
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
        joined = "\n".join(item.text for item in items)
        if product_codes(joined) or ordinal in table_ordinals:
            continue
        for item in items:
            value = "\n".join(
                line.strip()
                for line in item.text.splitlines()
                if line.strip()
            )
            normalized = value.lower()
            if (
                not value
                or len(value) > 140
                or MONEY_PATTERN.search(value)
                or DIMENSION_PATTERN.fullmatch(value)
                or any(term in normalized for term in ignored)
            ):
                continue
            lines = value.splitlines()
            looks_location = (
                len(lines) >= 2
                and len(lines[0]) <= 50
                and len(lines[1]) <= 90
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
