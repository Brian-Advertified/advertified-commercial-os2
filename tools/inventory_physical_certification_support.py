"""Matching helpers for independent inventory physical certification."""

from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from functools import lru_cache
from typing import Any

from inventory_physical_facts import (
    MONEY_PATTERN,
    PhysicalAnchor,
    PhysicalSource,
    normalize_compact,
    normalize_money,
)


@dataclass(frozen=True)
class CandidateFact:
    candidate_id: str
    row_number: int
    status: str
    source_locator: str
    name: str | None
    product_code: str | None
    channel: str | None
    product_type: str | None
    geography: str | None
    raw_rate: str | None
    rate_amount_minor: int | None
    currency: str | None
    rate_type: str | None
    evidence_count: int
    evidence_fields: tuple[str, ...]
    blocking_issue_count: int
    source_hashes: tuple[str, ...]
    source_blob: str


def candidate_fact(item: dict[str, Any]) -> CandidateFact:
    values = (
        item.get("canonicalValues")
        or item.get("proposedValues")
        or item.get("values")
        or {}
    )
    evidence = item.get("evidence") or []
    validation = item.get("validation") or []
    raw_rate = next(
        (
            str(entry.get("rawValue"))
            for entry in evidence
            if entry.get("fieldName") == "rate" and entry.get("rawValue")
        ),
        None,
    )
    blob_parts = [json.dumps(values, sort_keys=True)]
    blob_parts.extend(
        str(entry.get("rawValue") or "") for entry in evidence
    )
    blob_parts.extend(
        str(entry.get("normalizedValue") or "") for entry in evidence
    )
    return CandidateFact(
        candidate_id=str(item.get("id") or ""),
        row_number=int(item.get("rowNumber") or 0),
        status=str(item.get("status") or ""),
        source_locator=str(item.get("sourceLocator") or ""),
        name=text(values.get("name")),
        product_code=text(values.get("productCode")),
        channel=text(values.get("channel")),
        product_type=text(values.get("productType")),
        geography=text(values.get("geography")),
        raw_rate=raw_rate,
        rate_amount_minor=as_int(values.get("rateAmountMinor")),
        currency=text(values.get("currency")),
        rate_type=text(values.get("rateType")),
        evidence_count=len(evidence),
        evidence_fields=tuple(sorted({
            str(entry.get("fieldName") or "")
            for entry in evidence
            if entry.get("fieldName")
        })),
        blocking_issue_count=sum(
            bool(entry.get("isBlocking")) for entry in validation
        ),
        source_hashes=tuple(
            sorted({
                str(entry.get("sourceHash"))
                for entry in evidence
                if entry.get("sourceHash")
                and entry.get("evidenceBasis") != "DERIVED_POLICY"
            })
        ),
        source_blob="\n".join(blob_parts),
    )


def match_inventory(
    physical: PhysicalSource,
    anchors: tuple[PhysicalAnchor, ...],
    candidates: tuple[CandidateFact, ...],
) -> tuple[
    tuple[tuple[PhysicalAnchor, CandidateFact], ...],
    tuple[PhysicalAnchor, ...],
    tuple[CandidateFact, ...],
]:
    _ = physical
    supported_candidates: set[int] = set()
    matches: list[tuple[PhysicalAnchor, CandidateFact]] = []
    unmatched: list[PhysicalAnchor] = []
    for anchor in anchors:
        ranked = sorted(
            (
                (candidate_match_score(anchor, candidate), index)
                for index, candidate in enumerate(candidates)
                if index not in supported_candidates
            ),
            reverse=True,
        )
        score, index = ranked[0] if ranked else (0, -1)
        if score < minimum_match_score(anchor):
            unmatched.append(anchor)
            continue
        candidate = candidates[index]
        matches.append((anchor, candidate))
        supported_candidates.add(index)
    unsupported = tuple(
        candidate
        for index, candidate in enumerate(candidates)
        if index not in supported_candidates
    )
    return tuple(matches), tuple(unmatched), unsupported


def candidate_match_score(
    anchor: PhysicalAnchor,
    candidate: CandidateFact,
) -> int:
    score = 0
    if anchor.anchor_type in {"WORKBOOK_ROW", "TABLE_ROW"}:
        anchor_row = locator_number(anchor.locator, "row")
        candidate_row = locator_number(candidate.source_locator, "row")
        if (
            anchor_row is not None
            and candidate_row is not None
            and anchor_row != candidate_row
        ):
            return 0
        if anchor_row is not None and anchor_row == candidate_row:
            score += 12
    blob = normalize_compact(candidate.source_blob)
    if anchor.product_code:
        code = normalize_compact(anchor.product_code)
        if code and code in blob:
            score += 10
        else:
            return 0
    identity = normalize_compact(anchor.identity or "")
    if identity and identity in blob:
        score += 6
    elif anchor.identity:
        tokens = identity_tokens(anchor.identity)
        candidate_tokens = identity_tokens(candidate.source_blob)
        overlap = len(tokens.intersection(candidate_tokens))
        if overlap >= max(1, min(3, len(tokens))):
            score += min(5, overlap)
    if anchor.raw_rate:
        rate = rate_token(anchor.raw_rate)
        if rate and rate in candidate_rate_tokens(candidate):
            score += 8
        else:
            return 0
    if anchor.ordinal and ordinal_in_locator(anchor.ordinal, candidate.source_locator):
        score += 2
    if candidate_has_identity(candidate):
        score += 1
    return score


