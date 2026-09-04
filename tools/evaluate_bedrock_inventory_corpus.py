"""Validate every Bedrock-enriched candidate against the immutable physical baseline."""

from __future__ import annotations

import json
import re
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
TENANT = "10000000-0000-0000-0000-000000000020"
PROGRAMME_LIMIT = 5_000_000
HISTORICAL_RESERVE = 250_000
NEW_RUN_LIMIT = 4_750_000
MONEY = re.compile(r"(?<![A-Za-z])(?:ZAR|R)\s*\d[\d\s.,\u00a0]*", re.IGNORECASE)
DATE = re.compile(r"\b(?:20\d{2}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]20\d{2})\b")
MASTER_DATA = ROOT / "shared" / "contracts" / "master-data.json"


def main() -> int:
    baseline = read_json(CORPUS / "certification" / "pre-bedrock-candidate-baseline.json")
    progress = read_json(CORPUS / "certification" / "bedrock-corpus-progress.json")
    if baseline.get("sourceCount") != 43:
        raise RuntimeError("Pre-Bedrock baseline is incomplete.")
    if progress.get("status") != "COMPLETED" or progress.get("completedSourceCount") != 43:
        raise RuntimeError("Bedrock corpus run did not complete all 43 sources.")

    allowed_channels, allowed_product_types = governed_semantic_codes()
    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = {str(item["sourceHash"]): item for item in preflight.get("sources") or []}
    failures: list[str] = []
    file_results: list[dict[str, Any]] = []
    total_candidates = 0
    changed_source_facts = 0
    missing_descriptions = 0
    invalid_codes = 0

    for base_file in baseline.get("files") or []:
        source_hash = str(base_file["sourceHash"])
        source = sources.get(source_hash)
        if source is None:
            failures.append(f"LIVE_SOURCE_MISSING:{base_file['fileName']}")
            continue
        current = api.read_complete_import(str(source["importId"]))
        current_candidates = current.get("candidates") or []
        total_candidates += len(current_candidates)
        base_candidates = base_file.get("candidates") or []
        file_failures: list[str] = []
        if current.get("status") != "REVIEW_REQUIRED":
            file_failures.append("IMPORT_NOT_REVIEW_REQUIRED")
        if current.get("failureCode"):
            file_failures.append(f"IMPORT_FAILURE:{current['failureCode']}")
        if len(current_candidates) != len(base_candidates):
            file_failures.append(
                f"CANDIDATE_COUNT_CHANGED:{len(base_candidates)}->{len(current_candidates)}"
            )

        baseline_by_key = {stable_key(item): item for item in base_candidates}
        current_by_key = {
            current_key(item): item for item in current_candidates
        }
        if Counter(baseline_by_key) != Counter(current_by_key):
            file_failures.append("CANDIDATE_IDENTITY_SET_CHANGED")

        source_map = read_json(CORPUS / "semantic-v1" / f"{source_hash}.json")
        source_text = normalize_words(" ".join(source_strings(source_map)))
        for key, base_candidate in baseline_by_key.items():
            candidate = current_by_key.get(key)
            if candidate is None:
                continue
            values = candidate_values(candidate)
            current_source = current_source_facts(candidate, values)
            if canonical(current_source) != canonical(base_candidate.get("sourceFacts") or {}):
                changed_source_facts += 1
                file_failures.append(f"SOURCE_FACTS_CHANGED:{key}")
            channel = values.get("channel")
            product_type = values.get("productType")
            description = str(values.get("description") or "").strip()
            if channel not in allowed_channels:
                invalid_codes += 1
                file_failures.append(f"CHANNEL_NOT_ALLOWLISTED:{key}:{channel}")
            if product_type not in allowed_product_types:
                invalid_codes += 1
                file_failures.append(
                    f"PRODUCT_TYPE_NOT_ALLOWLISTED:{key}:{product_type}"
                )
            if not description:
                missing_descriptions += 1
                file_failures.append(f"DESCRIPTION_MISSING:{key}")
            else:
                description_failure = validate_description(
                    description,
                    values,
                    source_text,
                )
                if description_failure:
                    file_failures.append(f"{description_failure}:{key}")

        file_failures = sorted(set(file_failures))
        failures.extend(
            f"{base_file['fileName']}:{failure}" for failure in file_failures
        )
        file_results.append({
            "fileName": base_file["fileName"],
            "sourceHash": source_hash,
            "baselineCandidateCount": len(base_candidates),
            "currentCandidateCount": len(current_candidates),
            "passed": not file_failures,
            "failures": file_failures,
        })

    committed = int(preflight.get("existingCommittedCostUsdMicros") or 0)
    if committed > NEW_RUN_LIMIT:
        failures.append(f"NEW_RUN_COST_LIMIT_EXCEEDED:{committed}")
    if HISTORICAL_RESERVE + committed > PROGRAMME_LIMIT:
        failures.append(
            f"PROGRAMME_USD5_LIMIT_EXCEEDED:{HISTORICAL_RESERVE + committed}"
        )
    if len(file_results) != 43:
        failures.append(f"SOURCE_EVALUATION_COUNT:{len(file_results)}")

    failures = sorted(set(failures))
    result = {
        "schemaVersion": "advertified.inventory-bedrock-corpus-evaluation.v1",
        "evaluatedAtUtc": datetime.now(UTC).isoformat(),
        "passed": not failures,
        "sourceCount": len(file_results),
        "passedSourceCount": sum(item["passed"] for item in file_results),
        "candidateCount": total_candidates,
        "changedSourceFactCount": changed_source_facts,
        "missingDescriptionCount": missing_descriptions,
        "invalidSemanticCodeCount": invalid_codes,
        "committedCostUsdMicros": committed,
        "historicalReserveUsdMicros": HISTORICAL_RESERVE,
        "programmeLimitUsdMicros": PROGRAMME_LIMIT,
        "failures": failures,
        "files": file_results,
    }
    output = CORPUS / "certification" / "bedrock-corpus-evaluation.json"
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in result.items() if key != "files"}, indent=2))
    return 0 if not failures else 2


