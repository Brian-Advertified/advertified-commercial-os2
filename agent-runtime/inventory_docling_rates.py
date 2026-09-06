"""Generic rate evidence detection within Docling text and table structures."""

from __future__ import annotations

import re

from inventory_docling_context_rates import project_remaining_context_money
from inventory_docling_structure import SourceElement, TableCell
from inventory_extraction_contracts import ExtractedRateVariant, ExtractedRow

RATE_LABELS = frozenset({
    "rate", "rates", "ratecard", "mediarate", "monthlyrate",
    "unitrate", "adrate", "price", "prices", "pricing",
    "cost", "costs", "cpm", "cpl", "investment", "value",
    "spot", "spots",
})
NON_PRODUCT_RATE_LABELS = frozenset({
    "production", "installation", "printing", "flighting",
})
NON_RATE_SECTION_LABELS = frozenset({
    "invoice", "vat", "tax", "total", "subtotal",
})
INLINE_COMMERCIAL_LABELS = frozenset({
    "rate", "price", "cost", "fee", "budget", "spend", "package", "packages",
    "post", "platform", "site", "sponsorship", "takeover", "advertising",
})
ROUTE_LABELS = frozenset({
    "road", "route", "street", "drive", "exit", "entrance", "cnr",
})
AMOUNT_PATTERN = (
    r"(?:"
    r"\d{1,3}(?:[ \u00a0]\d{3})+(?:[.,]\s?\d{1,2})?"
    r"|\d{1,3}(?:,\d{3})+(?:\.\s?\d{1,2})?"
    r"|\d{1,3}(?:\.\d{3})+(?:,\s?\d{1,2})?"
    r"|\d+(?:[.,]\s?\d{1,2})?"
    r")"
)
MONEY_PATTERN = re.compile(
    rf"(?<![A-Za-z0-9])R\s*{AMOUNT_PATTERN}(?![A-Za-z0-9])",
    re.I,
)
MONEY_ONLY_PATTERN = re.compile(rf"^R\s*{AMOUNT_PATTERN}$", re.I)
NUMERIC_ONLY_PATTERN = re.compile(rf"^\s*{AMOUNT_PATTERN}\s*$")
BUYING_UNITS = frozenset({
    "pm", "each", "per day", "per week", "per month", "per spot",
    "per insertion", "per campaign", "per annum", "per slot", "per slot per month", "per slot monthly", "pa",
    "p d", "p day", "p week", "p month", "p post",
    "p post p platform", "p post per platform",
    "per post", "per post per platform", "per platform",
})

def is_rate_header(header: str) -> bool:
    return _is_rate_label(header)
def is_product_rate(header: str, value: str) -> bool:
    return bool(MONEY_PATTERN.search(value)) and is_rate_header(header)


def is_table_rate(header: str, value: str) -> bool:
    tokens = set(_normal(header).split())
    if tokens & NON_PRODUCT_RATE_LABELS:
        return False
    money = list(MONEY_PATTERN.finditer(value))
    if money:
        return bool(
            is_rate_header(header) or len(money) > 1 or _is_money_value(value)
            or _is_inline_commercial_rate(value) or (money[0].start() == 0 and re.search(r"\b(?:mon(?:day)?|tue(?:sday)?|wed(?:nesday)?|thu(?:rsday)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\b", header, re.I))
        )
    leaf_header = header.rsplit("/", 1)[-1]
    return bool(
        NUMERIC_ONLY_PATTERN.fullmatch(value)
        and is_rate_header(leaf_header)
    )


def table_rate_variants(
    header: str,
    header_locators: tuple[str, ...],
    value: TableCell,
    currency: str | None = None,
    force_numeric: bool = False,
) -> tuple[ExtractedRateVariant, ...]:
    if not is_table_rate(header, value.text) and not (
        force_numeric and NUMERIC_ONLY_PATTERN.fullmatch(value.text)
    ):
        return ()
    matches = list(MONEY_PATTERN.finditer(value.text))
    raw_values = [match.group(0).strip() for match in matches] or [value.text]
    return tuple(ExtractedRateVariant(
        raw_value=raw_value,
        source_locator=value.locator,
        header_hierarchy=header,
        header_locators=header_locators,
        position_json=value.position_json,
        currency="ZAR" if matches else currency,
    ) for raw_value in raw_values)


