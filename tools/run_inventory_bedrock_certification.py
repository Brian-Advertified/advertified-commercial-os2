"""Run the governed Bedrock inventory certification plan.

The runner is resumable, one-attempt, hash-bound and hard-capped at US$5,
including the historical uncertainty reserve. It creates no Docker resources.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import time
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import boto3
from botocore.exceptions import BotoCoreError, ClientError

from inventory_bedrock_response_validator import validate_response

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
PHYSICAL_MARKER = CORPUS / "physical-certification-v2" / "ALL_43_PHYSICALLY_CERTIFIED.json"
PREFLIGHT_ROOT = CORPUS / "bedrock-production-certification"
PREFLIGHT_MARKER = PREFLIGHT_ROOT / "PREFLIGHT_APPROVED.json"
PLAN_PATH = PREFLIGHT_ROOT / "plan.json"
REQUEST_ROOT = PREFLIGHT_ROOT / "requests"
RESULT_ROOT = PREFLIGHT_ROOT / "results"
LEDGER_PATH = PREFLIGHT_ROOT / "usage-ledger.json"
LOCK_PATH = PREFLIGHT_ROOT / ".execution.lock"
COMPLETE_MARKER = PREFLIGHT_ROOT / "ALL_BEDROCK_RESPONSES_CERTIFIED.json"
CANARY_MARKER = PREFLIGHT_ROOT / "DMS_CANARY_PASSED.json"
DMS_HASH = "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"
MASTER_DATA_PATH = ROOT / "shared" / "contracts" / "master-data.json"
PROGRAMME_LIMIT_USD_MICROS = 5_000_000
HISTORICAL_RESERVE_USD_MICROS = 188_122
INPUT_USD_MICROS_PER_MILLION = 800_000
OUTPUT_USD_MICROS_PER_MILLION = 3_200_000


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--all", action="store_true",
        help="Run every remaining approved packet after the DMS canary passes.",
    )
    args = parser.parse_args()
    require_guard(PHYSICAL_MARKER, "all43PhysicallyCertified")
    require_guard(PREFLIGHT_MARKER, "approved")
    if args.all:
        require_guard(CANARY_MARKER, "dmsCanaryPassed")
    plan = read_json(PLAN_PATH)
    if not plan.get("withinProgrammeLimit") or int(plan.get("programmeLimitUsdMicros") or 0) != PROGRAMME_LIMIT_USD_MICROS:
        raise RuntimeError("The Bedrock plan is not approved under the US$5 programme limit.")
    acquire_lock()
    try:
        return execute(plan, run_all=args.all)
    finally:
        LOCK_PATH.unlink(missing_ok=True)


def execute(plan: dict[str, Any], *, run_all: bool) -> int:
    allowed_channels, allowed_product_types = governed_codes()
    RESULT_ROOT.mkdir(parents=True, exist_ok=True)
    ledger = load_ledger()
    client = boto3.client(
        "bedrock-runtime",
        region_name=os.environ.get("AWS_REGION", "us-east-1"),
    )
    validated_packets = set()
    for entry in ledger.get("entries", []):
        if entry.get("validationPassed") and entry.get("status") == "COMPLETED":
            validated_packets.add(str(entry.get("packetId")))

    planned_packets = list(plan.get("packets") or [])
    selected_packets = (
        planned_packets
        if run_all
        else [item for item in planned_packets if item.get("sourceHash") == DMS_HASH]
    )
    if not selected_packets:
        raise RuntimeError("The approved plan does not contain the DMS canary packet.")
    for summary in selected_packets:
        position = planned_packets.index(summary) + 1
        packet_id = str(summary["packetId"])
        if packet_id in validated_packets:
            continue
        packet = read_json(ROOT / summary["requestPath"])
        verify_packet(packet, summary)
        prior_entries = [
            item for item in ledger.get("entries", [])
            if str(item.get("packetId")) == packet_id
        ]
        if prior_entries and not any(
            item.get("status") == "COMPLETED"
            and item.get("validationPassed")
            for item in prior_entries
        ):
            raise RuntimeError(
                "A failed terminal packet cannot be retried without a new approved repair packet."
            )
        enforce_budget(ledger, int(packet["maximumCostUsdMicros"]))
        started = datetime.now(UTC).isoformat()
        request_hash = file_hash(ROOT / summary["requestPath"])
        try:
            response = invoke(client, packet)
            response_text = response_text_value(response)
            output = parse_json_response(response_text)
            validation = validate_response(
                packet,
                output,
                allowed_channels,
                allowed_product_types,
            )
            usage = response.get("usage") or {}
            input_tokens = int(usage.get("inputTokens") or 0)
            output_tokens = int(usage.get("outputTokens") or 0)
            actual_cost = actual_cost_usd_micros(input_tokens, output_tokens)
            if actual_cost > int(packet["maximumCostUsdMicros"]):
                raise RuntimeError("Actual Bedrock cost exceeded the preflight packet maximum.")
            result = {
                "schemaVersion": "advertified.inventory-bedrock-result.v1",
                "packetId": packet_id,
                "position": position,
                "packetCount": len(plan.get("packets") or []),
                "sourceHash": packet["sourceHash"],
                "fileName": packet["input"]["fileName"],
                "purpose": packet["purpose"],
                "reason": packet["reason"],
                "modelId": packet["modelId"],
                "promptVersion": packet["promptVersion"],
                "requestHash": request_hash,
                "response": output,
                "rawResponseText": response_text,
                "responseMetadata": response.get("ResponseMetadata") or {},
                "stopReason": response.get("stopReason"),
                "inputTokens": input_tokens,
                "outputTokens": output_tokens,
                "actualCostUsdMicros": actual_cost,
                "maximumCostUsdMicros": packet["maximumCostUsdMicros"],
                "validationPassed": validation.passed,
                "validationFailures": list(validation.failures),
                "productCount": validation.product_count,
                "nonProductFactCount": validation.non_product_fact_count,
                "coveredSourceRowCount": validation.covered_source_row_count,
                "startedAtUtc": started,
                "completedAtUtc": datetime.now(UTC).isoformat(),
            }
            result_path = RESULT_ROOT / f"{packet_id}.json"
            write_json(result_path, result)
            append_ledger(ledger, result, result_path)
            if not validation.passed:
                COMPLETE_MARKER.unlink(missing_ok=True)
                raise RuntimeError(
                    f"Bedrock response {packet_id} failed deterministic grounding validation: "
                    + ", ".join(validation.failures)
                )
            validated_packets.add(packet_id)
        except (BotoCoreError, ClientError, ValueError, KeyError, RuntimeError) as error:
            if not ledger_entry_exists(ledger, packet_id):
                failure = {
                    "schemaVersion": "advertified.inventory-bedrock-result.v1",
                    "packetId": packet_id,
                    "position": position,
                    "packetCount": len(plan.get("packets") or []),
                    "sourceHash": packet["sourceHash"],
                    "fileName": packet["input"]["fileName"],
                    "purpose": packet["purpose"],
                    "reason": packet["reason"],
                    "modelId": packet["modelId"],
                    "promptVersion": packet["promptVersion"],
                    "requestHash": request_hash,
                    "status": "FAILED_TERMINAL",
                    "errorType": type(error).__name__,
                    "error": str(error),
                    "actualCostUsdMicros": 0,
                    "maximumCostUsdMicros": packet["maximumCostUsdMicros"],
                    "validationPassed": False,
                    "startedAtUtc": started,
                    "completedAtUtc": datetime.now(UTC).isoformat(),
                }
                failure_path = RESULT_ROOT / f"{packet_id}.failure.json"
                write_json(failure_path, failure)
                append_ledger(ledger, failure, failure_path)
            raise

    if not run_all:
        canary = dms_canary_reconciliation(plan, ledger)
        write_json(PREFLIGHT_ROOT / "dms-canary-reconciliation.json", canary)
        if not canary["passed"]:
            CANARY_MARKER.unlink(missing_ok=True)
            raise RuntimeError("The DMS Bedrock canary did not match the certified four-row workbook truth.")
        write_json(CANARY_MARKER, {
            "dmsCanaryPassed": True,
            "sourceHash": DMS_HASH,
            "packetCount": canary["packetCount"],
            "physicalRowCount": canary["physicalRowCount"],
            "coveredPhysicalRowCount": canary["coveredPhysicalRowCount"],
            "canonicalProductCount": canary["canonicalProductCount"],
            "actualCostUsdMicros": canary["actualCostUsdMicros"],
            "programmeRemainingUsdMicros": canary["programmeRemainingUsdMicros"],
        })
        print(json.dumps(canary, indent=2))
        return 0

    final = final_reconciliation(plan, ledger)
    write_json(PREFLIGHT_ROOT / "final-reconciliation.json", final)
    if not final["passed"]:
        COMPLETE_MARKER.unlink(missing_ok=True)
        raise RuntimeError("Bedrock packet reconciliation did not pass.")
    write_json(COMPLETE_MARKER, {
        "allBedrockResponsesCertified": True,
        "sourceCount": final["sourceCount"],
        "packetCount": final["packetCount"],
        "physicalRowCount": final["physicalRowCount"],
        "coveredPhysicalRowCount": final["coveredPhysicalRowCount"],
        "canonicalProductCount": final["canonicalProductCount"],
        "actualNewCostUsdMicros": final["actualNewCostUsdMicros"],
        "historicalUsageReserveUsdMicros": HISTORICAL_RESERVE_USD_MICROS,
        "programmeUsedOrReservedUsdMicros": final["programmeUsedOrReservedUsdMicros"],
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "remainingProgrammeAllowanceUsdMicros": final["remainingProgrammeAllowanceUsdMicros"],
    })
    print(json.dumps(final, indent=2))
    return 0


def invoke(client: Any, packet: dict[str, Any]) -> dict[str, Any]:
    user_text = json.dumps({
        "task": packet["purpose"],
        "reason": packet["reason"],
        "outputSchema": packet["outputSchema"],
        "certifiedPhysicalInput": packet["input"],
    }, separators=(",", ":"), sort_keys=True)
    return client.converse(
        modelId=packet["modelId"],
        system=[{"text": packet["systemInstruction"]}],
        messages=[{
            "role": "user",
            "content": [{"text": user_text}],
        }],
        inferenceConfig={
            "maxTokens": int(packet["maximumOutputTokens"]),
            "temperature": 0,
            "topP": 0.1,
        },
    )


def response_text_value(response: dict[str, Any]) -> str:
    content = (((response.get("output") or {}).get("message") or {}).get("content") or [])
    texts = [str(item.get("text") or "") for item in content if isinstance(item, dict) and item.get("text")]
    if not texts:
        raise ValueError("Bedrock response did not contain text output.")
    return "\n".join(texts)


def parse_json_response(text: str) -> dict[str, Any]:
    value = text.strip()
    if value.startswith("```"):
        value = re.sub(r"^```(?:json)?\s*", "", value, flags=re.I)
        value = re.sub(r"\s*```$", "", value)
    parsed = json.loads(value)
    if not isinstance(parsed, dict):
        raise ValueError("Bedrock response must be a JSON object.")
    return parsed


def governed_codes() -> tuple[set[str], set[str]]:
    registry = read_json(MASTER_DATA_PATH)
    collections = registry.get("collections") or {}
    channels = codes_from_collection(collections, "channels")
    product_types = codes_from_collection(collections, "inventoryProductTypes")
    if not channels or not product_types:
        raise RuntimeError("Governed channel/product-type allowlists are unavailable.")
    return channels, product_types


def codes_from_collection(collections: dict[str, Any], expected: str) -> set[str]:
    for key, values in collections.items():
        if key.lower() == expected.lower() and isinstance(values, list):
            return {str(item["code"]) for item in values if isinstance(item, dict) and item.get("code")}
    return set()


def verify_packet(packet: dict[str, Any], summary: dict[str, Any]) -> None:
    if packet.get("packetId") != summary.get("packetId"):
        raise RuntimeError("Packet identity does not match the approved plan.")
    if packet.get("purpose") != "PHYSICAL_INVENTORY_CLASSIFICATION_AND_DESCRIPTION":
        raise RuntimeError("Packet purpose is not approved.")
    if int(packet.get("maximumAttempts") or 0) != 1:
        raise RuntimeError("Only one Bedrock attempt per approved packet is allowed.")
    if not packet.get("input", {}).get("physicalRows"):
        raise RuntimeError("Approved packet has no physical rows.")


def enforce_budget(ledger: dict[str, Any], new_maximum: int) -> None:
    actual = sum(int(item.get("actualCostUsdMicros") or 0) for item in ledger.get("entries", []))
    if HISTORICAL_RESERVE_USD_MICROS + actual + new_maximum > PROGRAMME_LIMIT_USD_MICROS:
        raise RuntimeError("The next call would exceed the total US$5 programme limit.")


def actual_cost_usd_micros(input_tokens: int, output_tokens: int) -> int:
    input_cost = (input_tokens * INPUT_USD_MICROS_PER_MILLION + 999_999) // 1_000_000
    output_cost = (output_tokens * OUTPUT_USD_MICROS_PER_MILLION + 999_999) // 1_000_000
    return input_cost + output_cost


def dms_canary_reconciliation(
    plan: dict[str, Any],
    ledger: dict[str, Any],
) -> dict[str, Any]:
    expected = [
        item for item in plan.get("packets") or []
        if item.get("sourceHash") == DMS_HASH
    ]
    expected_ids = {str(item["packetId"]) for item in expected}
    completed_entries = [
        item for item in ledger.get("entries", [])
        if str(item.get("packetId")) in expected_ids
        and item.get("status") == "COMPLETED"
        and item.get("validationPassed")
    ]
    facts: dict[str, dict[str, Any]] = {}
    products: list[dict[str, Any]] = []
    non_products: list[dict[str, Any]] = []
    for summary in expected:
        packet = read_json(ROOT / summary["requestPath"])
        facts.update({
            str(row["physicalRowId"]): row
            for row in packet["input"]["physicalRows"]
        })
        packet_id = str(summary["packetId"])
        entry = next((
            item for item in completed_entries
            if str(item.get("packetId")) == packet_id
        ), None)
        if entry is None:
            continue
        result = read_json(ROOT / entry["resultPath"])
        output = result.get("response") or {}
        products.extend(output.get("products") or [])
        non_products.extend(output.get("nonProductFacts") or [])
    failures: list[str] = []
    if len(completed_entries) != len(expected):
        failures.append("DMS_PACKETS_INCOMPLETE")
    if len(products) != 4:
        failures.append("DMS_PRODUCT_COUNT_NOT_FOUR")
    identities: list[str] = []
    amounts: list[int] = []
    ambiguous_count = 0
    for product in products:
        source_ids = [str(value) for value in product.get("sourceRowIds") or []]
        source_rows = [facts[value] for value in source_ids if value in facts]
        searchable = json.dumps(source_rows, sort_keys=True, default=str).lower()
        identities.append(searchable)
        rate_id = product.get("rateSourceRowId")
        rate_row = facts.get(str(rate_id)) if rate_id is not None else None
        if rate_row and rate_row.get("rateAmountMinor") is not None:
            amounts.append(int(rate_row["rateAmountMinor"]))
        if "r1,10" in searchable:
            ambiguous_count += 1
            notes = " ".join(str(value) for value in product.get("reviewNotes") or [])
            if not re.search(r"(?i)ambiguous|truncated|review|unclear", notes):
                failures.append("DMS_AMBIGUOUS_RATE_NOTE_MISSING")
    if sorted(amounts) != [20_000, 50_000, 57_500]:
        failures.append("DMS_NORMALIZED_RATE_SET_MISMATCH")
    if ambiguous_count != 1:
        failures.append("DMS_AMBIGUOUS_RATE_PRODUCT_MISMATCH")
    combined = "\n".join(identities)
    if combined.count("dstv stream vod") < 2:
        failures.append("DMS_VOD_PRODUCTS_MISSING")
    if "dstv stream live" not in combined:
        failures.append("DMS_LIVE_PRODUCT_MISSING")
    if "you tube" not in combined and "youtube" not in combined:
        failures.append("DMS_YOUTUBE_PRODUCT_MISSING")
    actual = sum(int(item.get("actualCostUsdMicros") or 0) for item in completed_entries)
    covered = sum(int(item.get("coveredSourceRowCount") or 0) for item in completed_entries)
    physical_rows = sum(int(item.get("rowCount") or 0) for item in expected)
    if covered != physical_rows:
        failures.append("DMS_PHYSICAL_ROW_COVERAGE_MISMATCH")
    return {
        "passed": not failures,
        "failures": failures,
        "sourceHash": DMS_HASH,
        "packetCount": len(completed_entries),
        "expectedPacketCount": len(expected),
        "physicalRowCount": physical_rows,
        "coveredPhysicalRowCount": covered,
        "canonicalProductCount": len(products),
        "nonProductFactCount": len(non_products),
        "actualCostUsdMicros": actual,
        "historicalUsageReserveUsdMicros": HISTORICAL_RESERVE_USD_MICROS,
        "programmeRemainingUsdMicros": (
            PROGRAMME_LIMIT_USD_MICROS
            - HISTORICAL_RESERVE_USD_MICROS
            - sum(int(item.get("actualCostUsdMicros") or 0) for item in ledger.get("entries", []))
        ),
    }


def final_reconciliation(plan: dict[str, Any], ledger: dict[str, Any]) -> dict[str, Any]:
    expected = {str(item["packetId"]): item for item in plan.get("packets") or []}
    completed = {
        str(item.get("packetId")): item
        for item in ledger.get("entries", [])
        if item.get("status") == "COMPLETED" and item.get("validationPassed")
    }
    missing = sorted(set(expected) - set(completed))
    extra = sorted(set(completed) - set(expected))
    actual = sum(int(item.get("actualCostUsdMicros") or 0) for item in completed.values())
    physical_rows = sum(int(item.get("rowCount") or 0) for item in expected.values())
    covered = sum(int(item.get("coveredSourceRowCount") or 0) for item in completed.values())
    products = sum(int(item.get("productCount") or 0) for item in completed.values())
    sources = {str(item.get("sourceHash")) for item in completed.values()}
    used = HISTORICAL_RESERVE_USD_MICROS + actual
    failures = []
    if missing:
        failures.append("PACKETS_MISSING")
    if extra:
        failures.append("UNAPPROVED_PACKETS_PRESENT")
    if len(sources) != 43:
        failures.append("NOT_ALL_SOURCES_PROCESSED")
    if covered != physical_rows:
        failures.append("PHYSICAL_ROW_COVERAGE_MISMATCH")
    if used > PROGRAMME_LIMIT_USD_MICROS:
        failures.append("PROGRAMME_LIMIT_EXCEEDED")
    return {
        "passed": not failures,
        "failures": failures,
        "sourceCount": len(sources),
        "packetCount": len(completed),
        "expectedPacketCount": len(expected),
        "missingPacketIds": missing,
        "extraPacketIds": extra,
        "physicalRowCount": physical_rows,
        "coveredPhysicalRowCount": covered,
        "canonicalProductCount": products,
        "actualNewCostUsdMicros": actual,
        "historicalUsageReserveUsdMicros": HISTORICAL_RESERVE_USD_MICROS,
        "programmeUsedOrReservedUsdMicros": used,
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "remainingProgrammeAllowanceUsdMicros": PROGRAMME_LIMIT_USD_MICROS - used,
    }


def load_ledger() -> dict[str, Any]:
    if LEDGER_PATH.exists():
        return read_json(LEDGER_PATH)
    return {
        "schemaVersion": "advertified.inventory-bedrock-usage-ledger.v1",
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "historicalUsageReserveUsdMicros": HISTORICAL_RESERVE_USD_MICROS,
        "entries": [],
    }


def append_ledger(ledger: dict[str, Any], result: dict[str, Any], path: Path) -> None:
    entry = {
        "packetId": result["packetId"],
        "sourceHash": result["sourceHash"],
        "fileName": result["fileName"],
        "purpose": result["purpose"],
        "reason": result["reason"],
        "modelId": result["modelId"],
        "promptVersion": result["promptVersion"],
        "status": result.get("status") or "COMPLETED",
        "inputTokens": int(result.get("inputTokens") or 0),
        "outputTokens": int(result.get("outputTokens") or 0),
        "actualCostUsdMicros": int(result.get("actualCostUsdMicros") or 0),
        "maximumCostUsdMicros": int(result.get("maximumCostUsdMicros") or 0),
        "validationPassed": bool(result.get("validationPassed")),
        "validationFailures": result.get("validationFailures") or [],
        "productCount": int(result.get("productCount") or 0),
        "coveredSourceRowCount": int(result.get("coveredSourceRowCount") or 0),
        "resultPath": str(path.relative_to(ROOT)),
        "completedAtUtc": result.get("completedAtUtc"),
    }
    ledger.setdefault("entries", []).append(entry)
    ledger["updatedAtUtc"] = datetime.now(UTC).isoformat()
    ledger["actualNewCostUsdMicros"] = sum(int(item.get("actualCostUsdMicros") or 0) for item in ledger["entries"])
    ledger["programmeUsedOrReservedUsdMicros"] = HISTORICAL_RESERVE_USD_MICROS + ledger["actualNewCostUsdMicros"]
    ledger["remainingProgrammeAllowanceUsdMicros"] = PROGRAMME_LIMIT_USD_MICROS - ledger["programmeUsedOrReservedUsdMicros"]
    write_json_atomic(LEDGER_PATH, ledger)


def ledger_entry_exists(ledger: dict[str, Any], packet_id: str) -> bool:
    return any(str(item.get("packetId")) == packet_id for item in ledger.get("entries", []))


def acquire_lock() -> None:
    PREFLIGHT_ROOT.mkdir(parents=True, exist_ok=True)
    try:
        descriptor = os.open(LOCK_PATH, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError as error:
        raise RuntimeError("A Bedrock inventory certification run is already active.") from error
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        stream.write(json.dumps({"pid": os.getpid(), "startedAtUtc": datetime.now(UTC).isoformat()}))
        stream.flush()
        os.fsync(stream.fileno())


def require_guard(path: Path, field: str) -> None:
    value = read_json(path)
    if not value.get(field):
        raise RuntimeError(f"Required guard {path.name} is not approved.")


def file_hash(path: Path) -> str:
    import hashlib
    return hashlib.sha256(path.resolve(strict=True).read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    write_json(temporary, value)
    os.replace(temporary, path)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, RuntimeError, BotoCoreError, ClientError) as error:
        print(json.dumps({
            "passed": False,
            "errorType": type(error).__name__,
            "error": str(error),
            "paidExecutionMayHaveStarted": LEDGER_PATH.exists(),
        }, indent=2))
        raise SystemExit(2)
