"""Compare projected inventory with independently discovered physical facts."""

from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from inventory_physical_anchor_discovery import discover_anchors
from inventory_physical_facts import (
    MONEY_PATTERN,
    PhysicalAnchor,
    PhysicalSource,
    all_source_values,
    load_source,
    normalize_compact,
    normalize_money,
)

ALLOWED_DERIVED_FIELDS = {
    "availability",
    "channel",
    "currency",
    "productType",
    "rateType",
    "description",
}


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


@dataclass(frozen=True)
class FileCertification:
    source_hash: str
    file_name: str
    document_format: str
    import_id: str
    import_status: str
    latest_attempt_status: str | None
    candidate_count: int
    expected_anchor_count: int
    matched_anchor_count: int
    unmatched_anchor_count: int
    unsupported_candidate_count: int
    blocking_candidate_count: int
    physical_rate_count: int
    covered_physical_rate_count: int
    physical_code_count: int
    covered_physical_code_count: int
    passed: bool
    failures: tuple[str, ...]
    warnings: tuple[str, ...]
    unmatched_anchors: tuple[dict[str, Any], ...]
    unsupported_candidates: tuple[dict[str, Any], ...]


def certify_file(
    source_map_path: Path,
    import_view: dict[str, Any],
    source: dict[str, Any],
) -> FileCertification:
    physical = load_source(source_map_path)
    anchors = discover_anchors(physical)
    candidates = tuple(
        candidate_fact(item)
        for item in import_view.get("candidates") or []
    )
    matches, unmatched, unmatched_candidates = match_inventory(
        physical, anchors, candidates
    )
    unsupported = tuple(
        item
        for item in unmatched_candidates
        if not candidate_source_supported(physical, item)
    )
    failures: list[str] = []
    warnings: list[str] = []
    latest = latest_attempt(import_view)

    require(
        physical.source_hash == str(source.get("sourceHash")),
        failures,
        "SOURCE_MAP_HASH_MISMATCH",
    )
    require(
        str(import_view.get("sourceHash")) == physical.source_hash,
        failures,
        "IMPORT_SOURCE_HASH_MISMATCH",
    )
    require(
        str(import_view.get("status")) == "REVIEW_REQUIRED",
        failures,
        "IMPORT_NOT_REVIEW_FENCED",
    )
    require(
        latest is not None and latest.get("status") == "COMPLETED",
        failures,
        "LATEST_ATTEMPT_NOT_COMPLETED",
    )
    require(
        not import_view.get("failureCode"),
        failures,
        "IMPORT_FAILURE_PRESENT",
    )
    require(bool(anchors), failures, "NO_PHYSICAL_INVENTORY_ANCHORS")
    require(bool(candidates), failures, "NO_PROJECTED_CANDIDATES")
    require(not unmatched, failures, "PHYSICAL_ANCHORS_UNCOVERED")
    require(not unsupported, failures, "PROJECTED_CANDIDATES_UNSUPPORTED")

    core_empty = [item for item in candidates if not candidate_has_identity(item)]
    require(not core_empty, failures, "CORE_EMPTY_CANDIDATES_PRESENT")
    no_evidence = [item for item in candidates if item.evidence_count == 0]
    require(not no_evidence, failures, "CANDIDATES_WITHOUT_EVIDENCE")
    supplier_reconstructed = any(
        any(field.lower() in {"supplier", "suppliername"}
            for field in item.evidence_fields)
        for item in candidates
    )
    require(
        supplier_reconstructed,
        failures,
        "SUPPLIER_NOT_RECONSTRUCTED",
    )
    wrong_hash = [
        item for item in candidates
        if any(value != physical.source_hash for value in item.source_hashes)
    ]
    require(not wrong_hash, failures, "CANDIDATE_EVIDENCE_HASH_MISMATCH")

    duplicate_signatures = duplicate_candidate_signatures(candidates)
    require(
        not duplicate_signatures,
        failures,
        "DUPLICATE_PROJECTED_CANDIDATES",
    )
    if any(item.blocking_issue_count for item in candidates):
        warnings.append("HUMAN_REVIEW_BLOCKERS_REMAIN")

    physical_rates = {
        normalize_money(item.raw_rate or "")
        for item in anchors
        if item.raw_rate
    }
    candidate_rates = {
        rate
        for item in candidates
        for rate in candidate_rate_tokens(item)
        if rate
    }
    candidate_primary_rates = {
        normalize_money(item.raw_rate or "")
        for item in candidates
        if item.raw_rate
    }
    covered_rates = physical_rates.intersection(candidate_rates)
    require(
        covered_rates == physical_rates,
        failures,
        "PHYSICAL_RATES_UNCOVERED",
    )
    _ = candidate_primary_rates

    physical_codes = {
        normalize_compact(item.product_code or "")
        for item in anchors
        if item.product_code
    }
    candidate_blob = normalize_compact(
        "\n".join(item.source_blob for item in candidates)
    )
    covered_codes = {
        value for value in physical_codes if value and value in candidate_blob
    }
    require(
        covered_codes == physical_codes,
        failures,
        "PHYSICAL_PRODUCT_CODES_UNCOVERED",
    )

    source_values = tuple(normalize_text(value) for value in all_source_values(physical))
    for item in candidates:
        if item.raw_rate and not any(
            normalize_money(item.raw_rate) in normalize_money(value)
            for value in source_values
        ):
            failures.append(
                f"RATE_NOT_SOURCE_SUPPORTED:{item.row_number}:{item.raw_rate}"
            )

    return FileCertification(
        source_hash=physical.source_hash,
        file_name=physical.relative_path,
        document_format=physical.document_format,
        import_id=str(source.get("importId") or ""),
        import_status=str(import_view.get("status") or ""),
        latest_attempt_status=(
            str(latest.get("status")) if latest else None
        ),
        candidate_count=len(candidates),
        expected_anchor_count=len(anchors),
        matched_anchor_count=len(matches),
        unmatched_anchor_count=len(unmatched),
        unsupported_candidate_count=len(unsupported),
        blocking_candidate_count=sum(
            item.blocking_issue_count for item in candidates
        ),
        physical_rate_count=len(physical_rates),
        covered_physical_rate_count=len(covered_rates),
        physical_code_count=len(physical_codes),
        covered_physical_code_count=len(covered_codes),
        passed=not failures,
        failures=tuple(dict.fromkeys(failures)),
        warnings=tuple(dict.fromkeys(warnings)),
        unmatched_anchors=tuple(anchor_view(item) for item in unmatched[:100]),
        unsupported_candidates=tuple(
            candidate_view(item) for item in unsupported[:100]
        ),
    )


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
    supported_candidates: set[int] = set()
    matches: list[tuple[PhysicalAnchor, CandidateFact]] = []
    unmatched: list[PhysicalAnchor] = []
    for anchor in anchors:
        ranked = sorted(
            (
                (candidate_match_score(anchor, candidate), index)
                for index, candidate in enumerate(candidates)
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
        rate = normalize_money(anchor.raw_rate)
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
) -> bool:
    if not candidate_has_identity(item) or item.evidence_count == 0:
        return False
    source_blob = normalize_compact(physical.searchable_text)
    if item.product_code:
        identity_supported = (
            normalize_compact(item.product_code) in source_blob
        )
    else:
        identity = identity_tokens(item.name or "")
        source_tokens = identity_tokens(physical.searchable_text)
        required = max(1, min(3, len(identity)))
        identity_supported = len(identity.intersection(source_tokens)) >= required
    if not identity_supported:
        return False
    if item.raw_rate:
        raw = normalize_money(item.raw_rate)
        if raw and raw not in normalize_money(physical.searchable_text):
            return False
    return True


def candidate_rate_tokens(item: CandidateFact) -> set[str]:
    result = {
        normalize_money(item.raw_rate or "")
    } if item.raw_rate else set()
    result.update(
        normalize_money(match.group(0))
        for match in MONEY_PATTERN.finditer(item.source_blob)
    )
    return {value for value in result if value}


def duplicate_candidate_signatures(
    candidates: tuple[CandidateFact, ...],
) -> tuple[str, ...]:
    counts: dict[str, int] = {}
    for item in candidates:
        signature = "|".join((
            normalize_compact(item.product_code or item.name or ""),
            normalize_compact(item.raw_rate or ""),
            normalize_compact(item.channel or ""),
            normalize_compact(item.product_type or ""),
            normalize_compact(item.geography or ""),
            normalize_compact(item.source_locator),
        ))
        counts[signature] = counts.get(signature, 0) + 1
    return tuple(
        key for key, count in counts.items() if key.strip("|") and count > 1
    )


def ordinal_in_locator(ordinal: int, locator: str) -> bool:
    return bool(re.search(rf"(?:page|slide)={ordinal}(?:;|$)", locator))


def identity_tokens(value: str) -> set[str]:
    return {
        token.lower()
        for token in re.findall(r"[A-Za-z0-9]+", value)
        if len(token) >= 3
        and token.lower() not in {
            "the", "and", "for", "with", "rate", "card", "media",
            "available", "advertising", "south", "africa",
        }
    }


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
