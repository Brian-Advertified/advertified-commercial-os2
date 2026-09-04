"""Pure validation policy for Bedrock-enriched inventory candidates."""

from __future__ import annotations

import json
import re
from typing import Any, Iterable

from inventory_physical_facts import normalize_compact

ALLOWED_ROOT_CHANGES = {"channel", "productType", "description"}
ALLOWED_EXTENSION_PREFIXES = (
    "semantic",
    "search",
    "bedrock",
    "classification",
)


def certify_document(
    source: dict[str, Any],
    baseline: dict[str, Any],
    current: dict[str, Any],
    physical_text: str,
) -> dict[str, Any]:
    failures: list[str] = []
    baseline_candidates = baseline.get("candidates") or []
    current_candidates = current.get("candidates") or []
    if not baseline.get("physicalCertificationPassed"):
        failures.append("PHYSICAL_BASELINE_NOT_CERTIFIED")
    if current.get("status") != "REVIEW_REQUIRED":
        failures.append("IMPORT_NOT_REVIEW_FENCED")
    if current.get("failureCode"):
        failures.append("IMPORT_FAILURE_PRESENT")
    if len(baseline_candidates) != len(current_candidates):
        failures.append("CANDIDATE_COUNT_CHANGED")

    baseline_by_key = candidate_index(baseline_candidates)
    current_by_key = candidate_index(current_candidates)
    if set(baseline_by_key) != set(current_by_key):
        failures.append("PHYSICAL_CANDIDATE_IDENTITY_SET_CHANGED")

    changed_physical = 0
    missing_classification = 0
    missing_description = 0
    for key in sorted(set(baseline_by_key).intersection(current_by_key)):
        before = baseline_by_key[key]
        after = current_by_key[key]
        changes = forbidden_changes(
            before.get("values") or {},
            values(after),
        )
        if changes:
            changed_physical += len(changes)
            failures.extend(
                f"PHYSICAL_FIELD_CHANGED:{key}:{field}"
                for field in changes
            )
        current_values = values(after)
        if (
            not current_values.get("channel")
            or not current_values.get("productType")
        ):
            missing_classification += 1
            failures.append(f"CLASSIFICATION_MISSING:{key}")
        description = str(
            current_values.get("description") or ""
        ).strip()
        if not description:
            missing_description += 1
            failures.append(f"DESCRIPTION_MISSING:{key}")
        elif not description_grounded(
            description,
            physical_text,
            str(current_values.get("name") or ""),
        ):
            failures.append(f"DESCRIPTION_NOT_GROUNDED:{key}")
        if str(after.get("status") or "") != "REVIEW_REQUIRED":
            failures.append(f"CANDIDATE_NOT_REVIEW_FENCED:{key}")
        if not source_evidence_preserved(
            before.get("evidence") or [],
            after.get("evidence") or [],
        ):
            failures.append(f"SOURCE_EVIDENCE_CHANGED:{key}")

    return {
        "sourceHash": source["sourceHash"],
        "fileName": source["fileName"],
        "importId": source["importId"],
        "candidateCount": len(current_candidates),
        "changedPhysicalFieldCount": changed_physical,
        "missingClassificationCount": missing_classification,
        "missingDescriptionCount": missing_description,
        "passed": not failures,
        "failures": list(dict.fromkeys(failures)),
    }


def candidate_index(
    candidates: list[dict[str, Any]],
) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for item in candidates:
        item_values = values(item)
        evidence = item.get("evidence") or []
        raw_rate = next((
            str(entry.get("rawValue"))
            for entry in evidence
            if entry.get("fieldName") == "rate"
            and entry.get("rawValue")
        ), "")
        key = "|".join((
            str(item.get("rowNumber") or ""),
            normalize_compact(
                str(item.get("sourceLocator") or "")
            ),
            normalize_compact(str(
                item_values.get("productCode")
                or item_values.get("name")
                or ""
            )),
            normalize_compact(raw_rate),
        ))
        if key in result:
            raise RuntimeError(f"Duplicate physical candidate key: {key}")
        result[key] = item
    return result


