"""Independent physical-evidence reader for the inventory corpus.

Source maps are immutable, hash-addressed transcriptions of the physical
PDF/PPTX/XLSX files. This module derives sellable-unit expectations without
using the API candidate projection or Bedrock output.
"""

from __future__ import annotations

import json
from pathlib import Path

from inventory_physical_model import (
    PhysicalDocument,
    deduplicate_units,
    locator_scope,
    normalize,
    table_rows,
)
from inventory_physical_table_units import extract_table_units
from inventory_physical_text_units import (
    extract_catalogue_fallback_units,
    extract_page_card_units,
    extract_presentation_site_units,
    extract_priced_fragment_units,
)


def read_physical_document(path: Path) -> PhysicalDocument:
    payload = json.loads(path.read_text(encoding="utf-8"))
    fragments = payload.get("fragments") or []
    tables = payload.get("tables") or []
    assets = payload.get("assets") or []
    strings: list[str] = []
    locators: set[str] = set()
    scopes: set[str] = set()

    for item in fragments:
        capture(item, "text", strings, locators, scopes)
    for item in assets:
        capture(item, "ocrText", strings, locators, scopes)
    for table in tables:
        locator = str(table.get("locator") or "")
        if locator:
            locators.add(locator)
            scope = locator_scope(locator)
            if scope:
                scopes.add(scope)
        for row in table_rows(table):
            strings.extend(value for value in row if value)

    table_units = extract_table_units(tables, fragments)
    page_units = extract_page_card_units(fragments)
    presentation_units = extract_presentation_site_units(
        fragments,
        payload.get("format"),
    )
    stronger_units = table_units + page_units + presentation_units
    covered_scopes = {
        unit.scope for unit in stronger_units if unit.scope
    }
    priced_units = [
        unit for unit in extract_priced_fragment_units(fragments)
        if not unit.scope or unit.scope not in covered_scopes
    ]
    units = deduplicate_units(stronger_units + priced_units)
    if not units:
        units = deduplicate_units(
            extract_catalogue_fallback_units(fragments)
        )

    raw_text = "\n".join(strings)
    return PhysicalDocument(
        source_hash=str(payload.get("sourceHash") or ""),
        file_name=str(payload.get("relativePath") or path.name),
        document_format=str(payload.get("format") or ""),
        locators=frozenset(locators),
        raw_text=raw_text,
        normalized_text=normalize(raw_text),
        units=tuple(units),
        pages_or_slides=tuple(sorted(scopes)),
    )


def capture(
    item: dict,
    text_key: str,
    strings: list[str],
    locators: set[str],
    scopes: set[str],
) -> None:
    text = str(item.get(text_key) or "").strip()
    locator = str(item.get("locator") or "")
    if text:
        strings.append(text)
    if locator:
        locators.add(locator)
        scope = locator_scope(locator)
        if scope:
            scopes.add(scope)


__all__ = [
    "PhysicalDocument",
    "read_physical_document",
]
