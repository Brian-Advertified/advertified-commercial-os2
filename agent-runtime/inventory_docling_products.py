"""Generic current-page inventory product detection from Docling text."""

from __future__ import annotations

import re

from inventory_docling_structure import SourceElement
from inventory_extraction_contracts import ExtractedRow


_CODE = re.compile(r"^(?=.{4,20}$)(?=.*\d)[A-Z]{2,8}[A-Z0-9-]*$")
_IMAGE_CONTEXT = re.compile(
    r"\b(?:inventory|site|location|format|rate|price|media|billboard|screen|panel|placement)\b",
    re.I,
)


def project_text_products(
    pages: dict[int, tuple[SourceElement, ...]],
    existing_rows: list[ExtractedRow],
) -> list[ExtractedRow]:
    occupied = {
        page_number
        for page_number in pages
        if any(f"page={page_number};" in row.locator for row in existing_rows)
    }
    output = []
    for page_number in sorted(pages):
        if page_number in occupied:
            continue
        row = _page_product(pages[page_number])
        if row is not None:
            output.append(row)
    return output


def _page_product(
    texts: tuple[SourceElement, ...],
) -> ExtractedRow | None:
    image_page = any("picture=" in item.locator for item in texts)
    if image_page and not any(_IMAGE_CONTEXT.search(item.text) for item in texts):
        return None
    for index, item in enumerate(texts):
        combined = _combined_product(item)
        if combined is not None:
            name, code = combined
            return _row(item, item, name, code)
        if not _is_code(item.text) or index == 0:
            continue
        name = texts[index - 1]
        if _is_product_name(name.text):
            return _row(name, item, name.text.strip(), item.text.strip())
    return None


def _combined_product(item: SourceElement) -> tuple[str, str] | None:
    parts = [part.strip() for part in item.text.split("|") if part.strip()]
    if len(parts) < 2 or not _is_code(parts[-1]):
        return None
    name = " | ".join(parts[:-1])
    return (name, parts[-1]) if _is_product_name(name) else None


def _is_code(value: str) -> bool:
    raw = value.strip()
    parts = raw.split()
    compact = "".join(parts)
    return (
        bool(parts) and len(parts) <= 2 and (len(parts) == 1 or len(parts[0]) <= 5 and any(character.isdigit() for character in parts[1]))
        and raw == raw.upper() and bool(_CODE.fullmatch(compact))
        and not re.fullmatch(r"(?:19|20)\d{2}", compact)
        and not re.fullmatch(r"(?:SEM|LSM)\d.*", compact)
    )


def _is_product_name(value: str) -> bool:
    text = value.strip()
    return (
        1 < len(text) <= 120
        and any(character.isalpha() for character in text)
        and not re.fullmatch(r"[\d\s.,%+-]+", text)
    )


def _row(
    name: SourceElement,
    code: SourceElement,
    name_value: str,
    code_value: str,
) -> ExtractedRow:
    return ExtractedRow(
        number=1,
        locator=name.locator,
        values={"name": name_value, "productcode": code_value},
        extraction_method="TEXT_PRODUCT",
        field_locators={
            "name": name.locator,
            "productcode": code.locator,
        },
    )