def forbidden_changes(
    before: dict[str, Any],
    after: dict[str, Any],
) -> list[str]:
    before_clean = physical_values(before)
    after_clean = physical_values(after)
    keys = set(before_clean).union(after_clean)
    return [
        key
        for key in sorted(keys)
        if before_clean.get(key) != after_clean.get(key)
    ]


def physical_values(value: dict[str, Any]) -> dict[str, Any]:
    result = {
        key: item
        for key, item in value.items()
        if key not in ALLOWED_ROOT_CHANGES
    }
    extension = result.get("extension")
    if isinstance(extension, dict):
        result["extension"] = {
            key: item
            for key, item in extension.items()
            if not key.lower().startswith(ALLOWED_EXTENSION_PREFIXES)
        }
    return result


def description_grounded(
    description: str,
    physical_text: str,
    name: str,
) -> bool:
    source_tokens = tokens(physical_text + " " + name)
    descriptive = {
        value
        for value in tokens(description)
        if value not in {
            "advertising",
            "placement",
            "inventory",
            "media",
            "available",
            "campaign",
            "audience",
            "product",
            "format",
            "platform",
        }
    }
    if (
        descriptive
        and len(descriptive.intersection(source_tokens))
        < min(3, len(descriptive))
    ):
        return False
    source_numbers = set(
        re.findall(r"\d+(?:[.,]\d+)?", physical_text)
    )
    description_numbers = set(
        re.findall(r"\d+(?:[.,]\d+)?", description)
    )
    return description_numbers.issubset(source_numbers)


def source_evidence_preserved(
    before: list[dict[str, Any]],
    after: list[dict[str, Any]],
) -> bool:
    source_before = {
        evidence_signature(item)
        for item in before
        if item.get("evidenceBasis") != "DERIVED_POLICY"
    }
    source_after = {
        evidence_signature(item)
        for item in after
        if item.get("evidenceBasis") != "DERIVED_POLICY"
    }
    return source_before.issubset(source_after)


def evidence_signature(item: dict[str, Any]) -> tuple[str, ...]:
    return (
        str(item.get("fieldName") or ""),
        str(item.get("rawValue") or ""),
        str(item.get("normalizedValue") or ""),
        str(item.get("sourceLocator") or ""),
        str(item.get("sourceHash") or ""),
    )


def validate_ledger_content(
    entries: Iterable[Any],
    policy: dict[str, Any],
    certified_hashes: set[str],
) -> list[str]:
    scope = str(policy.get("activeBudgetScope") or "")
    failures: list[str] = []
    for item in entries:
        if item.budget_scope != scope:
            continue
        if item.source_hash and item.source_hash not in certified_hashes:
            failures.append(
                f"LEDGER_SOURCE_NOT_CERTIFIED:{item.entry_id}"
            )
        request = json.dumps(
            item.request, sort_keys=True
        ).lower()
        response = json.dumps(
            item.response, sort_keys=True
        ).lower()
        if any(value in request for value in (
            "base64", "image_data", "imagebytes"
        )):
            failures.append(
                f"BEDROCK_REQUEST_CONTAINS_IMAGE:{item.entry_id}"
            )
        if "source transcription" in request or "transcribe" in request:
            failures.append(
                f"BEDROCK_USED_FOR_TRANSCRIPTION:{item.entry_id}"
            )
        if not response or response in {"null", "\"\""}:
            failures.append(
                f"BEDROCK_RESPONSE_MISSING:{item.entry_id}"
            )
        if not item.source_hash:
            failures.append(
                f"LEDGER_SOURCE_HASH_MISSING:{item.entry_id}"
            )
    return failures


def values(item: dict[str, Any]) -> dict[str, Any]:
    return (
        item.get("canonicalValues")
        or item.get("proposedValues")
        or item.get("values")
        or {}
    )


def tokens(value: str) -> set[str]:
    return {
        item.lower()
        for item in re.findall(r"[A-Za-z][A-Za-z0-9'-]{2,}", value)
    }
