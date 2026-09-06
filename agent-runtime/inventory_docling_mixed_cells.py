"""Generic segmentation of dense Docling cells containing many programme rates."""

from __future__ import annotations

import re

from inventory_docling_rates import (
    MONEY_PATTERN,
    NON_PRODUCT_RATE_LABELS,
    is_table_rate,
)
from inventory_docling_structure import TableCell
from inventory_extraction_contracts import ExtractedRateVariant, ExtractedRow


_TIME = re.compile(r"\b\d{1,2}:\d{2}\b")


def project_mixed_rate_row(
    row: dict[int, TableCell],
    headers: dict[int, tuple[str, tuple[str, ...]]],
    section: tuple[str, str] | None,
) -> list[ExtractedRow]:
    if not any(_is_dense_rate_cell(cell) for cell in row.values()):
        return []
    output = []
    for column, cell in dict((item[1].locator, item) for item in sorted(row.items())).values():
        header, header_locators = headers.get(
            column, (f"column_{column + 1}", ())
        )
        if _non_product(header):
            continue
        matches = list(MONEY_PATTERN.finditer(cell.text))
        if not matches:
            continue
        output.extend(_cell_rows(
            cell, header, header_locators, matches, section
        ))
    return output


def project_unrepresented_rate_cells(
    cells: tuple[TableCell, ...],
    headers: dict[int, tuple[str, tuple[str, ...]]],
    section: tuple[str, str] | None,
    used: set[str],
    represented: set[int],
) -> list[ExtractedRow]:
    """Recover physical schedule/rate cells without an emitted relationship."""
    output = []
    schedule_matrix = _is_schedule_matrix(headers)
    for cell in cells:
        if cell.locator in used:
            continue
        header, header_locators = headers.get(
            cell.column, (f"column_{cell.column + 1}", ())
        )
        matches = list(MONEY_PATTERN.finditer(cell.text))
        eligible = (
            schedule_matrix if id(cell) in represented
            else is_table_rate(header, cell.text)
        )
        if _non_product(header) or not matches or not eligible:
            continue
        output.extend(_cell_rows(
            cell, header, header_locators, matches, section
        ))
        used.add(cell.locator)
    return output


def _is_schedule_matrix(
    headers: dict[int, tuple[str, tuple[str, ...]]],
) -> bool:
    values = [header for header, _ in headers.values()]
    dated = sum(bool(re.search(
        r"\b(?:20\d{2}[/-]\d{2}[/-]\d{2}|mon(?:day)?|tue(?:sday)?|wed(?:nesday)?|thu(?:rsday)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\b",
        header, re.I,
    )) for header in values)
    return dated >= 2 and any("time slot" in header.casefold() for header in values)


def _is_dense_rate_cell(cell: TableCell) -> bool:
    matches = list(MONEY_PATTERN.finditer(cell.text))
    return len(matches) >= 2 and (
        len(cell.text) >= 120 or bool(_TIME.search(cell.text))
    )


def _cell_rows(
    cell: TableCell,
    header: str,
    header_locators: tuple[str, ...],
    matches: list[re.Match[str]],
    section: tuple[str, str] | None,
) -> list[ExtractedRow]:
    output = []
    prior_end = 0
    for match in matches:
        name = _segment_name(cell.text[prior_end:match.start()])
        prior_end = match.end()
        if not name:
            name = header
        values = {"name": name, "rate": match.group(0).strip()}
        locators = {"name": cell.locator, "rate": cell.locator}
        if section:
            values["sectioncontext"] = section[0]
            locators["sectioncontext"] = section[1]
        variant = ExtractedRateVariant(
            raw_value=match.group(0).strip(),
            source_locator=cell.locator,
            header_hierarchy=header,
            header_locators=header_locators,
            position_json=cell.position_json,
            currency="ZAR",
        )
        output.append(ExtractedRow(
            number=1,
            locator=cell.locator,
            values=values,
            extraction_method="MIXED_CELL",
            field_locators=locators,
            rate_variants=(variant,),
        ))
    return output


def _segment_name(value: str) -> str:
    return " ".join(value.strip(" \n\t|,;:+-()").split())[:500]


def _non_product(header: str) -> bool:
    tokens = set(re.sub(
        r"[^a-z0-9]+", " ", header.casefold()
    ).split())
    return bool(tokens & NON_PRODUCT_RATE_LABELS)
