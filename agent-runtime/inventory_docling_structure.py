"""Lossless structural reading helpers for native Docling JSON."""

from __future__ import annotations

import json
from collections import defaultdict
from dataclasses import dataclass
from typing import Any, Iterable

from inventory_extraction_contracts import ExtractedSourceElement


@dataclass(frozen=True)
class SourceElement:
    text: str
    locator: str
    page: int
    label: str
    position_json: str | None
    left: float | None
    top: float | None
    right: float | None
    bottom: float | None

    @property
    def centre_y(self) -> float | None:
        if self.top is None or self.bottom is None:
            return None
        return (self.top + self.bottom) / 2


@dataclass(frozen=True)
class TableCell:
    text: str
    locator: str
    row: int
    column: int
    row_end: int
    column_end: int
    is_header: bool
    is_row_header: bool
    position_json: str | None


def unwrap_document(value: dict[str, Any]) -> dict[str, Any]:
    document = value.get("document", value)
    structured = document.get("json_content", document) if isinstance(document, dict) else document
    if isinstance(structured, str):
        structured = json.loads(structured)
    if not isinstance(structured, dict):
        raise ValueError("Docling document JSON is missing.")
    return structured


def read_texts(document: dict[str, Any]) -> tuple[SourceElement, ...]:
    result = []
    for index, item in enumerate(document.get("texts", []), start=1):
        text = str(item.get("text") or "").strip()
        if not text:
            continue
        prefix = locator_prefix(item, f"docling:page={page(item)}")
        result.append(source_element(item, text, f"{prefix};text={index}"))
    return tuple(result)


def source_element(item: dict[str, Any], text: str, locator: str) -> SourceElement:
    provenance = item.get("prov") or []
    position = provenance[0] if provenance else None
    box = position.get("bbox", {}) if isinstance(position, dict) else {}
    return SourceElement(
        text=text,
        locator=locator,
        page=page(item),
        label=str(item.get("label") or ""),
        position_json=position_json(position),
        left=number(box.get("l")),
        top=number(box.get("t")),
        right=number(box.get("r")),
        bottom=number(box.get("b")),
    )


def read_cells(table: dict[str, Any], table_number: int) -> tuple[TableCell, ...]:
    table_page = page(table)
    result = []
    for item in table.get("data", {}).get("table_cells", []):
        row = int(item.get("start_row_offset_idx", 0))
        column = int(item.get("start_col_offset_idx", 0))
        row_end = int(item.get("end_row_offset_idx", row + int(item.get("row_span", 1))))
        column_end = int(item.get("end_col_offset_idx", column + int(item.get("col_span", 1))))
        prefix = locator_prefix(
            table,
            f"docling:page={table_page};table={table_number}",
        )
        locator = f"{prefix};row={row + 1};cell={column + 1}"
        result.append(TableCell(
            text=str(item.get("text") or "").strip(),
            locator=locator,
            row=row,
            column=column,
            row_end=max(row + 1, row_end),
            column_end=max(column + 1, column_end),
            is_header=bool(item.get("column_header")),
            is_row_header=bool(item.get("row_header")),
            position_json=position_json(item.get("prov") or table.get("prov")),
        ))
    return tuple(result)


def source_elements(
    document: dict[str, Any],
    texts: tuple[SourceElement, ...],
) -> tuple[ExtractedSourceElement, ...]:
    result = [
        ExtractedSourceElement(
            locator=item.locator,
            structure_id=f"docling:page={item.page};text",
            structure_kind=item.label or "text",
            row=index,
            column=1,
            raw_value=item.text,
            position_json=item.position_json,
        )
        for index, item in enumerate(texts, 1)
    ]
    for table_number, table in enumerate(document.get("tables", []), 1):
        table_page = page(table)
        for cell in read_cells(table, table_number):
            if cell.text:
                result.append(ExtractedSourceElement(
                    locator=cell.locator,
                    structure_id=locator_prefix(
                        table,
                        f"docling:page={table_page};table={table_number}",
                    ),
                    structure_kind="table",
                    row=cell.row + 1,
                    column=cell.column + 1,
                    raw_value=cell.text,
                    position_json=cell.position_json,
                ))
    return tuple(result)


def by_page(items: Iterable[SourceElement]) -> dict[int, tuple[SourceElement, ...]]:
    result: dict[int, list[SourceElement]] = defaultdict(list)
    for item in items:
        result[item.page].append(item)
    return {item_page: tuple(values) for item_page, values in result.items()}


def page(item: dict[str, Any]) -> int:
    virtual = item.get("_advertified_virtual_page")
    if isinstance(virtual, int):
        return virtual
    provenance = item.get("prov") or []
    if provenance and isinstance(provenance[0], dict):
        return int(provenance[0].get("page_no", 1))
    return 1


def locator_prefix(item: dict[str, Any], fallback: str) -> str:
    value = item.get("_advertified_locator_prefix")
    return value if isinstance(value, str) and value else fallback


def position_json(value: Any) -> str | None:
    if not value:
        return None
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def number(value: Any) -> float | None:
    return float(value) if isinstance(value, (int, float)) else None
