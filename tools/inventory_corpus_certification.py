"""File-level comparison of physical inventory evidence and API candidates."""

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


@dataclass(frozen=True)
class CandidateAudit:
    candidate_id: str
    row_number: int
    source_locator: str
    scope: str
    name: str | None
    raw_rate: str | None
    rate_amount_minor: int | None
    blocking_issues: tuple[str, ...]
    unsupported_evidence: tuple[str, ...]
    matched_unit_key: str | None
    duplicate_of: str | None
    verdict: str


@dataclass(frozen=True)
class FileCertification:
    source_hash: str
    file_name: str
    document_format: str
    import_id: str
    import_status: str
    latest_attempt_status: str | None
    latest_provider_version: str | None
    source_hash_verified: bool
    physical_scope_count: int
    physical_unit_count: int
    candidate_count: int
    matched_physical_unit_count: int
    unmatched_physical_units: tuple[dict[str, Any], ...]
    unsupported_candidate_count: int
    duplicate_candidate_count: int
    blocking_candidate_count: int
    approved_candidate_count: int
    published_candidate_count: int
    candidate_audits: tuple[CandidateAudit, ...]
    blockers: tuple[str, ...]
    warnings: tuple[str, ...]
    verdict: str


def certify_file(
    source_map_path: Path,
    manifest_document: dict[str, Any],
    import_view: dict[str, Any],
    current_provider_version: str,
    human_gold: dict[str, Any] | None = None,
) -> FileCertification:
    physical = read_physical_document(source_map_path)
    expected_hash = str(manifest_document["sha256"])
    source_hash_verified = (
        physical.source_hash == expected_hash
        and str(import_view.get("sourceHash") or expected_hash) == expected_hash
    )
    candidates = import_view.get("candidates") or []
    candidate_audits, matched_keys = audit_candidates(
        physical,
        candidates,
        human_gold,
    )
    unmatched = tuple(
        physical_unit_view(unit)
        for unit in physical.units
        if unit.key not in matched_keys
    )
    attempts = import_view.get("extractionAttempts") or []
    latest = max(
        attempts,
        key=lambda item: int(item.get("attemptNumber") or 0),
        default={},
    )
    blockers: list[str] = []
    warnings: list[str] = []
    if not source_hash_verified:
        blockers.append("SOURCE_HASH_MISMATCH")
    if str(import_view.get("status")) != "REVIEW_REQUIRED":
        blockers.append("IMPORT_NOT_AT_HUMAN_REVIEW")
    if import_view.get("failureCode"):
        blockers.append("IMPORT_FAILURE_PRESENT")
    if str(latest.get("status")) != "COMPLETED":
        blockers.append("LATEST_EXTRACTION_ATTEMPT_NOT_COMPLETED")
    if str(latest.get("providerVersion") or "") != current_provider_version:
        blockers.append("STALE_PROJECTION_VERSION")
    if not candidates:
        blockers.append("NO_CANDIDATES")
    if unmatched:
        blockers.append("PHYSICAL_SELLABLE_UNITS_MISSING")
    unsupported = sum(audit.verdict == "FAIL" for audit in candidate_audits)
    duplicates = sum(audit.duplicate_of is not None for audit in candidate_audits)
    blocking = sum(bool(audit.blocking_issues) for audit in candidate_audits)
    if unsupported:
        blockers.append("UNSUPPORTED_OR_INCOMPLETE_CANDIDATES")
    if duplicates:
        blockers.append("DUPLICATE_CANDIDATES")
    if blocking:
        blockers.append("BLOCKING_VALIDATION_ISSUES")
    if len(physical.units) == 0:
        warnings.append("NO_STRONG_PHYSICAL_SELLABLE_UNIT_WAS_DERIVED")
    approved = sum(
        str(candidate.get("status") or "") == "APPROVED"
        for candidate in candidates
    )
    published = sum(
        str(candidate.get("status") or "") == "PUBLISHED"
        for candidate in candidates
    )
    if approved or published:
        blockers.append("PRE_CERTIFICATION_APPROVAL_OR_PUBLICATION_PRESENT")
    return FileCertification(
        source_hash=expected_hash,
        file_name=str(manifest_document["relativePath"]),
        document_format=str(physical.document_format),
        import_id=str(import_view.get("id") or manifest_document.get("processing", {}).get("importId") or ""),
        import_status=str(import_view.get("status") or ""),
        latest_attempt_status=str(latest.get("status")) if latest else None,
        latest_provider_version=str(latest.get("providerVersion")) if latest else None,
        source_hash_verified=source_hash_verified,
        physical_scope_count=len(physical.pages_or_slides),
        physical_unit_count=len(physical.units),
        candidate_count=len(candidates),
        matched_physical_unit_count=len(matched_keys),
        unmatched_physical_units=unmatched,
        unsupported_candidate_count=unsupported,
        duplicate_candidate_count=duplicates,
        blocking_candidate_count=blocking,
        approved_candidate_count=approved,
        published_candidate_count=published,
        candidate_audits=tuple(candidate_audits),
        blockers=tuple(dict.fromkeys(blockers)),
        warnings=tuple(warnings),
        verdict="PASS" if not blockers else "FAIL",
    )


