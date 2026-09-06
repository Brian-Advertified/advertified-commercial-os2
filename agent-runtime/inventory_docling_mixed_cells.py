"""Generic segmentation of dense Docling cells containing many programme rates."""

from __future__ import annotations

import re

from inventory_docling_rates import (
    MONEY_PATTERN,
    NON_PRODUCT_RATE_LABELS,
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
    for column, cell in sorted(row.items()):
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