def page_rates(
    texts: tuple[SourceElement, ...],
    include_section_labels: bool = False,
) -> tuple[ExtractedRateVariant, ...]:
    money = [item for item in texts if _is_money_value(item.text)]
    labels = [
        item for item in texts
        if item.centre_y is not None and (
            _is_page_rate_label(item.text)
            or _is_inline_commercial_rate(item.text)
            or include_section_labels and (
                _is_section_rate_label(item)
                or re.search(r"\b(?:article|post|newsletter|banner|homepage|social)s?\b", _normal(item.text))
            )
        )
    ]
    matches = _match_rate_values(labels, money, texts)
    variants = []
    for label in labels:
        matched = matches.get(label.locator)
        sources = [label] if matched is None else [label, matched]
        for source in dict.fromkeys(sources):
            source_matches = list(MONEY_PATTERN.finditer(source.text))
            for match in source_matches:
                variants.append(ExtractedRateVariant(
                    raw_value=(source.text.strip() if source is not label
                               and len(source_matches) == 1
                               else match.group(0).strip()),
                    source_locator=source.locator,
                    header_hierarchy=_rate_header(label, source),
                    header_locators=(label.locator,),
                    position_json=source.position_json,
                    currency="ZAR",
                    buying_unit=_buying_unit(source.text, match),
                ))
    variants.extend(_adjacent_unit_rates(
        texts, {item.source_locator for item in variants}
    ))
    return tuple(sorted(
        variants,
        key=lambda item: (
            item.source_locator, item.header_hierarchy, item.raw_value
        ),
    ))


def _adjacent_unit_rates(
    texts: tuple[SourceElement, ...],
    used: set[str],
) -> list[ExtractedRateVariant]:
    output = []
    for index, item in enumerate(texts):
        if item.locator in used or not MONEY_ONLY_PATTERN.fullmatch(item.text):
            continue
        nearby = sorted((candidate for candidate in texts if candidate.page == item.page), key=lambda candidate: abs(texts.index(candidate) - index))
        unit_item = next((
            (candidate, unit)
            for candidate in nearby
            if candidate.page == item.page
            for unit in sorted(BUYING_UNITS, key=len, reverse=True)
            if _normal(candidate.text).startswith(unit)
        ), None)
        if unit_item is None:
            continue
        label, unit = unit_item
        output.append(ExtractedRateVariant(
            raw_value=item.text,
            source_locator=item.locator,
            header_hierarchy=label.text,
            header_locators=(label.locator,),
            position_json=item.position_json,
            currency="ZAR",
            buying_unit=unit,
        ))
    return output


def project_text_rates(
    pages: dict[int, tuple[SourceElement, ...]],
    existing_rows: list[ExtractedRow],
) -> list[ExtractedRow]:
    used = {
        variant.source_locator
        for row in existing_rows for variant in (row.rate_variants or ())
    }
    output = []
    for page_number in sorted(pages):
        texts = pages[page_number]
        heading = _page_heading(texts)
        context = next((item for item in texts if _is_page_rate_label(item.text) and not MONEY_PATTERN.search(item.text)), heading)
        has_page_rate = (
            any(_is_page_rate_label(item.text) for item in texts)
            or any(f"page={page_number};" in locator for locator in used)
        )
        page_variants = page_rates(texts, has_page_rate)
        for variant in page_variants:
            if variant.source_locator in used:
                continue
            unit_heading = _normal(variant.header_hierarchy).startswith(
                tuple(BUYING_UNITS)
            )
            name = heading.text if unit_heading and heading else variant.header_hierarchy
            name_locator = (
                heading.locator if unit_heading and heading
                else variant.header_locators[0]
            )
            values = {"name": name, "rate": variant.raw_value}
            locators = {"name": name_locator, "rate": variant.source_locator}
            if heading and heading.locator != locators["name"]:
                values["sectioncontext"] = heading.text
                locators["sectioncontext"] = heading.locator
            output.append(ExtractedRow(
                number=1, locator=variant.source_locator, values=values,
                extraction_method="TEXT_RATE", field_locators=locators,
                rate_variants=(variant,),
            ))
        used.update(variant.source_locator for variant in page_variants)
        output.extend(project_remaining_context_money(texts, used, context, _is_money_value, MONEY_PATTERN))
    return output


def _page_heading(texts: tuple[SourceElement, ...]) -> SourceElement | None:
    candidates = [
        item for item in texts if item.label == "section_header"
        and not item.text.casefold().startswith("site info")
        and _normal(item.text) not in {"location", "rate", "rates", "price"}
        and not MONEY_PATTERN.search(item.text)
    ]
    return max(candidates, key=lambda item: item.top or 0) if candidates else None


def _is_money_value(value: str) -> bool:
    if MONEY_ONLY_PATTERN.fullmatch(value):
        return True
    match = MONEY_PATTERN.search(value)
    if not match or value[:match.start()].strip():
        return False
    suffix = _normal(value[match.end():]); return suffix in BUYING_UNITS or bool(set(suffix.split()) & INLINE_COMMERCIAL_LABELS)


def _buying_unit(value: str, match: re.Match[str]) -> str | None:
    suffix = _normal(value[match.end():])
    return suffix if suffix in BUYING_UNITS else None


