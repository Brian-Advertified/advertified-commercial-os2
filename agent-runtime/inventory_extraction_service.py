"""Generic structure-first projection of Docling documents into inventory rows."""

from __future__ import annotations

import re
from collections import defaultdict
from typing import Iterable

from inventory_docling_embedded_images import (
    ImageConverter,
    expand_embedded_images,
)
from inventory_docling_mixed_cells import project_mixed_rate_row, project_unrepresented_rate_cells
from inventory_docling_numeric_matrix import is_numeric_rate_matrix
from inventory_docling_products import project_text_products
from inventory_docling_rates import (
    MONEY_PATTERN, is_rate_header, is_table_rate, page_rates,
    project_text_rates, table_rate_variants,
)
from inventory_extraction_contracts import (
    ExtractedRow,
    InventoryProjectionRequest,
    InventoryProjectionResponse,
)
from inventory_docling_structure import (
    SourceElement,
    TableCell,
    by_page,
    page,
    read_cells,
    read_texts,
    source_elements,
    unwrap_document,
)

_KEY_VALUE_FIELDS = {
    "site number": "productcode",
    "site no": "productcode",
    "product code": "productcode",
    "product name": "name",
    "size": "size",
    "dimensions": "size",
    "illuminated": "illumination",
    "production": "productioncost",
    "production print flight": "productioncost",
    "gps coordinate": "__coordinates__",
    "gps coordinates": "__coordinates__",
    "coordinates": "__coordinates__",
    "audience reach": "audiencereach",
    "impacts": "audienceimpressions",
    "availability": "availability",
    "ad unit": "placement",
}
_COORDINATES = re.compile(
    r"^\s*(-?\d{1,2}(?:\.\d+)?)\s*[, ]\s*(-?\d{1,3}(?:\.\d+)?)\s*$"
)

def project_inventory_with_embedded_images(
    request: InventoryProjectionRequest,
    converter: ImageConverter | None = None,
) -> InventoryProjectionResponse:
    document = unwrap_document(request.provider_document)
    expansion = expand_embedded_images(document, converter)
    expanded_request = request.model_copy(
        update={"provider_document": expansion.document}
    )
    response = project_inventory(expanded_request)
    return response.model_copy(
        update={"warnings": expansion.warnings + response.warnings}
    )

def project_inventory(request: InventoryProjectionRequest) -> InventoryProjectionResponse:
    document = unwrap_document(request.provider_document)
    texts = read_texts(document)
    pages = by_page(texts)
    table_pages = {page(item) for item in document.get("tables", [])}
    inherited = _inherited_sections(pages, table_pages)
    rate_document = any(is_rate_header(item.text) for item in texts)
    rows: list[ExtractedRow] = []
    warnings: list[str] = []

    for table_number, table in enumerate(document.get("tables", []), start=1):
        cells = read_cells(table, table_number)
        if not cells:
            warnings.append(f"Table {table_number} has no retained cells.")
            continue
        table_page = page(table)
        projected = _project_key_value(
            cells, table_number, table_page, pages.get(table_page, ()),
            inherited.get(table_page)
        )
        if projected is None:
            projected = _project_grid(
                cells, pages.get(table_page, ()),
                inherited.get(table_page), rate_document,
            )
        if not projected:
            warnings.append(
                f"Table {table_number} retained evidence but produced no inventory row.")
        rows.extend(projected)

    rows.extend(project_text_rates(pages, rows))
    rows.extend(project_text_products(pages, rows))
    picture_count = len(document.get("pictures", []))
    if picture_count:
        warnings.append(
            f"{picture_count} picture blocks were retained but not interpreted.")
    if not rows:
        warnings.append(
            "The retained Docling document produced no inventory rows.")
    return InventoryProjectionResponse(
        rows=tuple(row.model_copy(update={"number": index}) for index, row in enumerate(rows, 1)),
        source_elements=source_elements(document, texts),
        warnings=tuple(warnings),
    )

