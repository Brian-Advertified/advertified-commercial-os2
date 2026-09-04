"""Independent sellable-unit derivation from physical document tables."""

from __future__ import annotations

from typing import Any, Iterable

from inventory_physical_model import (
    IDENTITY_LABELS,
    RATE_LABELS,
    TIME,
    VERTICAL_LABELS,
    PhysicalUnit,
    cell,
    extract_money,
    first_value,
    group_by_scope,
    locator_scope,
    meaningful_identity,
    normalize_label,
    numeric_rate,
    table_rows,
)


def extract_table_units(
    tables: Iterable[dict[str, Any]],
    fragments: list[dict[str, Any]],
) -> list[PhysicalUnit]:
    result: list[PhysicalUnit] = []
    for table_index, table in enumerate(tables, start=1):
        rows = table_rows(table)
        if not rows:
            continue
        locator = str(table.get("locator") or f"table:{table_index}")
        scope = locator_scope(locator)
        radio = extract_radio_units(rows, locator, scope, fragments)
        if radio:
            result.extend(radio)
            continue
        vertical = extract_vertical_unit(rows, locator, scope)
        if vertical:
            result.append(vertical)
            continue
        horizontal = extract_horizontal_units(rows, locator, scope)
        if horizontal:
            result.extend(horizontal)
            continue
        result.extend(extract_headerless_units(rows, locator, scope))
    return result


def extract_radio_units(
    rows: list[list[str]],
    locator: str,
    scope: str,
    fragments: list[dict[str, Any]],
) -> list[PhysicalUnit]:
    header_index = next(
        (
            index
            for index, row in enumerate(rows[:5])
            if sum("time band" in normalize_label(value) for value in row) >= 1
            and sum("rate" in normalize_label(value) for value in row) >= 1
        ),
        None,
    )
    if header_index is None:
        return []
    header = rows[header_index]
    pairs = [
        (column, column + 1)
        for column in range(0, len(header) - 1, 2)
        if "time band" in normalize_label(header[column])
        and "rate" in normalize_label(header[column + 1])
    ]
    if not pairs:
        return []
    days = radio_days(scope, fragments, len(pairs))
    station = station_name(scope, fragments)
    result: list[PhysicalUnit] = []
    for row_index, row in enumerate(
        rows[header_index + 1 :], start=header_index + 2
    ):
        for pair_index, (time_column, rate_column) in enumerate(pairs):
            time_band = cell(row, time_column)
            rate = cell(row, rate_column)
            if not TIME.match(time_band) or not numeric_rate(rate):
                continue
            day = (
                days[pair_index]
                if pair_index < len(days)
                else f"DAY_{pair_index + 1}"
            )
            identity = " - ".join(
                value for value in (station, day, time_band) if value
            )
            result.append(PhysicalUnit(
                key=f"{locator}:radio:{row_index}:{pair_index + 1}",
                locator=f"{locator};row={row_index};pair={pair_index + 1}",
                scope=scope,
                kind="RADIO_RATE",
                identity=identity,
                raw_rate="R" + rate.strip(),
                evidence=(time_band, rate, station, day),
            ))
    return result


def extract_vertical_unit(
    rows: list[list[str]], locator: str, scope: str
) -> PhysicalUnit | None:
    pairs: list[tuple[str, str]] = []
    for row in rows:
        if len(row) < 2:
            continue
        label = normalize_label(row[0])
        value = " | ".join(item.strip() for item in row[1:] if item.strip())
        if label and value:
            pairs.append((label, value))
    if sum(label in VERTICAL_LABELS for label, _ in pairs) < 4:
        return None
    values = {label: value for label, value in pairs}
    rate = first_value(
        values,
        "discounted rate",
        "rate card",
        "rate",
        "cost",
        "cpm",
    )
    identity = first_value(values, "site number", "site code", "name")
    geography = first_value(
        values,
        "area",
        "city prov",
        "city province",
        "target mall",
    )
    description = first_value(
        values,
        "description",
        "site info",
        "format",
        "type",
    )
    identity = " | ".join(
        value for value in (identity, geography, description) if value
    )
    if not identity:
        return None
    return PhysicalUnit(
        key=f"{locator}:vertical",
        locator=locator,
        scope=scope,
        kind="VERTICAL_SITE",
        identity=identity,
        raw_rate=extract_money(rate) if rate else None,
        evidence=tuple(f"{label}: {value}" for label, value in pairs),
    )


