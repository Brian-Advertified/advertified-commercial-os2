"""Pure production-release policy for extracted inventory files."""

from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import UTC, datetime
from typing import Any


CERTIFIED = "CERTIFIED_PHYSICAL_SOURCE_MATCH"
QUARANTINED = "UNCERTIFIED_QUARANTINED"
HUMAN_REVIEW_REQUIRED = "HUMAN_REVIEW_REQUIRED"
PUBLICATION_PROHIBITED = "PUBLICATION_PROHIBITED"


@dataclass(frozen=True)
class FileReleaseRecord:
    source_hash: str
    file_name: str
    document_class: str
    import_id: str
    import_status: str
    import_failure_code: str | None
    latest_attempt_number: int | None
    latest_attempt_status: str | None
    candidate_count: int
    candidates_with_no_core_fields: int
    candidates_meeting_minimum: int
    blocking_candidate_count: int
    approved_candidate_count: int
    published_candidate_count: int
    file_gold_present: bool
    file_gold_passed: bool
    extraction_certification: str
    publication_disposition: str
    reasons: tuple[str, ...]


def build_record(
    source: dict[str, Any],
    import_view: dict[str, Any],
    manifest_document: dict[str, Any] | None,
    *,
    gold_present: bool,
    gold_passed: bool,
) -> FileReleaseRecord:
    candidates = import_view.get("candidates") or []
    no_core = 0
    minimum = 0
    blocking = 0
    approved = 0
    published = 0
    for candidate in candidates:
        values = candidate_values(candidate)
        if not any(has_value(item) for item in core_values(values).values()):
            no_core += 1
        if meets_minimum(values):
            minimum += 1
        if any(
            bool(issue.get("isBlocking"))
            for issue in candidate.get("validation") or []
        ):
            blocking += 1
        status = str(candidate.get("status") or "")
        if status == "APPROVED":
            approved += 1
        if status == "PUBLISHED":
            published += 1

    attempts = import_view.get("extractionAttempts") or []
    latest = attempts[0] if attempts else {}
    reasons = reasons_for(
        import_view,
        latest,
        manifest_document,
        gold_present=gold_present,
        gold_passed=gold_passed,
        no_core=no_core,
        blocking=blocking,
        published=published,
    )
    certified = (
        gold_passed
        and str(import_view.get("status")) == "REVIEW_REQUIRED"
        and str(latest.get("status")) == "COMPLETED"
        and not import_view.get("failureCode")
        and no_core == 0
    )
    return FileReleaseRecord(
        source_hash=str(source["sourceHash"]),
        file_name=str(source.get("fileName") or ""),
        document_class=str(source.get("documentClass") or ""),
        import_id=str(source.get("importId") or ""),
        import_status=str(import_view.get("status") or ""),
        import_failure_code=import_view.get("failureCode"),
        latest_attempt_number=as_int(latest.get("attemptNumber")),
        latest_attempt_status=(
            str(latest.get("status")) if latest.get("status") else None
        ),
        candidate_count=len(candidates),
        candidates_with_no_core_fields=no_core,
        candidates_meeting_minimum=minimum,
        blocking_candidate_count=blocking,
        approved_candidate_count=approved,
        published_candidate_count=published,
        file_gold_present=gold_present,
        file_gold_passed=gold_passed,
        extraction_certification=CERTIFIED if certified else QUARANTINED,
        publication_disposition=(
            HUMAN_REVIEW_REQUIRED if certified else PUBLICATION_PROHIBITED
        ),
        reasons=tuple(reasons),
    )


def reasons_for(
    import_view: dict[str, Any],
    latest: dict[str, Any],
    manifest_document: dict[str, Any] | None,
    *,
    gold_present: bool,
    gold_passed: bool,
    no_core: int,
    blocking: int,
    published: int,
) -> list[str]:
    reasons: list[str] = []
    if not gold_present:
        reasons.append("FILE_LEVEL_GOLD_MISSING")
    elif not gold_passed:
        reasons.append("FILE_LEVEL_GOLD_FAILED")
    if str(import_view.get("status")) != "REVIEW_REQUIRED":
        reasons.append("IMPORT_NOT_FENCED_FOR_REVIEW")
    if str(latest.get("status")) != "COMPLETED":
        reasons.append("LATEST_EXTRACTION_ATTEMPT_NOT_COMPLETED")
    if import_view.get("failureCode"):
        reasons.append("IMPORT_FAILURE_PRESENT")
    if no_core:
        reasons.append("CORE_EMPTY_CANDIDATES_PRESENT")
    if blocking:
        reasons.append("BLOCKING_REVIEW_ISSUES_PRESENT")
    if published:
        reasons.append("CANDIDATES_ALREADY_PUBLISHED")
    if manifest_document is None:
        reasons.append("SOURCE_MANIFEST_ENTRY_MISSING")
    return reasons


