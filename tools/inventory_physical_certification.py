"""Compare projected inventory with independently discovered physical facts."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

from inventory_physical_anchor_discovery import discover_anchors
from inventory_physical_certification_support import (
    anchor_view,
    candidate_fact,
    candidate_has_identity,
    candidate_rate_tokens,
    candidate_source_supported,
    candidate_view,
    duplicate_candidate_signatures,
    latest_attempt,
    match_inventory,
    rate_token,
    require,
)
from inventory_physical_facts import load_source, normalize_compact


@dataclass(frozen=True)
class FileCertification:
    source_hash: str
    file_name: str
    document_format: str
    import_id: str
    import_status: str
    latest_attempt_status: str | None
    latest_attempt_provider_version: str | None
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


def supplier_identity_required(physical: Any) -> bool:
    """Return true only when the physical source or filename names an owner."""
    haystack = (physical.relative_path + "\n" + physical.searchable_text).lower()
    strong_identities = (
        "algoa fm", "arena holdings", "blackspace", "business day tv",
        "dstv media sales", "eleven8", "emedia", "ignition tv",
        "insight outdoor", "jacaranda fm", "jcdecaux", "jit tv",
        "jozi fm", "kena outdoor", "mamg", "volt.africa", "volt africa",
        "primedia", "relativ media", "reveel", "rsd rate cards", "sabc",
        "sb outdoor", "smile 90.4", "summit ooh", "the home channel",
        "virgin active", "yfm", "kaya fm",
    )
    return any(identity in haystack for identity in strong_identities)


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
    # A sellable candidate must map to one distinct physical inventory unit.
    # Source-supported duplicates are still duplicates and therefore fail.
    unsupported = unmatched_candidates
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
        any(
            field.lower().replace("_", "") in {"supplier", "suppliername"}
            for field in item.evidence_fields
        )
        for item in candidates
    )
    if supplier_identity_required(physical):
        require(
            supplier_reconstructed,
            failures,
            "SUPPLIER_NOT_RECONSTRUCTED",
        )
    elif not supplier_reconstructed:
        warnings.append("SUPPLIER_NOT_EXPLICIT_IN_PHYSICAL_SOURCE")
    wrong_hash = [
        item
        for item in candidates
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
        rate_token(item.raw_rate or "")
        for item in anchors
        if item.raw_rate
    }
    candidate_rates = {
        rate
        for item in candidates
        for rate in candidate_rate_tokens(item)
        if rate
    }
    covered_rates = physical_rates.intersection(candidate_rates)
    require(
        covered_rates == physical_rates,
        failures,
        "PHYSICAL_RATES_UNCOVERED",
    )

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

    for item in candidates:
        if item.raw_rate and rate_token(item.raw_rate) not in physical_rates:
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
        latest_attempt_provider_version=(
            str(latest.get("providerVersion")) if latest else None
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
