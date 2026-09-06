"""Current-page monetary rows under explicit generic commercial context."""

from __future__ import annotations

import re
from collections.abc import Callable

from inventory_docling_structure import SourceElement
from inventory_extraction_contracts import ExtractedRateVariant, ExtractedRow


def project_remaining_context_money(
    texts: tuple[SourceElement, ...],
    used: set[str],
    context: SourceElement | None,
    is_money_value: Callable[[str], bool],
    money_pattern: re.Pattern[str],
) -> list[ExtractedRow]:
    if context is None:
        return []
    output = []
    for item in texts:
        if item.locator in used or not is_money_value(item.text):
            continue
        match = money_pattern.search(item.text)
        if match is None:
            continue
        raw_value = match.group(0).strip()
        variant = ExtractedRateVariant(
            raw_value=raw_value,
            source_locator=item.locator,
            header_hierarchy=context.text,
            header_locators=(context.locator,),
            position_json=item.position_json,
            currency="ZAR",
        )
        output.append(ExtractedRow(
            number=1,
            locator=item.locator,
            values={"name": context.text, "rate": raw_value},
            extraction_method="TEXT_RATE",
            field_locators={
                "name": context.locator,
                "rate": item.locator,
            },
            rate_variants=(variant,),
        ))
        used.add(item.locator)
    return output