def _rate_header(label: SourceElement, source: SourceElement) -> str:
    text = label.text.strip()
    without_money = " ".join(
        MONEY_PATTERN.sub("", text).strip(" :-").split()
    )
    return without_money or text


def _match_rate_values(
    labels: list[SourceElement],
    money: list[SourceElement],
    ordered_items: tuple[SourceElement, ...],
) -> dict[str, SourceElement]:
    pairs = []
    for label in labels:
        for value in money:
            distance = _rate_alignment(label, value)
            if value.locator != label.locator and distance is not None:
                pairs.append((
                    distance,
                    label.locator,
                    value.locator,
                    label,
                    value,
                ))
    matches: dict[str, SourceElement] = {}
    used_values: set[str] = set()
    for _, _, _, label, value in sorted(pairs):
        if label.locator in matches or value.locator in used_values:
            continue
        matches[label.locator] = value
        used_values.add(value.locator)
    _match_by_reading_order(
        labels, money, ordered_items, matches, used_values
    )
    return matches


def _match_by_reading_order(
    labels: list[SourceElement],
    money: list[SourceElement],
    ordered_items: tuple[SourceElement, ...],
    matches: dict[str, SourceElement],
    used_values: set[str],
) -> None:
    positions = {
        item.locator: index for index, item in enumerate(ordered_items)
    }
    label_positions = sorted(
        positions[label.locator] for label in labels
        if label.locator in positions
    )
    for label in labels:
        if label.locator in matches or label.locator not in positions:
            continue
        start = positions[label.locator]
        next_label = next(
            (position for position in label_positions if position > start),
            len(ordered_items),
        )
        candidates = [
            value for value in money
            if value.locator not in used_values
            and value.page == label.page
            and 0 < positions.get(value.locator, -1) - start <= 3
            and positions[value.locator] < next_label
        ]
        if not candidates:
            continue
        value = min(candidates, key=lambda item: positions[item.locator])
        matches[label.locator] = value
        used_values.add(value.locator)


def _rate_alignment(
    label: SourceElement,
    value: SourceElement,
) -> float | None:
    if label.centre_y is None or value.centre_y is None:
        return None
    vertical_gap = abs(value.centre_y - label.centre_y)
    if vertical_gap <= 6:
        if (
            label.left is not None
            and value.left is not None
            and value.left < label.left
        ):
            return None
        return vertical_gap
    if vertical_gap > 40 or None in (
        label.left,
        label.right,
        value.left,
        value.right,
    ):
        return None
    horizontal_gap = max(
        0.0,
        value.left - label.right,
        label.left - value.right,
    )
    return vertical_gap + horizontal_gap if horizontal_gap <= 40 else None


def _is_inline_commercial_rate(value: str) -> bool:
    matches = list(MONEY_PATTERN.finditer(value))
    if not matches:
        return False
    first = matches[0]
    prefix = value[:first.start()].strip()
    tokens = set(_normal(prefix).split())
    amount_digits = re.sub(r"\D", "", first.group(0))
    if tokens & ROUTE_LABELS and len(amount_digits) <= 3:
        return False
    if tokens & NON_PRODUCT_RATE_LABELS:
        return False
    if not prefix:
        return len(matches) > 1 or bool(set(_normal(value[first.end():]).split()) & INLINE_COMMERCIAL_LABELS)
    structured = ":" in prefix or bool(re.search(r"\s{2,}$", value[:first.start()]))
    return (
        len(tokens) <= 4 or structured
        or bool(tokens & INLINE_COMMERCIAL_LABELS)
    )

def _is_section_rate_label(item: SourceElement) -> bool:
    normalized = _normal(item.text)
    tokens = set(normalized.split())
    return (
        item.label == "section_header"
        and 0 < len(tokens) <= 8
        and not tokens & NON_RATE_SECTION_LABELS
        and re.search(r"\b(?:19|20)\d{2}\b", normalized) is None
        and not MONEY_PATTERN.search(item.text)
    )

def _is_page_rate_label(value: str) -> bool:
    normalized = _normal(value)
    maximum_words = 20 if MONEY_PATTERN.search(value) else 6
    return (
        len(normalized.split()) <= maximum_words
        and re.search(r"\b(?:19|20)\d{2}\b", normalized) is None
        and (_is_rate_label(value) or "packages" in normalized.split())
    )


def _is_rate_label(value: str) -> bool:
    normalized = _normal(value); tokens = set(normalized.split())
    count_label = re.search(r"\b(?:no|number) of (?:spots?|units?|insertions?)\b|\b(?:spot|unit|insertion) count\b|\bquantity\b", normalized)
    return count_label is None and bool(tokens & RATE_LABELS) and not bool(
        tokens & NON_PRODUCT_RATE_LABELS
    )


def _normal(value: str) -> str:
    return " ".join(
        re.sub(r"[^a-z0-9]+", " ", value.casefold()).split()
    )