def extract_horizontal_units(
    rows: list[list[str]], locator: str, scope: str
) -> list[PhysicalUnit]:
    header_index = None
    identity_columns: list[int] = []
    rate_columns: list[int] = []
    labels: list[str] = []
    for index, row in enumerate(rows[:10]):
        row_labels = [normalize_label(value) for value in row]
        rates = [
            column for column, value in enumerate(row_labels)
            if value in RATE_LABELS
        ]
        identities = [
            column for column, value in enumerate(row_labels)
            if value in IDENTITY_LABELS
        ]
        if identities and (rates or len(row_labels) >= 2):
            header_index = index
            identity_columns = identities
            rate_columns = rates
            labels = row_labels
            break
    if header_index is None or not identity_columns:
        return []

    priority = (
        "element", "placement", "ad unit", "advertising unit",
        "programme", "program", "show", "description", "name",
        "product", "platform", "site", "site number", "site code",
    )
    identity_column = next(
        (
            column
            for label in priority
            for column in identity_columns
            if labels[column] == label
        ),
        identity_columns[0],
    )
    context_columns = [
        column for column in identity_columns
        if labels[column] in {"platform", "publication", "station"}
    ]
    context: dict[int, str] = {}
    result: list[PhysicalUnit] = []
    for row_index, row in enumerate(
        rows[header_index + 1 :], start=header_index + 2
    ):
        for column in context_columns:
            value = cell(row, column)
            if meaningful_identity(value):
                context[column] = value
        identity = cell(row, identity_column)
        if not meaningful_identity(identity):
            continue

        money_cells = [
            (column, extract_money(value))
            for column, value in enumerate(row)
            if extract_money(value)
        ]
        if not money_cells:
            money_cells = [
                (column, "R" + cell(row, column))
                for column in rate_columns
                if numeric_rate(cell(row, column))
            ]
        raw_rate = money_cells[-1][1] if money_cells else None
        additional = [
            value for column, value in enumerate(row)
            if column != identity_column and value.strip()
        ]
        if rate_columns and raw_rate is None:
            continue
        if not rate_columns and not additional:
            continue

        context_values = [
            context[column]
            for column in context_columns
            if column != identity_column and context.get(column)
        ]
        full_identity = " | ".join((*context_values, identity))
        result.append(PhysicalUnit(
            key=f"{locator}:row:{row_index}",
            locator=f"{locator};row={row_index}",
            scope=scope,
            kind=("HORIZONTAL_RATE" if raw_rate else "HORIZONTAL_ITEM"),
            identity=full_identity,
            raw_rate=raw_rate,
            evidence=tuple(value for value in row if value),
        ))
    return result


def extract_headerless_units(
    rows: list[list[str]], locator: str, scope: str
) -> list[PhysicalUnit]:
    result: list[PhysicalUnit] = []
    for row_index, row in enumerate(rows, start=1):
        prices = [
            (index, extract_money(value))
            for index, value in enumerate(row)
        ]
        prices = [(index, value) for index, value in prices if value]
        if not prices:
            continue
        identity = next(
            (
                value.strip()
                for index, value in enumerate(row)
                if index != prices[0][0] and meaningful_identity(value)
            ),
            "",
        )
        if not identity:
            continue
        for price_index, raw_rate in prices:
            result.append(PhysicalUnit(
                key=(
                    f"{locator}:headerless:{row_index}:"
                    f"{price_index + 1}"
                ),
                locator=(
                    f"{locator};row={row_index};cell={price_index + 1}"
                ),
                scope=scope,
                kind="HEADERLESS_RATE",
                identity=identity,
                raw_rate=raw_rate,
                evidence=tuple(value for value in row if value),
            ))
    return result


def radio_days(
    scope: str,
    fragments: list[dict[str, Any]],
    count: int,
) -> list[str]:
    combined = "\n".join(
        str(item.get("text") or "")
        for item in fragments
        if locator_scope(str(item.get("locator") or "")) == scope
    ).upper()
    result = []
    if "MONDAY" in combined or "WEEKDAY" in combined:
        result.append("MONDAY_FRIDAY")
    if "SATURDAY" in combined:
        result.append("SATURDAY")
    if "SUNDAY" in combined:
        result.append("SUNDAY")
    return result[:count] or [f"DAY_{index + 1}" for index in range(count)]


def station_name(scope: str, fragments: list[dict[str, Any]]) -> str:
    import re

    for item in fragments:
        if locator_scope(str(item.get("locator") or "")) != scope:
            continue
        text = str(item.get("text") or "")
        match = re.search(r"\b([A-Z][A-Z0-9 .&'-]{1,40}\sFM)\b", text)
        if match:
            return " ".join(match.group(1).split())
    return "RADIO"
