"""Finalize and publish all commercially complete certified inventory candidates.

The command requires both 43/43 physical certification and 43/43 Bedrock response
acceptance. It never fabricates a price, date, supplier or buying basis. Rows with
real source ambiguity remain uploaded and review-fenced while complete rows from
the same import may be published.
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi
from inventory_review_http import InventoryReviewHttp

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
CERT = CORPUS / "certification"
TENANT = "10000000-0000-0000-0000-000000000020"
PROVINCES = (
    "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape",
    "Free State", "Limpopo", "Mpumalanga", "North West",
    "Northern Cape",
)
MONEY_CURRENCY = re.compile(r"^\s*(?:ZAR|R)", re.IGNORECASE)


def main() -> int:
    require_gate(CERT / "physical-corpus-certification.json", "passed", True)
    bedrock = require_gate(CERT / "bedrock-corpus-evaluation.json", "passed", True)
    if bedrock.get("passedSourceCount") != 43:
        raise RuntimeError("Bedrock evaluation did not pass all 43 sources.")

    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    review = InventoryReviewHttp(api, TENANT)
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = sorted(
        preflight.get("sources") or [],
        key=lambda item: str(item.get("fileName") or "").lower(),
    )
    if len(sources) != 43:
        raise RuntimeError(f"Expected 43 sources; found {len(sources)}.")

    results: list[dict[str, Any]] = []
    for source in sources:
        result = finalize_source(api, review, source)
        results.append(result)
        write_progress(results, review.contract_summary())

    verification = verify_upload(api, sources, results)
    output = CERT / "inventory-upload-verification.json"
    output.write_text(
        json.dumps(verification, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({key: value for key, value in verification.items() if key != "files"}, indent=2))
    return 0 if verification["passed"] else 2


def finalize_source(
    api: InventoryApi,
    review: InventoryReviewHttp,
    source: dict[str, Any],
) -> dict[str, Any]:
    source_hash = str(source["sourceHash"])
    file_name = str(source["fileName"])
    source_map = read_json(CORPUS / "semantic-v1" / f"{source_hash}.json")
    view = api.read_complete_import(str(source["importId"]))
    if view.get("status") != "REVIEW_REQUIRED":
        raise RuntimeError(f"Import is not review-fenced: {file_name}")

    edited = 0
    approved = 0
    unresolved = 0
    for original in view.get("candidates") or []:
        if str(original.get("status") or "") != "REVIEW_REQUIRED":
            continue
        values = clone(candidate_values(original))
        changes, unresolved_reasons = apply_safe_completion(
            values,
            original,
            source_hash,
            file_name,
            source_map,
        )
        current = original
        if changes:
            current = review.review(
                candidate_id=str(original["id"]),
                candidate_version=require_version(original),
                decision="EDIT",
                values=values,
                reason=(
                    "Applied deterministic production completion: "
                    + ", ".join(sorted(changes))
                ),
            )
            edited += 1
            current = unwrap_candidate(current, str(original["id"]))

        current_values = candidate_values(current)
        blockers = blocking_issues(current)
        publication_missing = publication_missing_fields(current_values)
        unresolved_reasons = sorted(set(
            unresolved_reasons
            + [f"VALIDATION:{item}" for item in blockers]
            + [f"MISSING:{item}" for item in publication_missing]
        ))
        if unresolved_reasons:
            unresolved += 1
            continue
        approved_result = review.review(
            candidate_id=str(current["id"]),
            candidate_version=require_version(current),
            decision="APPROVE",
            values=current_values,
            reason=(
                "Approved after 43-file physical certification and governed "
                "Bedrock response evaluation."
            ),
        )
        unwrap_candidate(approved_result, str(current["id"]))
        approved += 1

    refreshed = api.read_complete_import(str(source["importId"]))
    approved_now = sum(
        str(item.get("status") or "") == "APPROVED"
        for item in refreshed.get("candidates") or []
    )
    published_before = sum(
        str(item.get("status") or "") == "PUBLISHED"
        for item in refreshed.get("candidates") or []
    )
    if approved_now:
        review.publish(
            import_id=str(source["importId"]),
            expected_version=int(refreshed["version"]),
            reason=(
                "Published the commercially complete subset after physical and "
                "Bedrock certification; unresolved source rows remain review-fenced."
            ),
        )
    final = api.read_complete_import(str(source["importId"]))
    counts = status_counts(final.get("candidates") or [])
    return {
        "fileName": file_name,
        "sourceHash": source_hash,
        "importId": source["importId"],
        "editedCandidateCount": edited,
        "approvedCandidateCountThisRun": approved,
        "unresolvedCandidateCountThisRun": unresolved,
        "publishedBeforeCount": published_before,
        "finalImportStatus": final.get("status"),
        "finalFailureCode": final.get("failureCode"),
        "candidateCounts": counts,
    }


def apply_safe_completion(
    values: dict[str, Any],
    candidate: dict[str, Any],
    source_hash: str,
    file_name: str,
    source_map: dict[str, Any],
) -> tuple[set[str], list[str]]:
    changes: set[str] = set()
    unresolved: list[str] = []
    extension = values.setdefault("extension", {})

    if not values.get("productCode"):
        values["productCode"] = product_code(
            source_hash,
            int(candidate.get("rowNumber") or 0),
            str(values.get("name") or "inventory"),
        )
        extension["productCodeBasis"] = "SOURCE_HASH_ROW"
        changes.add("productCode")

    availability = str(values.get("availability") or "").upper()
    if availability not in {"UNAVAILABLE"}:
        if availability != "AVAILABLE":
            values["availability"] = "AVAILABLE"
            extension["availabilityBasis"] = (
                "OWNER_POLICY_NOT_MARKED_UNAVAILABLE"
            )
            changes.add("availability")

    source_text = source_text_for_candidate(source_map, candidate)
    if not values.get("geography"):
        geography = derive_geography(values, source_text)
        values["geography"] = geography
        extension["geographyBasis"] = (
            "SOURCE_CONTEXT" if geography != "Coverage not supplied by source"
            else "EXPLICIT_UNKNOWN"
        )
        changes.add("geography")

    raw_rate = candidate_rate_raw(candidate)
    if not values.get("currency") and raw_rate and MONEY_CURRENCY.search(raw_rate):
        values["currency"] = "ZAR"
        extension["currencyBasis"] = "VISIBLE_R_OR_ZAR_PREFIX"
        changes.add("currency")

    if values.get("rateAmountMinor") is not None and not values.get("rateType"):
        rate_type = derive_rate_type(values, file_name, source_text)
        if rate_type:
            values["rateType"] = rate_type
            extension["rateTypeBasis"] = "EXPLICIT_SOURCE_CONTEXT"
            changes.add("rateType")
        else:
            unresolved.append("BUYING_BASIS_NOT_SUPPLIED")
    if values.get("rateAmountMinor") is None:
        rate_ambiguity = str(extension.get("rateambiguity") or "")
        unresolved.append(
            rate_ambiguity or "RATE_NOT_SUPPLIED_OR_QUOTE_REQUIRED"
        )

    if not values.get("description"):
        unresolved.append("SEARCH_DESCRIPTION_NOT_ACCEPTED")
    if not values.get("channel"):
        unresolved.append("CHANNEL_NOT_ACCEPTED")
    if not values.get("productType"):
        unresolved.append("PRODUCT_TYPE_NOT_ACCEPTED")
    if not values.get("name"):
        unresolved.append("INVENTORY_IDENTITY_NOT_SUPPLIED")

    if unresolved:
        extension["publicationDisposition"] = "REVIEW_REQUIRED"
        extension["publicationBlockers"] = "|".join(sorted(set(unresolved)))
        changes.add("publicationDisposition")
    else:
        extension.pop("publicationBlockers", None)
        extension["publicationDisposition"] = "READY_AFTER_CERTIFICATION"
        changes.add("publicationDisposition")
    return changes, unresolved


def derive_geography(values: dict[str, Any], source_text: str) -> str:
    spatial = values.get("spatial") or {}
    parts = [
        spatial.get("venue"), spatial.get("locality"),
        spatial.get("municipality"), spatial.get("province"),
        spatial.get("country"),
    ]
    supplied = unique_text(parts)
    if supplied:
        return ", ".join(supplied)
    address = str(values.get("address") or "").strip()
    if address:
        return address
    provinces = [province for province in PROVINCES if province.lower() in source_text.lower()]
    if provinces:
        return ", ".join(provinces)
    if any(marker in source_text.lower() for marker in (
        "south africa", "nationwide", "national coverage",
    )):
        return "South Africa"
    return "Coverage not supplied by source"


def derive_rate_type(
    values: dict[str, Any],
    file_name: str,
    source_text: str,
) -> str | None:
    lower = source_text.lower()
    deliverable = values.get("deliverable") or {}
    buying_unit = str(deliverable.get("buyingUnit") or "").lower()
    channel = str(values.get("channel") or "").upper()
    if "cpm" in lower or "cpm" in buying_unit:
        return "CPM"
    if any(marker in lower for marker in ("per month", "monthly rate", "month rate")):
        return "MONTH_RATE"
    if any(marker in lower for marker in ("per week", "weekly rate", "week rate")):
        return "WEEK_RATE"
    if any(marker in lower for marker in ("per day", "daily rate", "day rate")):
        return "DAY_RATE"
    if values.get("package") or "package" in file_name.lower() or "package cost" in lower:
        return "PACKAGE_RATE"
    if channel in {"RADIO", "TV"} and any(
        marker in lower for marker in (
            "net rate", "spot rate", "time band", "30 sec", "30 second",
        )
    ):
        return "SPOT_RATE"
    if any(marker in lower for marker in (
        "per post", "once off", "one-off", "fixed fee", "flat rate",
    )):
        return "FLAT_RATE"
    return None


def publication_missing_fields(values: dict[str, Any]) -> list[str]:
    required = {
        "productCode": values.get("productCode"),
        "name": values.get("name"),
        "channel": values.get("channel"),
        "productType": values.get("productType"),
        "geography": values.get("geography"),
        "rateType": values.get("rateType"),
        "currency": values.get("currency"),
        "rateAmountMinor": values.get("rateAmountMinor"),
        "availability": values.get("availability"),
    }
    return [
        field for field, value in required.items()
        if value is None or value == ""
    ]


def verify_upload(
    api: InventoryApi,
    sources: list[dict[str, Any]],
    run_results: list[dict[str, Any]],
) -> dict[str, Any]:
    physical = read_json(CERT / "physical-corpus-certification.json")
    expected_candidates = int(physical.get("summary", {}).get("candidateCount") or 0)
    files: list[dict[str, Any]] = []
    total = 0
    published = 0
    review_required = 0
    approved = 0
    rejected = 0
    unexplained_review = 0
    failures: list[str] = []
    for source in sources:
        view = api.read_complete_import(str(source["importId"]))
        candidates = view.get("candidates") or []
        total += len(candidates)
        counts = status_counts(candidates)
        published += counts.get("PUBLISHED", 0)
        review_required += counts.get("REVIEW_REQUIRED", 0)
        approved += counts.get("APPROVED", 0)
        rejected += counts.get("REJECTED", 0)
        unexplained = sum(
            str(item.get("status") or "") == "REVIEW_REQUIRED"
            and not str((candidate_values(item).get("extension") or {}).get(
                "publicationBlockers"
            ) or "").strip()
            for item in candidates
        )
        unexplained_review += unexplained
        if view.get("failureCode"):
            failures.append(
                f"IMPORT_FAILURE:{source['fileName']}:{view['failureCode']}"
            )
        if unexplained:
            failures.append(
                f"UNEXPLAINED_REVIEW_ROWS:{source['fileName']}:{unexplained}"
            )
        files.append({
            "fileName": source["fileName"],
            "sourceHash": source["sourceHash"],
            "importId": source["importId"],
            "importStatus": view.get("status"),
            "failureCode": view.get("failureCode"),
            "candidateCount": len(candidates),
            "candidateCounts": counts,
            "unexplainedReviewCount": unexplained,
        })
    if len(files) != 43:
        failures.append(f"SOURCE_COUNT:{len(files)}")
    if total != expected_candidates:
        failures.append(
            f"CANDIDATE_COUNT_MISMATCH:{expected_candidates}->{total}"
        )
    if approved:
        failures.append(f"APPROVED_NOT_PUBLISHED:{approved}")
    if published == 0:
        failures.append("NO_INVENTORY_PUBLISHED")
    result = {
        "schemaVersion": "advertified.inventory-upload-verification.v1",
        "verifiedAtUtc": datetime.now(UTC).isoformat(),
        "passed": not failures,
        "sourceCount": len(files),
        "expectedCandidateCount": expected_candidates,
        "uploadedCandidateCount": total,
        "publishedCandidateCount": published,
        "reviewRequiredCandidateCount": review_required,
        "rejectedCandidateCount": rejected,
        "approvedNotPublishedCount": approved,
        "unexplainedReviewCount": unexplained_review,
        "failures": sorted(set(failures)),
        "runResults": run_results,
        "files": files,
    }
    return result


def source_text_for_candidate(
    source_map: dict[str, Any],
    candidate: dict[str, Any],
) -> str:
    locator = str(candidate.get("sourceLocator") or "")
    scope = locator_scope(locator)
    scoped: list[str] = []
    all_text: list[str] = []
    for item in source_map.get("fragments") or []:
        text = str(item.get("text") or "").strip()
        if not text:
            continue
        all_text.append(text)
        if scope and locator_scope(str(item.get("locator") or "")) == scope:
            scoped.append(text)
    for table in source_map.get("tables") or []:
        locator_value = str(table.get("locator") or "")
        cells = table_strings(table)
        all_text.extend(cells)
        if scope and locator_scope(locator_value) == scope:
            scoped.extend(cells)
    return "\n".join(scoped or all_text)


def locator_scope(locator: str) -> str | None:
    for pattern in (
        r"(?:pdf:)?page=(\d+)",
        r"(?:pptx:)?slide=(\d+)",
        r"(?:xlsx:)?sheet=([^;]+)",
    ):
        match = re.search(pattern, locator, re.IGNORECASE)
        if match:
            return pattern.split("=")[0] + "=" + match.group(1)
    return None


def table_strings(table: dict[str, Any]) -> list[str]:
    values: list[str] = []
    for row in table.get("rows") or []:
        for cell in row:
            if isinstance(cell, dict):
                value = cell.get("value")
                if value is None:
                    value = cell.get("cachedValue")
            else:
                value = cell
            if value is not None and str(value).strip():
                values.append(str(value).strip())
    return values


def product_code(source_hash: str, row: int, name: str) -> str:
    name_hash = hashlib.sha256(name.encode("utf-8")).hexdigest()[:6].upper()
    return f"ADV-{source_hash[:8].upper()}-{row:05d}-{name_hash}"


def candidate_rate_raw(candidate: dict[str, Any]) -> str | None:
    for evidence in candidate.get("evidence") or []:
        if normalize(str(evidence.get("fieldName") or "")) == "rate":
            value = str(evidence.get("rawValue") or "").strip()
            if value:
                return value
    return None


def blocking_issues(candidate: dict[str, Any]) -> list[str]:
    return [
        str(item.get("issueType") or item.get("code") or item.get("fieldName") or "UNKNOWN")
        for item in candidate.get("validation") or []
        if item.get("isBlocking")
    ]


def unwrap_candidate(
    response: dict[str, Any],
    candidate_id: str,
) -> dict[str, Any]:
    candidates: list[dict[str, Any]] = []
    if str(response.get("id") or "") == candidate_id:
        return response
    for key in ("candidate", "data", "result"):
        value = response.get(key)
        if isinstance(value, dict):
            if str(value.get("id") or "") == candidate_id:
                return value
            candidates.extend(value.get("candidates") or [])
    candidates.extend(response.get("candidates") or [])
    match = next(
        (item for item in candidates if str(item.get("id") or "") == candidate_id),
        None,
    )
    if match is None:
        raise RuntimeError(
            f"Review response did not contain candidate {candidate_id}."
        )
    return match


def require_version(candidate: dict[str, Any]) -> int:
    value = candidate.get("version")
    if value is None:
        raise RuntimeError(
            f"Candidate {candidate.get('id')} does not expose a version."
        )
    return int(value)


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
        or {}
    )


def status_counts(candidates: list[dict[str, Any]]) -> dict[str, int]:
    result: dict[str, int] = {}
    for candidate in candidates:
        status = str(candidate.get("status") or "UNKNOWN")
        result[status] = result.get(status, 0) + 1
    return result


def unique_text(values: list[Any]) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        text = str(value or "").strip()
        key = text.lower()
        if text and key not in seen:
            seen.add(key)
            result.append(text)
    return result


def clone(value: dict[str, Any]) -> dict[str, Any]:
    return json.loads(json.dumps(value))


def normalize(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def require_gate(path: Path, key: str, expected: Any) -> dict[str, Any]:
    payload = read_json(path)
    if payload.get(key) != expected:
        raise RuntimeError(f"Required gate failed: {path.name}:{key}")
    return payload


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object in {path}.")
    return value


def write_progress(
    results: list[dict[str, Any]],
    contract: dict[str, Any],
) -> None:
    payload = {
        "schemaVersion": "advertified.inventory-finalization-progress.v1",
        "updatedAtUtc": datetime.now(UTC).isoformat(),
        "completedSourceCount": len(results),
        "apiContract": contract,
        "sources": results,
    }
    (CERT / "inventory-finalization-progress.json").write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, KeyError, json.JSONDecodeError) as error:
        print(json.dumps({"passed": False, "error": str(error)}), file=sys.stderr)
        raise SystemExit(2)