def build_register(
    runtime: dict[str, Any],
    preflight: dict[str, Any],
    manifest: dict[str, Any],
    records: list[FileReleaseRecord],
) -> dict[str, Any]:
    certified = [
        item for item in records
        if item.extraction_certification == CERTIFIED
    ]
    published = sum(item.published_candidate_count for item in records)
    source_count_matches = (
        len(records) == int(manifest.get("documentCount") or 0) == 43
    )
    launch_blockers = software_launch_blockers(
        runtime,
        preflight,
        records,
        source_count_matches=source_count_matches,
    )
    corpus_blockers = corpus_publication_blockers(
        records,
        certified_count=len(certified),
        published_count=published,
    )
    return {
        "schemaVersion": "advertified.inventory-production-release.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "softwareLaunchGate": "GO" if not launch_blockers else "NO_GO",
        "softwareLaunchBlockers": launch_blockers,
        "corpusPublicationGate": (
            "GO" if not corpus_blockers else "NO_GO"
        ),
        "corpusPublicationBlockers": corpus_blockers,
        "launchBoundary": {
            "softwareMayLaunch": not launch_blockers,
            "uncertifiedCorpusMustRemainQuarantined": True,
            "automaticCorpusPublicationAllowed": False,
            "humanReviewStillRequiredForCertifiedFiles": True,
        },
        "bedrock": {
            "liveExecutionEnabled": bool(
                preflight.get("liveExecutionEnabled")
            ),
            "committedCostUsdMicros": int(
                preflight.get("existingCommittedCostUsdMicros") or 0
            ),
            "runtimeDeterministicZeroCost": bool(
                runtime.get("deterministicZeroCost")
            ),
        },
        "summary": summary(records, len(certified), published),
        "files": [asdict(item) for item in records],
    }


def software_launch_blockers(
    runtime: dict[str, Any],
    preflight: dict[str, Any],
    records: list[FileReleaseRecord],
    *,
    source_count_matches: bool,
) -> list[str]:
    result: list[str] = []
    if not runtime.get("deterministicZeroCost"):
        result.append("RUNTIME_NOT_DETERMINISTIC_ZERO_COST")
    if bool(preflight.get("liveExecutionEnabled")):
        result.append("LIVE_BEDROCK_ENABLED")
    if int(preflight.get("existingCommittedCostUsdMicros") or 0) != 0:
        result.append("ACTIVE_SCOPE_HAS_COMMITTED_BEDROCK_COST")
    if not source_count_matches:
        result.append("SOURCE_MANIFEST_COUNT_MISMATCH")
    if not all(item.import_status == "REVIEW_REQUIRED" for item in records):
        result.append("CORPUS_IMPORT_NOT_REVIEW_FENCED")
    if any(
        item.published_candidate_count > 0
        and item.extraction_certification != CERTIFIED
        for item in records
    ):
        result.append("UNCERTIFIED_INVENTORY_WAS_PUBLISHED")
    return result


def corpus_publication_blockers(
    records: list[FileReleaseRecord],
    *,
    certified_count: int,
    published_count: int,
) -> list[str]:
    result: list[str] = []
    if certified_count != len(records):
        result.append("NOT_ALL_SOURCE_FILES_CERTIFIED")
    if any(item.blocking_candidate_count for item in records):
        result.append("BLOCKING_CANDIDATE_REVIEWS_REMAIN")
    if published_count == 0:
        result.append("NO_CERTIFIED_INVENTORY_PUBLISHED")
    return result


def summary(
    records: list[FileReleaseRecord],
    certified_count: int,
    published_count: int,
) -> dict[str, int]:
    return {
        "sourceCount": len(records),
        "certifiedSourceCount": certified_count,
        "quarantinedSourceCount": len(records) - certified_count,
        "activeCandidateCount": sum(item.candidate_count for item in records),
        "coreEmptyCandidateCount": sum(
            item.candidates_with_no_core_fields for item in records
        ),
        "minimumCompleteCandidateCount": sum(
            item.candidates_meeting_minimum for item in records
        ),
        "blockingCandidateCount": sum(
            item.blocking_candidate_count for item in records
        ),
        "approvedCandidateCount": sum(
            item.approved_candidate_count for item in records
        ),
        "publishedCandidateCount": published_count,
    }


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )


def core_values(values: dict[str, Any]) -> dict[str, Any]:
    return {
        "name": values.get("name"),
        "productCode": values.get("productCode"),
        "channel": values.get("channel"),
        "productType": values.get("productType"),
        "currency": values.get("currency"),
        "rateAmountMinor": values.get("rateAmountMinor"),
        "rateType": values.get("rateType"),
        "geography": values.get("geography"),
    }


def meets_minimum(values: dict[str, Any]) -> bool:
    extension = values.get("extension") or {}
    rate_known_or_quarantined = (
        values.get("rateAmountMinor") is not None
        or extension.get("rateambiguity") == "AMBIGUOUS_TRUNCATED_RATE"
    )
    return (
        bool(values.get("name") or values.get("productCode"))
        and bool(values.get("channel"))
        and bool(values.get("productType"))
        and bool(values.get("currency"))
        and rate_known_or_quarantined
    )


def has_value(value: Any) -> bool:
    return value is not None and value != ""


def as_int(value: Any) -> int | None:
    try:
        return int(value) if value is not None else None
    except (TypeError, ValueError):
        return None