def _project_key_value(
    cells: tuple[TableCell, ...],
    table_number: int,
    page: int,
    page_texts: tuple[SourceElement, ...],
    inherited_section: tuple[str, str] | None,
) -> list[ExtractedRow] | None:
    grid = _dense_grid(cells)
    marked_header = any(
        cell.is_header and cell.row == min(grid) for cell in cells
    )
    columns = sorted({column for row in grid.values() for column in row})
    if len(columns) < 2 or len(columns) % 2 or columns != list(range(columns[-1] + 1)):
        return None
    pairs = [
        (left, right)
        for row in grid.values()
        for column in columns[::2]
        if (left := row.get(column)) is not None
        and (right := row.get(column + 1)) is not None
        and left.text and right.text
    ]
    is_key_like = lambda left, right: bool(_field(left.text) or _field(right.text) or is_rate_header(left.text) or is_rate_header(right.text) or left.text.rstrip().endswith(":") or right.text.rstrip().endswith(":"))
    key_like = sum(is_key_like(left, right) for left, right in pairs)
    first_key_like = sum(is_key_like(left, right) for left, right in pairs if left.row == min(grid))
    sparse_rates = len(columns) == 2 and len(pairs) >= 6 and sum(bool(MONEY_PATTERN.search(right.text)) for _, right in pairs) * 3 <= len(pairs)
    required = 2 if sparse_rates else max(2, len(pairs) // 2) if marked_header or len(columns) > 2 else 1
    if len(pairs) < 2 or key_like < required or (marked_header and len(columns) > 2 and first_key_like < 2):
        return None
    recognized = [
        (((left_field := _field(left.text) or ("rate" if is_rate_header(left.text) else None)) or (right_field := _field(right.text) or ("rate" if is_rate_header(right.text) else None)) or _field_key(left.text)), left if left_field or not right_field else right, right if left_field or not right_field else left)
        for left, right in pairs
    ]

    values: dict[str, str] = {}
    locators: dict[str, str] = {}
    variants = []
    for field, label, value in recognized:
        found = table_rate_variants(
            label.text, (label.locator,), value
        )
        if found:
            variants.extend(found)
        else:
            _assign_key_value(field, value, values, locators)
    _add_page_context(values, locators, page_texts, inherited_section)
    variants.extend(page_rates(page_texts))
    if variants:
        values["rate"] = variants[0].raw_value
        locators["rate"] = variants[0].source_locator
    locator = min(cells, key=lambda cell: (cell.row, cell.column)).locator
    return [ExtractedRow(
        number=1,
        locator=locator,
        values=values,
        extraction_method="KEY_VALUE",
        field_locators=locators,
        rate_variants=variants or None,
    )]

def _assign_key_value(
    field: str,
    value: TableCell,
    values: dict[str, str],
    locators: dict[str, str],
) -> None:
    if not value.text:
        return
    if field != "__coordinates__":
        values[field] = value.text
        locators[field] = value.locator
        return
    match = _COORDINATES.match(value.text.replace(";", ","))
    if match:
        values["latitude"], values["longitude"] = match.groups()
        locators["latitude"] = locators["longitude"] = value.locator
        return
    values["coordinates"] = value.text
    locators["coordinates"] = value.locator

def _add_page_context(
    values: dict[str, str],
    locators: dict[str, str],
    page_texts: tuple[SourceElement, ...],
    inherited_section: tuple[str, str] | None,
) -> None:
    heading = _page_heading(page_texts)
    if heading:
        values.setdefault("name", heading.text)
        locators.setdefault("name", heading.locator)
    caption = _page_caption(page_texts)
    if caption:
        values["captioncontext"] = caption.text
        locators["captioncontext"] = caption.locator
    if inherited_section:
        values["sectioncontext"] = inherited_section[0]
        locators["sectioncontext"] = inherited_section[1]

def _project_grid(
    cells: tuple[TableCell, ...],
    page_texts: tuple[SourceElement, ...],
    inherited_section: tuple[str, str] | None,
    rate_document: bool,
) -> list[ExtractedRow]:
    grid = _dense_grid(cells)
    header_rows, data_start = _grid_header_rows(grid, cells)
    headers = _column_headers(grid, header_rows)
    descriptor = min((column for column, (header, _) in headers.items()
                      if not is_rate_header(header)), default=None)
    table_has_money = any(MONEY_PATTERN.search(cell.text) for cell in cells)
    numeric_matrix = is_numeric_rate_matrix(grid, rate_document)
    heading = _page_heading(page_texts)
    section = ((heading.text, heading.locator) if heading
               else inherited_section)
    output = []
    for number in sorted(row for row in grid if row >= data_start):
        mixed = project_mixed_rate_row(grid[number], headers, section)
        if mixed:
            output.extend(mixed)
            continue
        projected = _project_grid_row(
            grid[number], headers, descriptor, table_has_money,
            numeric_matrix, page_texts, inherited_section,
        )
        if projected is not None:
            output.append(projected)
    used = {variant.source_locator for item in output for variant in (item.rate_variants or ())}
    output.extend(project_unrepresented_rate_cells(cells, headers, section, used, {id(cell) for row in grid.values() for cell in row.values()}))
    return output

def _project_grid_row(
    row: dict[int, TableCell],
    headers: dict[int, tuple[str, tuple[str, ...]]],
    descriptor: int | None,
    table_has_money: bool,
    numeric_matrix: bool,
    page_texts: tuple[SourceElement, ...],
    inherited_section: tuple[str, str] | None,
) -> ExtractedRow | None:
    values: dict[str, str] = {}
    locators: dict[str, str] = {}
    variants = []
    row_currency = next((
        cell.text.strip().upper() for column, cell in row.items()
        if _normal(headers.get(column, ("", ()))[0]) == "currency"
        and re.fullmatch(r"[A-Za-z]{3}", cell.text.strip())
    ), None)
    for column, cell in dict((item[1].locator, item) for item in sorted(row.items())).values():
        if not cell.text:
            continue
        header, header_locators = headers.get(
            column, (f"column_{column + 1}", ())
        )
        field = _field(header) or (
            "name" if (table_has_money or numeric_matrix) and (
                cell.is_row_header or column == descriptor
            ) else _field_key(header)
        )
        found = () if (
            column == descriptor and not is_rate_header(header)
            and not (len(MONEY_PATTERN.findall(cell.text)) == 1 and is_table_rate(header, cell.text))
        ) else table_rate_variants(
            header, header_locators, cell, row_currency,
            force_numeric=numeric_matrix and column != descriptor,
        )
        if found:
            variants.extend(found)
            continue
        values[field] = cell.text
        locators[field] = cell.locator
    if variants:
        values["rate"] = variants[0].raw_value
        locators["rate"] = variants[0].source_locator
    section = _page_heading(page_texts)
    if section:
        values.setdefault("sectioncontext", section.text)
        locators.setdefault("sectioncontext", section.locator)
    elif inherited_section:
        values.setdefault("sectioncontext", inherited_section[0])
        locators.setdefault("sectioncontext", inherited_section[1])
    if not ({"productcode", "name"} & values.keys() or variants):
        return None
    first = min(row.values(), key=lambda cell: cell.column)
    return ExtractedRow(
        number=1, locator=first.locator, values=values,
        extraction_method="TABULAR", field_locators=locators,
        rate_variants=tuple(variants) or None,
    )

def _grid_header_rows(
    grid: dict[int, dict[int, TableCell]], cells: tuple[TableCell, ...]
) -> tuple[list[int], int]:
    first_row = min(grid)
    marked = {cell.row for cell in cells if cell.is_header and not any(MONEY_PATTERN.search(item.text) for item in cells if item.row == cell.row)}
    if first_row in marked:
        top_headers = []
        row_number = first_row
        while row_number in marked:
            top_headers.append(row_number)
            row_number += 1
        headers = _column_headers(grid, top_headers)
        priced = [number for number, row in grid.items()
                  if number >= row_number and any(is_table_rate(
                      headers.get(column, ("", ()))[0], cell.text
                  ) for column, cell in row.items())]
        data_start = min(priced) if priced else row_number
        return sorted(set(top_headers) | {data_start - 1}), data_start
    money_rows = [
        row_number
        for row_number, row in grid.items()
        if any(MONEY_PATTERN.search(cell.text) for cell in row.values())
    ]
    if money_rows:
        data_start = min(money_rows)
        if data_start == first_row and not marked:
            return [], first_row
        header_rows = {row for row in marked if row < data_start}
        return sorted(header_rows | {data_start - 1}), data_start
    header_rows = sorted(marked) or [first_row]
    return header_rows, max(header_rows) + 1

def _dense_grid(cells: Iterable[TableCell]) -> dict[int, dict[int, TableCell]]:
    grid: dict[int, dict[int, TableCell]] = defaultdict(dict)
    for cell in cells:
        for row in range(cell.row, cell.row_end):
            for column in range(cell.column, cell.column_end):
                grid[row].setdefault(column, cell)
    return dict(grid)

def _column_headers(
    grid: dict[int, dict[int, TableCell]], header_rows: list[int]
) -> dict[int, tuple[str, tuple[str, ...]]]:
    columns = {column for row in grid.values() for column in row}
    result = {}
    for column in columns:
        cells = [grid[row][column] for row in header_rows if column in grid.get(row, {})]
        unique = list(dict.fromkeys(cell.text for cell in cells if cell.text))
        result[column] = (" / ".join(unique) or f"column_{column + 1}",
                          tuple(dict.fromkeys(cell.locator for cell in cells)))
    return result

def _page_heading(texts: tuple[SourceElement, ...]) -> SourceElement | None:
    candidates = [
        item for item in texts
        if item.label == "section_header"
        and not item.text.casefold().startswith("site info")
    ]
    return max(candidates, key=lambda item: item.top or 0) if candidates else None

def _page_caption(texts: tuple[SourceElement, ...]) -> SourceElement | None:
    return next(
        (
            item for item in texts
            if item.label == "caption" and item.text.strip()
        ),
        None,
    )

def _inherited_sections(
    pages: dict[int, tuple[SourceElement, ...]], table_pages: set[int]
) -> dict[int, tuple[str, str]]:
    current = None
    result = {}
    for page in sorted(pages):
        if page not in table_pages:
            headings = [item for item in pages[page] if item.label == "section_header"]
            short = [item for item in headings if len(item.text) <= 50]
            if short:
                current = (short[-1].text, short[-1].locator)
        if current:
            result[page] = current
    return result

def _field(label: str) -> str | None:
    return _KEY_VALUE_FIELDS.get(_normal(label))

def _field_key(label: str) -> str:
    return _normal(label).replace(" ", "")

def _normal(value: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9]+", " ", value.casefold()).split())


