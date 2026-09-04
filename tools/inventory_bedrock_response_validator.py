"""Deterministic validation for Bedrock inventory certification responses."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import Any

NUMBER = re.compile(r"(?<![A-Za-z])[-+]?\d+(?:[.,]\d+)?")
DATE = re.compile(r"\b(?:20\d{2}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]20\d{2}|\d{1,2}\s+[A-Za-z]{3,9}\s+20\d{2})\b", re.I)
UNAVAILABLE = re.compile(r"(?i)\b(?:not\s+available|unavailable|blackout|booked|conflict)\b")
LIMITED = re.compile(r"(?i)\b(?:limited|subject\s+to\s+availability)\b")
RATE_BASIS = re.compile(r"(?i)\b(?:cpm|cpc|cpl|cpa|per\s+(?:spot|day|week|month|insert|post|platform)|package\s+(?:rate|cost)|spot\s+rate)\b")
AMBIGUITY = re.compile(r"(?i)\b(?:ambiguous|truncated|unclear|review|required|unknown)\b")


@dataclass(frozen=True)
class ValidationResult:
    passed: bool
    failures: tuple[str, ...]
    product_count: int
    non_product_fact_count: int
    covered_source_row_count: int


def validate_response(
    packet: dict[str, Any],
    response: dict[str, Any],
    allowed_channels: set[str],
    allowed_product_types: set[str],
) -> ValidationResult:
    failures: list[str] = []
    input_payload = packet["input"]
    rows = input_payload["physicalRows"]
    rows_by_id = {str(row["physicalRowId"]): row for row in rows}
    expected_ids = set(rows_by_id)
    if response.get("sourceHash") != packet.get("sourceHash"):
        failures.append("SOURCE_HASH_MISMATCH")
    products = response.get("products")
    non_products = response.get("nonProductFacts")
    if not isinstance(products, list) or not products:
        failures.append("PRODUCTS_MISSING")
        products = []
    if not isinstance(non_products, list):
        failures.append("NON_PRODUCT_FACTS_MISSING")
        non_products = []

    assigned: list[str] = []
    for index, product in enumerate(products):
        prefix = f"PRODUCT_{index + 1}"
        if not isinstance(product, dict):
            failures.append(prefix + "_NOT_OBJECT")
            continue
        source_ids = product.get("sourceRowIds")
        if not isinstance(source_ids, list) or not source_ids:
            failures.append(prefix + "_SOURCE_ROWS_MISSING")
            source_ids = []
        source_ids = [str(value) for value in source_ids]
        assigned.extend(source_ids)
        product_rows = [rows_by_id[value] for value in source_ids if value in rows_by_id]
        unknown_ids = sorted(set(source_ids) - expected_ids)
        if unknown_ids:
            failures.append(prefix + "_UNKNOWN_SOURCE_ROWS")
        validate_product(
            product,
            product_rows,
            prefix,
            allowed_channels,
            allowed_product_types,
            failures,
        )

    for index, item in enumerate(non_products):
        prefix = f"NON_PRODUCT_{index + 1}"
        if not isinstance(item, dict):
            failures.append(prefix + "_NOT_OBJECT")
            continue
        source_id = str(item.get("sourceRowId") or "")
        if not source_id:
            failures.append(prefix + "_SOURCE_ROW_MISSING")
        else:
            assigned.append(source_id)
            if source_id not in expected_ids:
                failures.append(prefix + "_UNKNOWN_SOURCE_ROW")
        reason = str(item.get("reason") or "").strip()
        if not reason:
            failures.append(prefix + "_REASON_MISSING")
        attached = item.get("attachToProductIndex")
        if attached is not None and (
            not isinstance(attached, int)
            or attached < 0
            or attached >= len(products)
        ):
            failures.append(prefix + "_INVALID_PRODUCT_INDEX")

    counts: dict[str, int] = {}
    for source_id in assigned:
        counts[source_id] = counts.get(source_id, 0) + 1
    missing = expected_ids - set(assigned)
    extra = set(assigned) - expected_ids
    repeated = {value for value, count in counts.items() if count > 1}
    if missing:
        failures.append("SOURCE_ROWS_NOT_CLASSIFIED")
    if extra:
        failures.append("EXTRA_SOURCE_ROWS_CLASSIFIED")
    if repeated:
        failures.append("SOURCE_ROWS_CLASSIFIED_MORE_THAN_ONCE")

    return ValidationResult(
        passed=not failures,
        failures=tuple(sorted(set(failures))),
        product_count=len(products),
        non_product_fact_count=len(non_products),
        covered_source_row_count=len(expected_ids - missing),
    )


def validate_product(
    product: dict[str, Any],
    rows: list[dict[str, Any]],
    prefix: str,
    allowed_channels: set[str],
    allowed_product_types: set[str],
    failures: list[str],
) -> None:
    if not rows:
        failures.append(prefix + "_NO_VALID_SOURCE_ROWS")
        return
    name = str(product.get("name") or "").strip()
    description = str(product.get("description") or "").strip()
    geography = product.get("geography")
    if not name:
        failures.append(prefix + "_NAME_MISSING")
    if not description:
        failures.append(prefix + "_DESCRIPTION_MISSING")
    channel = str(product.get("channel") or "")
    product_type = str(product.get("productType") or "")
    if allowed_channels and channel not in allowed_channels:
        failures.append(prefix + "_CHANNEL_NOT_ALLOWED")
    if allowed_product_types and product_type not in allowed_product_types:
        failures.append(prefix + "_PRODUCT_TYPE_NOT_ALLOWED")
    source_text = json.dumps(rows, sort_keys=True, default=str)
    if name and not grounded_words(name, source_text):
        failures.append(prefix + "_NAME_NOT_GROUNDED")
    if description and not grounded_numbers(description, source_text):
        failures.append(prefix + "_DESCRIPTION_HAS_UNSUPPORTED_NUMBER")
    if geography not in (None, "") and not grounded_words(str(geography), source_text):
        failures.append(prefix + "_GEOGRAPHY_NOT_GROUNDED")

    rate_source_id = product.get("rateSourceRowId")
    if rate_source_id is not None:
        matching = [row for row in rows if row.get("physicalRowId") == rate_source_id]
        if len(matching) != 1:
            failures.append(prefix + "_RATE_SOURCE_INVALID")
    rate_type = product.get("rateType")
    if rate_type not in (None, "") and not RATE_BASIS.search(source_text):
        failures.append(prefix + "_RATE_TYPE_INFERRED_WITHOUT_BASIS")

    valid_from = product.get("validFrom")
    valid_to = product.get("validTo")
    if valid_from not in (None, "") and str(valid_from) not in source_text and not DATE.search(source_text):
        failures.append(prefix + "_VALID_FROM_NOT_GROUNDED")
    if valid_to not in (None, "") and str(valid_to) not in source_text and not DATE.search(source_text):
        failures.append(prefix + "_VALID_TO_NOT_GROUNDED")

    availability = str(product.get("availability") or "")
    if availability not in {"AVAILABLE", "UNAVAILABLE", "LIMITED"}:
        failures.append(prefix + "_AVAILABILITY_INVALID")
    elif UNAVAILABLE.search(source_text) and availability != "UNAVAILABLE":
        failures.append(prefix + "_UNAVAILABLE_SOURCE_NOT_PRESERVED")
    elif not UNAVAILABLE.search(source_text) and LIMITED.search(source_text):
        if availability != "LIMITED":
            failures.append(prefix + "_LIMITED_SOURCE_NOT_PRESERVED")
    elif not UNAVAILABLE.search(source_text) and not LIMITED.search(source_text):
        if availability != "AVAILABLE":
            failures.append(prefix + "_DEFAULT_AVAILABLE_POLICY_VIOLATED")

    ambiguous = any(row.get("ambiguityCodes") for row in rows)
    notes = product.get("reviewNotes")
    if not isinstance(notes, list):
        failures.append(prefix + "_REVIEW_NOTES_INVALID")
    elif ambiguous and not any(AMBIGUITY.search(str(note)) for note in notes):
        failures.append(prefix + "_AMBIGUITY_NOTE_MISSING")


def grounded_words(value: str, source: str) -> bool:
    words = [
        normalize_word(word)
        for word in re.findall(r"[A-Za-z][A-Za-z0-9&+.-]*", value)
        if len(normalize_word(word)) >= 3
    ]
    if not words:
        return True
    source_words = {
        normalize_word(word)
        for word in re.findall(r"[A-Za-z][A-Za-z0-9&+.-]*", source)
    }
    supported = sum(word in source_words for word in words)
    return supported / len(words) >= 0.55


def grounded_numbers(value: str, source: str) -> bool:
    source_numbers = {normalize_number(match.group(0)) for match in NUMBER.finditer(source)}
    output_numbers = {normalize_number(match.group(0)) for match in NUMBER.finditer(value)}
    return output_numbers.issubset(source_numbers)


def normalize_word(value: str) -> str:
    return "".join(char.lower() for char in value if char.isalnum())


def normalize_number(value: str) -> str:
    return value.replace(" ", "").replace(",", ".").lstrip("+")