def audit_candidates(
    physical: PhysicalDocument,
    candidates: list[dict[str, Any]],
    human_gold: dict[str, Any] | None = None,
) -> tuple[list[CandidateAudit], set[str]]:
    result: list[CandidateAudit] = []
    matched_keys: set[str] = set()
    signatures: dict[str, str] = {}
    for candidate in candidates:
        values = candidate_values(candidate)
        candidate_id = str(candidate.get("id") or "")
        source_locator = str(candidate.get("sourceLocator") or "")
        scope = locator_scope(source_locator)
        name = string_value(values.get("name"))
        raw_rate = candidate_raw_rate(candidate, values)
        blocking = tuple(
            issue_key(issue)
            for issue in candidate.get("validation") or []
            if bool(issue.get("isBlocking"))
            and is_physical_blocker(issue)
        )
        unsupported = unsupported_evidence(
            physical,
            candidate,
            human_gold,
        )
        matched = best_unit_match(
            physical.units,
            scope=scope,
            name=name,
            raw_rate=raw_rate,
            already_matched=matched_keys,
        )
        if matched:
            matched_keys.update(equivalent_unit_keys(physical.units, matched))
        signature = candidate_signature(values, source_locator, raw_rate)
        duplicate_of = signatures.get(signature)
        signatures.setdefault(signature, candidate_id)
        core_ok = candidate_core_complete(values, matched)
        grounded = candidate_is_physically_grounded(
            candidate,
            physical,
            unsupported,
        )
        verdict = "PASS"
        if blocking or unsupported or duplicate_of or not core_ok or not grounded:
            verdict = "FAIL"
        result.append(CandidateAudit(
            candidate_id=candidate_id,
            row_number=int(candidate.get("rowNumber") or 0),
            source_locator=source_locator,
            scope=scope,
            name=name,
            raw_rate=raw_rate,
            rate_amount_minor=integer_value(values.get("rateAmountMinor")),
            blocking_issues=blocking,
            unsupported_evidence=tuple(unsupported),
            matched_unit_key=matched.key if matched else None,
            duplicate_of=duplicate_of,
            verdict=verdict,
        ))
    return result, matched_keys


def best_unit_match(
    units: tuple[PhysicalUnit, ...],
    *,
    scope: str,
    name: str | None,
    raw_rate: str | None,
    already_matched: set[str],
) -> PhysicalUnit | None:
    ranked: list[tuple[int, PhysicalUnit]] = []
    name_tokens = significant_tokens(name or "")
    rate_key = money_fingerprint(raw_rate)
    for unit in units:
        if unit.key in already_matched:
            continue
        score = 0
        if scope and unit.scope == scope:
            score += 6
        if rate_key and money_fingerprint(unit.raw_rate) == rate_key:
            score += 8
        unit_tokens = significant_tokens(unit.identity)
        overlap = len(name_tokens.intersection(unit_tokens))
        if overlap:
            score += min(8, overlap * 2)
        if name_tokens and unit_tokens and overlap / min(len(name_tokens), len(unit_tokens)) >= 0.5:
            score += 4
        if score >= 8:
            ranked.append((score, unit))
    return max(ranked, key=lambda item: item[0])[1] if ranked else None


def unsupported_evidence(
    physical: PhysicalDocument,
    candidate: dict[str, Any],
    human_gold: dict[str, Any] | None = None,
) -> list[str]:
    result: list[str] = []
    for evidence in candidate.get("evidence") or []:
        field = str(evidence.get("fieldName") or "")
        normalized_field = field.replace("_", "").casefold()
        basis = str(evidence.get("evidenceBasis") or "")
        locator = str(evidence.get("sourceLocator") or "")
        transformation = str(evidence.get("transformation") or "")
        raw = evidence.get("rawValue")
        if basis == "DERIVED_POLICY":
            if normalized_field not in ALLOWED_DERIVED_FIELDS:
                result.append(f"UNAPPROVED_DERIVED_FIELD:{field}")
            continue
        if locator.startswith(("policy:", "source:file-name")):
            result.append(f"SUPPLIER_EVIDENCE_HAS_NON_PHYSICAL_LOCATOR:{field}")
            continue
        scope = locator_scope(locator)
        if scope and scope not in physical.pages_or_slides:
            result.append(f"SOURCE_SCOPE_NOT_FOUND:{field}:{locator}")
        if (
            raw not in (None, "")
            and not evidence_value_supported(
                physical,
                normalized_field,
                str(raw),
                transformation,
            )
            and not human_gold_supports_evidence(
                human_gold,
                candidate,
                evidence,
            )
        ):
            result.append(f"RAW_VALUE_NOT_FOUND:{field}:{str(raw)[:120]}")
    return result


def human_gold_supports_evidence(
    human_gold: dict[str, Any] | None,
    candidate: dict[str, Any],
    evidence: dict[str, Any],
) -> bool:
    if not human_gold:
        return False
    field = str(evidence.get("fieldName") or "").replace("_", "").casefold()
    if field != "description":
        return False
    row_number = int(candidate.get("rowNumber") or 0)
    record_key = f"row-{row_number}"
    semantic = human_gold.get("semanticExpectations") or {}
    required = set(semantic.get("descriptionRequiredForRecordKeys") or [])
    return (
        record_key in required
        and str(evidence.get("sourceLocator") or "")
        == str(semantic.get("descriptionEvidence") or "")
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


def write_certification(path: Path, certification: FileCertification) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(asdict(certification), indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