def minimum_match_score(anchor: PhysicalAnchor) -> int:
    if anchor.product_code and anchor.raw_rate:
        return 19
    if anchor.product_code:
        return 11
    if anchor.raw_rate:
        return 9
    return 4


def candidate_has_identity(item: CandidateFact) -> bool:
    return bool(item.name or item.product_code)


def candidate_source_supported(
    physical: PhysicalSource,
    item: CandidateFact,
    anchors: tuple[PhysicalAnchor, ...],
) -> bool:
    if not candidate_has_identity(item) or item.evidence_count == 0:
        return False
    source_blob = normalize_compact(physical.searchable_text)
    supplier_code = (
        item.product_code
        if item.product_code and not item.product_code.startswith("ADV-")
        else None
    )
    if supplier_code:
        identity_supported = normalize_compact(supplier_code) in source_blob
    else:
        identity = identity_tokens(item.name or "")
        source_tokens = identity_tokens(physical.searchable_text)
        required = max(1, min(3, len(identity)))
        identity_supported = len(identity.intersection(source_tokens)) >= required
    if not identity_supported:
        return False
    if item.raw_rate:
        physical_rates = {
            rate_token(anchor.raw_rate)
            for anchor in anchors
            if anchor.raw_rate
        }
        if rate_token(item.raw_rate) not in physical_rates:
            return False
    return True


@lru_cache(maxsize=16_384)
def candidate_rate_tokens(item: CandidateFact) -> frozenset[str]:
    result = {rate_token(item.raw_rate or "")} if item.raw_rate else set()
    result.update(
        rate_token(match.group(0))
        for match in MONEY_PATTERN.finditer(item.source_blob)
    )
    return frozenset(value for value in result if value)


def duplicate_candidate_signatures(
    candidates: tuple[CandidateFact, ...],
) -> tuple[str, ...]:
    counts: dict[str, int] = {}
    for item in candidates:
        signature = "|".join((
            normalize_compact(item.product_code or item.name or ""),
            rate_token(item.raw_rate or ""),
            normalize_compact(item.channel or ""),
            normalize_compact(item.product_type or ""),
            normalize_compact(item.geography or ""),
            normalize_compact(item.source_locator),
        ))
        counts[signature] = counts.get(signature, 0) + 1
    return tuple(
        key for key, count in counts.items() if key.strip("|") and count > 1
    )


def locator_number(locator: str, field: str) -> int | None:
    match = re.search(rf"(?:^|;){re.escape(field)}=(\d+)(?:;|$)", locator)
    return int(match.group(1)) if match else None


def ordinal_in_locator(ordinal: int, locator: str) -> bool:
    return bool(re.search(rf"(?:page|slide)={ordinal}(?:;|$)", locator))


@lru_cache(maxsize=16_384)
def identity_tokens(value: str) -> frozenset[str]:
    return frozenset({
        token.lower()
        for token in re.findall(r"[A-Za-z0-9]+", value)
        if len(token) >= 3
        and token.lower() not in {
            "the", "and", "for", "with", "rate", "card", "media",
            "available", "advertising", "south", "africa",
        }
    })


def rate_token(value: str) -> str:
    normalized = normalize_money(value)
    if normalized.startswith("ZAR"):
        return normalized[3:]
    if normalized.startswith("R"):
        return normalized[1:]
    return normalized


def latest_attempt(import_view: dict[str, Any]) -> dict[str, Any] | None:
    attempts = import_view.get("extractionAttempts") or []
    return max(
        attempts,
        key=lambda value: int(value.get("attemptNumber") or 0),
        default=None,
    )


def anchor_view(item: PhysicalAnchor) -> dict[str, Any]:
    return asdict(item)


def candidate_view(item: CandidateFact) -> dict[str, Any]:
    value = asdict(item)
    value.pop("source_blob", None)
    return value


def require(value: bool, failures: list[str], code: str) -> None:
    if not value:
        failures.append(code)


def text(value: Any) -> str | None:
    return str(value).strip() if value is not None and str(value).strip() else None


def as_int(value: Any) -> int | None:
    try:
        return int(value) if value is not None else None
    except (TypeError, ValueError):
        return None


def normalize_text(value: str) -> str:
    return " ".join(value.replace("\u00a0", " ").split())