def governed_semantic_codes() -> tuple[set[str], set[str]]:
    registry = read_json(MASTER_DATA)
    collections = registry.get("collections") or {}
    channels: set[str] = set()
    product_types: set[str] = set()
    for name, records in collections.items():
        normalized_name = normalize(str(name))
        if not isinstance(records, list):
            continue
        codes = {
            str(record.get("code"))
            for record in records
            if isinstance(record, dict) and record.get("code")
        }
        if "channel" in normalized_name:
            channels.update(codes)
        if "producttype" in normalized_name or "inventoryproduct" in normalized_name:
            product_types.update(codes)
    if not channels:
        raise RuntimeError("The governed channel collection was not found.")
    if not product_types:
        raise RuntimeError("The governed inventory product-type collection was not found.")
    return channels, product_types


def validate_description(
    description: str,
    values: dict[str, Any],
    source_words: set[str],
) -> str | None:
    if len(description) < 20 or len(description) > 1_500:
        return "DESCRIPTION_LENGTH_INVALID"
    if MONEY.search(description):
        source_rate = values.get("rateAmountMinor")
        if source_rate is None:
            return "DESCRIPTION_INVENTED_PRICE"
    if DATE.search(description):
        terms = values.get("commercialTerms") or {}
        if not terms.get("rateValidFrom") and not terms.get("rateValidTo"):
            return "DESCRIPTION_INVENTED_DATE"
    words = normalize_words(description)
    meaningful = {
        word for word in words
        if len(word) >= 4 and word not in {
            "this", "that", "with", "from", "your", "their", "into",
            "placement", "advertising", "media", "audience",
        }
    }
    if meaningful and len(meaningful & source_words) / len(meaningful) < DecimalRatio:
        return "DESCRIPTION_WEAKLY_GROUNDED"
    return None


DecimalRatio = 0.20


def stable_key(item: dict[str, Any]) -> str:
    facts = item.get("sourceFacts") or {}
    return "|".join([
        str(item.get("rowNumber") or ""),
        str(item.get("sourceLocator") or ""),
        str(facts.get("productCode") or ""),
        str(facts.get("name") or ""),
    ])


def current_key(candidate: dict[str, Any]) -> str:
    values = candidate_values(candidate)
    return "|".join([
        str(candidate.get("rowNumber") or ""),
        str(candidate.get("sourceLocator") or ""),
        str(values.get("productCode") or ""),
        str(values.get("name") or ""),
    ])


def current_source_facts(
    candidate: dict[str, Any],
    values: dict[str, Any],
) -> dict[str, Any]:
    evidence = candidate.get("evidence") or []
    return {
        "productCode": values.get("productCode"),
        "name": values.get("name"),
        "geography": values.get("geography"),
        "address": values.get("address"),
        "latitude": values.get("latitude"),
        "longitude": values.get("longitude"),
        "rateType": values.get("rateType"),
        "currency": values.get("currency"),
        "rateAmountMinor": values.get("rateAmountMinor"),
        "availability": values.get("availability"),
        "supplierCommercial": values.get("supplierCommercial"),
        "supplierContacts": values.get("supplierContacts") or [],
        "commercialTerms": values.get("commercialTerms"),
        "deliverable": values.get("deliverable"),
        "spatial": values.get("spatial"),
        "package": values.get("package"),
        "extension": source_extension(values.get("extension") or {}),
        "supplierEvidence": [
            evidence_fact(item)
            for item in evidence
            if normalize(str(item.get("fieldName") or "")) in {"supplier", "suppliername"}
        ],
        "rateEvidence": [
            evidence_fact(item)
            for item in evidence
            if normalize(str(item.get("fieldName") or "")) == "rate"
        ],
    }


def source_extension(extension: dict[str, Any]) -> dict[str, Any]:
    semantic_markers = (
        "semantic", "embedding", "classification", "taxonomy",
        "model", "prompt", "agent", "enrichment",
    )
    return {
        key: value for key, value in extension.items()
        if not any(
            marker in normalize(str(key))
            for marker in semantic_markers
        )
    }


def evidence_fact(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "fieldName": item.get("fieldName"),
        "rawValue": item.get("rawValue"),
        "normalizedValue": item.get("normalizedValue"),
        "sourceLocator": item.get("sourceLocator"),
        "sourceHash": item.get("sourceHash"),
        "evidenceBasis": item.get("evidenceBasis"),
    }


def source_strings(source_map: dict[str, Any]) -> list[str]:
    result = [str(item.get("text") or "") for item in source_map.get("fragments") or []]
    for table in source_map.get("tables") or []:
        for row in table.get("rows") or []:
            for cell in row:
                if isinstance(cell, dict):
                    value = cell.get("value") if cell.get("value") is not None else cell.get("cachedValue")
                else:
                    value = cell
                if value is not None:
                    result.append(str(value))
    return result


def normalize_words(value: str) -> set[str]:
    return {word.lower() for word in re.findall(r"[A-Za-z0-9]+", value)}


def normalize(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    return candidate.get("canonicalValues") or candidate.get("proposedValues") or candidate.get("values") or {}


def canonical(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
