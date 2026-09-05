"""Historical source-grounding and identity comparison for corpus certification."""

from __future__ import annotations

import hashlib
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from inventory_physical_evidence import read_physical_document
from inventory_physical_model import (
    PhysicalDocument,
    PhysicalUnit,
    locator_scope,
    money_fingerprint,
    normalize,
)

ALLOWED_DERIVED_FIELDS = {
    "availability", "channel", "currency", "description", "producttype",
    "ratetype", "suppliername", "productcode", "geography", "name",
}
IDENTITY_FIELDS = {"name", "productCode", "geography", "address"}
COMMERCIAL_FIELDS = {"rate", "rateAmountMinor", "currency", "rateType"}
PHYSICAL_BLOCKING_KEYWORDS = {
    "AMBIG", "DUPLICATE", "GROUND", "SOURCE", "SUPPLIER_IDENTITY",
    "UNSUPPORTED", "RATE_PARSE", "HASH",
}
DMS_COORDINATE_PATTERN = re.compile(
    r"(?P<degrees>\d{1,3})\s*[°º]\s*"
    r"(?P<minutes>\d{1,2})\s*['’]\s*"
    r"(?P<seconds>\d{1,2}(?:[.,]\d+)?)\s*[\"”]?\s*"
    r"(?P<hemisphere>[NSEW])",
    re.IGNORECASE,
)


def evidence_value_supported(
    physical: PhysicalDocument,
    field: str,
    raw: str,
    transformation: str,
) -> bool:
    if text_supported(raw, physical.normalized_text):
        return True
    if field == "currency" and normalize(raw) == "zar":
        return any(
            money_fingerprint(unit.raw_rate).startswith("R")
            for unit in physical.units
            if unit.raw_rate
        )
    if (
        field in {"latitude", "longitude"}
        and transformation.upper() == "PARSE_DECIMAL"
    ):
        return transformed_coordinate_supported(
            physical.raw_text,
            field,
            raw,
        )
    return False


def transformed_coordinate_supported(
    source_text: str,
    field: str,
    raw: str,
) -> bool:
    try:
        expected = float(raw.replace(",", "."))
    except ValueError:
        return False
    hemispheres = {"N", "S"} if field == "latitude" else {"E", "W"}
    for match in DMS_COORDINATE_PATTERN.finditer(source_text):
        hemisphere = match.group("hemisphere").upper()
        if hemisphere not in hemispheres:
            continue
        degrees = float(match.group("degrees"))
        minutes = float(match.group("minutes"))
        seconds = float(match.group("seconds").replace(",", "."))
        value = degrees + minutes / 60 + seconds / 3600
        if hemisphere in {"S", "W"}:
            value *= -1
        if abs(value - expected) <= 0.00001:
            return True
    return False


def equivalent_unit_keys(
    units: tuple[PhysicalUnit, ...],
    matched: PhysicalUnit,
) -> set[str]:
    result = {matched.key}
    matched_code = explicit_code(matched.identity)
    matched_tokens = significant_tokens(matched.identity)
    matched_rate = money_fingerprint(matched.raw_rate)
    for unit in units:
        if unit.key == matched.key:
            continue
        unit_code = explicit_code(unit.identity)
        same_code = bool(
            matched_code and unit_code and matched_code == unit_code
        )
        same_scope_rate = bool(
            unit.scope == matched.scope
            and money_fingerprint(unit.raw_rate) == matched_rate
            and matched_rate
        )
        token_overlap = matched_tokens.intersection(
            significant_tokens(unit.identity)
        )
        same_identity = bool(
            matched_tokens
            and len(token_overlap) / len(matched_tokens) >= 0.7
        )
        if same_code or (same_scope_rate and same_identity):
            result.add(unit.key)
    return result


def explicit_code(value: str) -> str:
    import re

    match = re.search(r"\b[A-Z]{1,6}[ -]?\d{2,7}[A-Z]?\b", value)
    return normalize(match.group(0)) if match else ""


def issue_key(issue: dict[str, Any]) -> str:
    return str(
        issue.get("code")
        or issue.get("issueType")
        or issue.get("fieldName")
        or "BLOCKING"
    )


def is_physical_blocker(issue: dict[str, Any]) -> bool:
    key = issue_key(issue).upper()
    return any(keyword in key for keyword in PHYSICAL_BLOCKING_KEYWORDS)


def candidate_is_physically_grounded(
    candidate: dict[str, Any],
    physical: PhysicalDocument,
    unsupported: list[str],
) -> bool:
    if unsupported:
        return False
    evidence = candidate.get("evidence") or []
    supplier_evidence = [
        item for item in evidence
        if str(item.get("evidenceBasis") or "") != "DERIVED_POLICY"
    ]
    if not supplier_evidence:
        return False
    return any(
        locator_scope(str(item.get("sourceLocator") or ""))
        in physical.pages_or_slides
        or str(item.get("sourceLocator") or "").startswith("xlsx:sheet=")
        for item in supplier_evidence
    )


def candidate_core_complete(
    values: dict[str, Any],
    matched: PhysicalUnit | None,
) -> bool:
    del matched
    return bool(values.get("name") or values.get("productCode"))


def candidate_raw_rate(
    candidate: dict[str, Any], values: dict[str, Any]
) -> str | None:
    for evidence in candidate.get("evidence") or []:
        if str(evidence.get("fieldName") or "").lower() in {
            "rate", "rateamountminor", "base_price", "baseprice"
        } and evidence.get("rawValue") not in (None, ""):
            return str(evidence["rawValue"])
    extension = values.get("extension") or {}
    return string_value(extension.get("rawrate") or extension.get("rate_raw"))


def candidate_signature(
    values: dict[str, Any], source_locator: str, raw_rate: str | None
) -> str:
    deliverable = values.get("deliverable") or {}
    name = str(values.get("name") or "")
    code = normalize(str(values.get("productCode") or "")) or explicit_code(name)
    locator_key = "" if code else normalize(source_locator)
    material = "|".join((
        code,
        normalize(name),
        normalize(str(deliverable.get("placement") or "")),
        normalize(str(deliverable.get("programme") or "")),
        normalize(str(deliverable.get("daypart") or "")),
        money_fingerprint(raw_rate),
        locator_key,
    ))
    return hashlib.sha256(material.encode("utf-8")).hexdigest()


def text_supported(raw: str, normalized_document: str) -> bool:
    normalized = normalize(raw)
    if not normalized:
        return True
    if normalized in normalized_document:
        return True
    tokens = significant_tokens(normalized)
    if not tokens:
        return True
    document_tokens = set(normalized_document.split())
    return len(tokens.intersection(document_tokens)) / len(tokens) >= 0.8


def significant_tokens(value: str) -> set[str]:
    stop = {
        "the", "and", "for", "with", "from", "per", "rate", "card",
        "site", "media", "digital", "ooh", "radio", "tv", "placement",
    }
    return {
        token for token in normalize(value).split()
        if len(token) >= 3 and token not in stop
    }


def physical_unit_view(unit: PhysicalUnit) -> dict[str, Any]:
    return {
        "key": unit.key,
        "locator": unit.locator,
        "scope": unit.scope,
        "kind": unit.kind,
        "identity": unit.identity,
        "rawRate": unit.raw_rate,
        "evidence": list(unit.evidence[:8]),
    }


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )


def string_value(value: Any) -> str | None:
    return str(value) if value not in (None, "") else None


def integer_value(value: Any) -> int | None:
    try:
        return int(value) if value is not None else None
    except (TypeError, ValueError):
        return None
