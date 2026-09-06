"""Generic detection of bare-number daypart rate matrices."""

from __future__ import annotations

import re

from inventory_docling_structure import TableCell

_NUMERIC_AMOUNT = re.compile(
    r"^\s*\d[\d \u00a0]*(?:[.,]\d{1,2})?\s*$"
)
_TIME_RANGE = re.compile(
    r"^\s*\d{1,2}:\d{2}\s*[-\u2013\u2014]\s*\d{1,2}:\d{2}\s*\*?\s*$"
)


def is_numeric_amount(value: str) -> bool:
    return bool(_NUMERIC_AMOUNT.fullmatch(value))


def is_numeric_rate_matrix(
    grid: dict[int, dict[int, TableCell]], rate_document: bool
) -> bool:
    if not rate_document:
        return False
    text = " ".join(cell.text for row in grid.values() for cell in row.values())
    if re.search(
        r"\b(?:no\.?|number)\s+of\s+(?:spots?|units?|insertions?)\b|\b(?:spot|unit|insertion)\s+count\b|\bquantity\b",
        text, re.I,
    ):
        return False
    for row in grid.values():
        cells = tuple({cell.locator: cell for cell in row.values()}.values())
        if (
            any(_TIME_RANGE.fullmatch(cell.text) for cell in cells)
            and sum(is_numeric_amount(cell.text) for cell in cells) >= 2
        ):
            return True
    return False
